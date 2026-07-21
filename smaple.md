Understood — logic first, CSS later. This one needs a bit of care though, because it changes something that's currently a *shared* function (`buildStatusFilter`) used by every single role's dashboard query — so let me walk through exactly how I've scoped it to touch only OnshorePanel.

## What's happening today

`OnshorePanel` currently inherits the general "hierarchy" visibility rule that every role uses: `ROLE_STATUS_MAP.OnshorePanel` includes the *entire cascade* — `PendingTech1`, `PendingTech2`, `PendingTech2OrMgmt`, `PendingMgmt`, `PendingOnshoreOrHR`, `PendingOnshore` — so an Onshore panel member currently sees every candidate from Tech Round 1 onward, not just the ones actually at their stage. And there's no assignee check anywhere in the query at all — status match alone is enough to show a row, regardless of who (if anyone) is assigned to the Onshore round.

You want OnshorePanel specifically narrowed to: **only the onshore-pending status, and only when the Onshore round is assigned to that exact user.**

One heads-up before the fix: I traced `computeStatus()` carefully, and it never actually assigns the standalone `PendingOnshore` status to a candidate — when the onshore round is genuinely pending, the status that gets written is `PendingOnshoreOrHR`. So the fix below matches against `PendingOnshoreOrHR`, since that's what your onshore-pending candidates actually carry. Flagging this so you can confirm it lines up with what you're seeing in your SharePoint `StatusCode` column.

## The fix — scoped entirely to OnshorePanel

**`odata-filter.service.ts`** — `buildStatusFilter()` gets one new optional parameter and OnshorePanel is pulled out of the shared "union of allowed statuses" loop into its own AND-scoped clause:

```ts
export function buildStatusFilter(
  roles: UserRole[],
  tab: 'active' | 'rejected' | null,
  meId?: number,   // NEW — only used for OnshorePanel's own-assignment restriction
): string {
  const rejectedCode = STATUS_CODE[WorkflowStatus.Rejected];
  const isPower = roles.some(r => POWER_ROLES.includes(r));

  if (tab === 'rejected') return `StatusCode eq ${rejectedCode}`;

  if (isPower) {
    return tab === 'active' ? `StatusCode ne ${rejectedCode}` : '';
  }

  const clauses: string[] = [];

  // Every role except OnshorePanel keeps the exact existing behavior — the union of
  // whatever statuses ROLE_STATUS_MAP allows for that role, unchanged.
  const nonOnshoreRoles = roles.filter(r => r !== 'OnshorePanel');
  const allowed = new Set<number>();
  for (const role of nonOnshoreRoles) {
    (ROLE_STATUS_MAP[role] ?? []).forEach(s => {
      if (tab === 'active' && s === WorkflowStatus.Rejected) return;
      allowed.add(STATUS_CODE[s]);
    });
  }
  allowed.forEach(c => clauses.push(`StatusCode eq ${c}`));

  // OnshorePanel: only the onshore-pending status, and only rows assigned to this user.
  if (roles.includes('OnshorePanel') && meId != null) {
    const onshoreCode = STATUS_CODE[WorkflowStatus.PendingOnshoreOrHR];
    clauses.push(`(StatusCode eq ${onshoreCode} and OnShoreRoundInterviewedById eq ${meId})`);
  }

  if (clauses.length === 0) return 'StatusCode eq -1';
  return clauses.length > 1 ? `(${clauses.join(' or ')})` : clauses[0];
}
```

**`candidate.service.ts`** — the two call sites (`queryFor()` and `getCount()`, which must stay in sync so the pagination total matches the actual rows) need to pass the current user's id through:

```ts
private queryFor(tab, filters, location, roles, pageSize): ListQuery {
  const meId = this.currentUser.get().id;
  const statusClause = buildStatusFilter(roles, tab, meId);   // was: buildStatusFilter(roles, tab)
  // ...rest unchanged
}

getCount(tab, location, filters, roles): Observable<number> {
  const meId = this.currentUser.get().id;
  const statusClause = buildStatusFilter(roles, tab, meId);   // was: buildStatusFilter(roles, tab)
  // ...rest unchanged
}
```

## Why every other role is unaffected

- **HrAdmin/Recruiter**: the `isPower` branch returns before either the union loop or the Onshore-specific block ever runs — completely untouched, still see everything.
- **TechPanel / MgmtPanel alone**: `nonOnshoreRoles` still contains them (nothing's filtered out), so the union-of-statuses loop runs identically to before and produces the same clause string, character for character.
- **A user who holds OnshorePanel *and* another role** (like the `Management Panel · Technical Panel` combo I saw in your screenshot) — their Tech/Mgmt visibility stays exactly as it was; only the portion of the query contributed by OnshorePanel gets the new restrictive `(status AND assigned-to-me)` clause, OR'd alongside their other role's normal clause. So a hybrid user doesn't lose visibility on the Tech/Mgmt side.
- **Rejected tab**: unreachable for non-power roles anyway (`showRejectedTab` in the dashboard is already power-user-only), so this change never interacts with it.

## One more thing worth confirming

With this change, an Onshore panel member will **never see an unassigned onshore-pending candidate** — only ones already assigned to them specifically. I checked, and this is actually consistent with how the round is already configured (`onshoreRound.selfAssign` is an empty array — there was never a self-claim button for this round to begin with, only HR can assign it), so you're not losing any "claim it myself" capability that existed before. But it does mean Onshore panel members have no way to discover unclaimed onshore candidates through the dashboard at all — that has to come through HR assigning it first. Let me know if that's exactly what you want, or if unassigned-but-pending-onshore candidates should still show up (just visually flagged as unassigned) so panel members know work is coming.

Let me know how it looks once you've wired it in, and whether the unassigned-onshore-candidate behavior above is what you actually want.