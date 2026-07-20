Good diagnostics from the screenshots — I can pin down concrete root causes for all four. Let's go one at a time.

## 1. Status not updating on unassign — the real cause (Edm.String error)

Image 1's error — `Cannot convert a primitive value to the expected type 'Edm.String'` — happens on the exact MERGE call shown in image 2, whose payload is:
```json
{ Status: "Pending with Tech Round 2 or Management Round", StatusCode: 2 }
```
This error means SharePoint's server-side column definition for `StatusCode` expects a **string**, but the request sends a raw JSON **number** (`2`, unquoted). That happens when the `StatusCode` column was actually created as **Single line of text** instead of **Number** — SharePoint then rejects the numeric primitive.

**This is why the interviewer removal succeeds but the status never updates**: `unassignInterviewer()` is a separate, earlier MERGE call that only touches `InterviewedById` (Person field) — that one succeeds fine. The *next* call, `patchStatus()`, is the one that throws, so `Status`/`StatusCode` never actually get written to SharePoint at all.

**Fix — check the column type first:** go to the list settings for `StatusCode` on `MumbaiInterview` and confirm its type. If it's Text, recreate it properly as **Number**. If for some reason you want to keep it as Text (not recommended), the code-side fix is to send it as a quoted string and switch filter comparisons to string equality:

```typescript
// workflow.service.ts patchStatus() — only needed if StatusCode column is Text, not Number
return this.updateItem(LOCATION_LIST[candidate.location], candidate.id, {
  Status: newStatus,
  StatusCode: String(STATUS_CODE[newStatus]),   // quote it if column is Text
});
```
```typescript
// odata-filter.service.ts — match filter syntax to column type
`StatusCode eq '${c}'`   // instead of `StatusCode eq ${c}` if Text
```
**Recreating the column as true Number is the correct fix** — it's faster to index/filter and avoids this entirely.

## 2. Defensive fix — mutate local state before `patchStatus()`

Separately from the type bug, `confirmUnassign()` calls `patchStatus()` right after `unassignInterviewer()`, but `computeStatus()` reads off the **in-memory** `candidate` object — which `unassignInterviewer()` never touches (it only writes to SharePoint). So even once the Edm.String bug is fixed, status would be computed from **stale local fields**. Every round panel's `submit()` already does this correctly (mutates `this.candidate[round].selection` before calling `patchStatus`) — `confirmUnassign()` needs the same pattern:

```typescript
confirmUnassign(): void {
  if (!this.confirmCtx) return;
  const candidate = this.confirmCtx.candidate as Candidate;
  const round = this.confirmCtx.round;

  this.candidates.unassignInterviewer(candidate, ROUNDS[round].prefix).subscribe({
    next: () => {
      candidate[round].interviewedBy = null;   // NEW — mirror the write locally

      if (round === 'hrRound') {
        if (candidate.onshoreRound.selection === 'N/A') {
          const clearSkipFields: Record<string, any> = {
            OnShoreRoundInterviewSelection: 'Pending',
            OnShoreRoundInterviewedById: null,
            OnShoreRoundInterviewDate: null,
          };
          this.candidates.submitRound(candidate, clearSkipFields).subscribe({
            next: () => {
              candidate.onshoreRound.selection = RoundStatus.Pending;   // NEW
              candidate.onshoreRound.interviewedBy = null;              // NEW
              candidate.onshoreRound.interviewDate = null;              // NEW
              this.candidates.patchStatus(candidate).subscribe({
                next: () => { this.confirmCtx = null; this.reload(); },
                error: err => { this.error = this.humanError(err); this.confirmCtx = null; },
              });
            },
            error: err => { this.error = this.humanError(err); this.confirmCtx = null; },
          });
          return;
        }
        this.candidates.patchStatus(candidate).subscribe({
          next: () => { this.confirmCtx = null; this.reload(); },
          error: err => { this.error = this.humanError(err); this.confirmCtx = null; },
        });
        return;
      }

      if (round === 'mgmtRound') {
        const tech2WasSkipped = candidate.techRound2.selection === 'N/A';
        if (tech2WasSkipped) {
          this.candidates.unassignInterviewer(candidate, ROUNDS.techRound2.prefix, true).subscribe({
            next: () => {
              candidate.techRound2.selection = RoundStatus.Pending;   // NEW
              candidate.techRound2.interviewedBy = null;              // NEW
              this.candidates.patchStatus(candidate).subscribe({
                next: () => { this.confirmCtx = null; this.reload(); },
                error: err => { this.error = this.humanError(err); this.confirmCtx = null; },
              });
            },
            error: err => { this.error = this.humanError(err); this.confirmCtx = null; },
          });
          return;
        }
        this.candidates.patchStatus(candidate).subscribe({
          next: () => { this.confirmCtx = null; this.reload(); },
          error: err => { this.error = this.humanError(err); this.confirmCtx = null; },
        });
        return;
      }

      this.candidates.patchStatus(candidate).subscribe({
        next: () => { this.confirmCtx = null; this.reload(); },
        error: err => { this.error = this.humanError(err); this.confirmCtx = null; },
      });
    },
    error: err => { this.error = this.humanError(err); this.confirmCtx = null; },
  });
}
```

