Three separate bugs here, all fixable quickly. Let's go one by one — I found the actual causes.

**Bug 1 & 2 (business unit not saving + old assignment shown) — same root cause.** In `confirmHrAssign()`, the code only ever calls `assignInterviewer()`, which writes `InterviewedById` — it never writes the business unit field at all, even though the modal captures it in `assignCtx.businessUnit`. That's why it silently reverts to "Not set." The "old assignment still there" symptom is the same bug's side effect: since nothing else refreshes the local candidate object after reassigning, the UI keeps showing stale data until you reload.

**Bug 3 (should only navigate in if assigning to self)** — `confirmHrAssign()` unconditionally calls `navigateToCandidate()` after every assign, regardless of who it was assigned to.

**Fix — replace `confirmHrAssign()` in `dashboard.component.ts` entirely:**

```ts
confirmHrAssign(): void {
  if (!this.assignCtx?.picked) return;
  const { candidate, round } = this.assignCtx;
  const user = this.assignCtx.picked;
  const businessUnit = this.assignCtx.businessUnit;

  const targetPrefix = this.assignTargetRound === 'hrRound'
    ? ROUNDS['hrRound'].prefix
    : ROUNDS['onshoreRound'].prefix;

  const fields: Record<string, any> = {
    [`${targetPrefix}InterviewedById`]: user.id,
  };
  if (this.assignTargetRound === 'onshoreRound' && businessUnit) {
    fields['OnshoreRoundDepartment'] = businessUnit;   // ONSHORE_BU_COL — confirm exact internal name matches cohort.config.ts
  }

  this.candidates.updateItem(LOCATION_LIST[candidate.location], candidate.id, fields).pipe(
    switchMap(() => this.assignTargetRound === 'hrRound'
      ? this.candidates.skipOnshoreRound(candidate)
      : of(void 0))
  ).subscribe({
    next: () => {
      this.assignCtx = null;
      const me = this.currentUser.get();
      if (user.id === me.id) {
        this.navigateToCandidate(candidate);   // only enter it if you assigned yourself
      } else {
        this.reload();                          // otherwise just refresh the list
      }
    },
    error: err => { this.error = this.humanError(err); this.assignCtx = null; },
  });
}
```

(Add `switchMap`, `of` to your rxjs imports if not already there — `switchMap` is already imported elsewhere in your services.)

**Bug 4 — Recruiter has full edit rights, should be HrAdmin-only.** In `workflow.service.ts`, `getAccess()`, find:
```ts
if (this.currentUser.isAdmin() || this.currentUser.hasRole('Recruiter')) {
  return { state: 'editable', canEdit: true, assignedToName: assignedTo?.title };
}
```
Replace with:
```ts
if (this.currentUser.isAdmin()) {
  return { state: 'editable', canEdit: true, assignedToName: assignedTo?.title };
}
```
Now a Recruiter only gets `editable` when they're the actual assignee (`mine`) — exactly what you described: they can edit rounds they assigned to themselves, not everything.

Make these two edits, save, recompile, hard refresh — test the onshore assign-with-business-unit flow first since that's your immediate blocker.