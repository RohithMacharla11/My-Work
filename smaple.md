That's a reasonable optimization, but let me flag the tradeoffs before you commit, since it touches a live SharePoint column with existing data.

**What it actually saves vs. what it doesn't**

Numeric codes do shrink the filter clause (`StatusCode eq 3` vs `Status eq 'Pending with Tech Round 2 or Management Round'` — ~50 fewer characters per clause). But your original 404 was caused by the **`$select`/`$expand` field list** (skills×3×10 + rounds×5 + person expansions), which is hundreds of characters and completely unrelated to Status. That fix (splitting `DASHBOARD_SELECT_FIELDS` from the full detail set) is still necessary regardless of what you do with Status — numeric codes alone won't fix the 404 if you keep sending the full field list.

That said, shorter status clauses do help, especially for roles like OnshorePanel where 5 statuses get OR'd together. Worth doing as a second layer of safety margin.

**The right way to do it — don't replace the string column, add a parallel numeric one**

Replacing the Choice column outright breaks anything reading the human-readable value (SharePoint list views, any reports, HR staff eyeballing the list) and requires a one-time data migration with real risk of mismatches. Instead, add a second column purely for filtering:

**1. SharePoint** — add a new column `StatusCode` (type: **Number**) to all three lists, alongside the existing `Status` (Choice, keep as-is for humans to read).

**2. `cohort.config.ts`** — add the numeric mapping next to the existing enum:

```typescript
export const STATUS_CODE: Record<WorkflowStatus, number> = {
  [WorkflowStatus.Rejected]: 0,
  [WorkflowStatus.PendingTech1]: 1,
  [WorkflowStatus.PendingTech2OrMgmt]: 2,
  [WorkflowStatus.PendingMgmt]: 3,
  [WorkflowStatus.PendingOnshoreOrHR]: 4,
  [WorkflowStatus.PendingOnshore]: 5,
  [WorkflowStatus.PendingHR]: 6,
};

export const CODE_TO_STATUS: Record<number, WorkflowStatus> =
  Object.fromEntries(Object.entries(STATUS_CODE).map(([k, v]) => [v, k as WorkflowStatus])) as any;
```

**3. `workflow.service.ts` `computeStatus()`** — no change to the logic itself; just write both fields wherever Status is patched:

```typescript
patchStatus(candidate: Candidate): Observable<void> {
  const newStatus = WorkflowService.computeStatus(candidate);
  return this.updateItem(LOCATION_LIST[candidate.location], candidate.id, {
    Status: newStatus,
    StatusCode: STATUS_CODE[newStatus],   // NEW
  });
}
```

**4. `odata-filter.service.ts` `buildStatusFilter`** — switch to numbers, no quotes needed:

```typescript
export function buildStatusFilter(roles: UserRole[], tab: 'active' | 'rejected'): string {
  if (tab === 'rejected') return `StatusCode eq ${STATUS_CODE[WorkflowStatus.Rejected]}`;
  if (roles.some(r => POWER_ROLES.includes(r))) return '';

  const allowed = new Set<number>();
  for (const role of roles) {
    (ROLE_STATUS_MAP[role] ?? []).forEach(s => {
      if (s !== WorkflowStatus.Rejected) allowed.add(STATUS_CODE[s]);
    });
  }

  const allActiveCodes = Object.values(STATUS_CODE).filter(c => c !== STATUS_CODE[WorkflowStatus.Rejected]);
  if (allowed.size === 0) return `StatusCode eq -1`; // matches nothing
  if (allowed.size >= allActiveCodes.length) return '';

  const excluded = allActiveCodes.filter(c => !allowed.has(c));
  const orGroup = [...allowed].map(c => `StatusCode eq ${c}`).join(' or ');
  const andGroup = excluded.map(c => `StatusCode ne ${c}`).join(' and ');

  if (allowed.size <= excluded.length) {
    return allowed.size > 1 ? `(${orGroup})` : orGroup;
  }
  return andGroup;
}
```

Same parenthesization rule as before still applies — a multi-clause OR group must still be wrapped, that bug wasn't about strings vs numbers.

**5. Everywhere else** (`candidate.model.ts`, templates, `getDetailedStatusLabel`, etc.) — **keep using `Status` (the string) for anything shown to a human.** Only the filter-building path touches `StatusCode`. `candidate.service.ts`'s `mapCandidate()` doesn't need to read `StatusCode` at all — it's write-only from the app's perspective, purely there to make filters cheap for SharePoint's indexer too (Number columns index and filter faster than Choice/Text in large lists).

**One migration step you do need**: existing rows won't have `StatusCode` populated yet. Either run `patchStatus()` once across all existing candidates (a small one-off script or admin action) to backfill it, or have `buildStatusFilter`/dashboard fall back gracefully — but the cleanest option is the backfill, since a stale/missing `StatusCode` would otherwise silently hide existing candidates from filtered views.

Want me to write that backfill script (loop all items in each list, call `patchStatus` for each) as a one-time admin utility you can run once and then remove?