**Same gap exists in `confirmHrAssign()`'s onshore-skip branch** — `skipOnshoreRound()` changes the onshore selection to `N/A` (a real stage change), but the current code never calls `patchStatus` afterward. Add the local mutation + `patchStatus` call there too, right after the `skipOnshoreRound` write succeeds.

## 3. Rejected candidates showing in the Active tab (power users)

Found it — in `buildStatusFilter()`:
```typescript
if (roles.some(r => POWER_ROLES.includes(r))) return '';
```
For power users on the **active** tab, this returns an empty filter — meaning no status constraint at all, so `Rejected` candidates leak straight into the active view. Fix:

```typescript
export function buildStatusFilter(roles: UserRole[], tab: 'active' | 'rejected'): string {
  if (tab === 'rejected') return `StatusCode eq ${STATUS_CODE[WorkflowStatus.Rejected]}`;

  if (roles.some(r => POWER_ROLES.includes(r))) {
    return `StatusCode ne ${STATUS_CODE[WorkflowStatus.Rejected]}`;   // FIXED — still exclude Rejected
  }
  // ...rest unchanged
}
```

## 4. "Someone above my hierarchy shouldn't appear"

This is very likely the **same bug as #3**, not a separate one. Your `ROLE_STATUS_MAP` in `cohort.config.ts` already correctly scopes each role's status set (TechPanel only sees Tech-stage statuses, MgmtPanel sees Tech+Mgmt, etc.) — that logic is sound and enforces the hierarchy correctly *as long as it's actually applied*. If you tested this while logged in as an HR/Recruiter (power role), the empty-filter bug in #3 means you were seeing literally everything, which would look like "hierarchy isn't working" even though it is for non-power roles. Retest with a Tech-only or Mgmt-only test account after the #3 fix — if it's still leaking, tell me which role/candidate combination and I'll dig into `ROLE_STATUS_MAP` itself.

## 5. Pagination NaN + adding 10/25/50/100

Image 4 confirms it: `$top expression "NaN" is not valid`. Root cause — you're using `[ngValue]` on the `<option>` elements (needed for non-string binding) **and** manually reading `$event.target.value` in the `(change)` handler. With `[ngValue]`, the raw DOM `value` is an Angular-internal index string (like `"1: 50"`), not your actual number — so `Number(...)` on it produces `NaN`. Fix by using `ngModelChange` instead, which gives you the real bound value directly:

```html
<select class="filter" [ngModel]="pageSize" (ngModelChange)="setPageSize($event)">
  <option [ngValue]="10">10</option>
  <option [ngValue]="25">25</option>
  <option [ngValue]="50">50</option>
  <option [ngValue]="100">100</option>
</select>
```

```typescript
setPageSize(size: number): void {
  if (size === this.pageSize) return;
  this.pageSize = size;
  this.reload();
}
```

Note: switched from `[(ngModel)]` (two-way box syntax) to one-way `[ngModel]` + `(ngModelChange)` — combining both on the same element was part of what caused the bad value to be read. This also gives you the 10/25/50/100 set you asked for.

Want me to also check `confirmHrAssign()` end-to-end the same way I did `confirmUnassign()`, since it shares the missing-`patchStatus`-after-skip issue?