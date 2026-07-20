Confirmed — this is exactly it. Neither branch of `confirmSelfAssign()` calls `patchStatus()` at all. It writes `InterviewedById` (and `NotApplicable` for tech2 in the skip case) via `submitRound`/`assignInterviewer`, then just closes the dialog and navigates — `Status`/`StatusCode` are never touched from this path. That's why the panel (which does call `patchStatus`) eventually works but the dashboard doesn't.

Here's the exact patch — two small additions, nothing else changed:

**Branch 1 — skip-to-Mgmt case (lines 439–443):**

```ts
this.candidates.submitRound(candidate, fields).subscribe({
  next: () => {
    candidate.techRound2.selection = RoundStatus.NotApplicable;
    candidate.mgmtRound.interviewedBy = user;

    this.candidates.patchStatus(candidate).subscribe({
      next: () => {
        candidate.status = WorkflowService.computeStatus(candidate);
        this.confirmCtx = null;
        this.reload();
        this.navigateToCandidate(candidate);
      },
      error: err => {
        this.error = this.humanError(err);
        this.confirmCtx = null;
      },
    });
  },
  error: err => { this.error = this.humanError(err); this.confirmCtx = null; },
});
return;
```

**Branch 2 — plain self-assign case (lines 446–450):**

```ts
this.candidates.assignInterviewer(candidate, ROUNDS[round].prefix, user).subscribe({
  next: () => {
    candidate[round].interviewedBy = user;

    this.candidates.patchStatus(candidate).subscribe({
      next: () => {
        candidate.status = WorkflowService.computeStatus(candidate);
        this.confirmCtx = null;
        this.reload();
        this.navigateToCandidate(candidate);
      },
      error: err => {
        this.error = this.humanError(err);
        this.confirmCtx = null;
      },
    });
  },
  error: err => { this.error = this.humanError(err); this.confirmCtx = null; },
});
```

Notes on what I added and why:
- `patchStatus(candidate)` — the actual missing write; this recalculates and persists `Status`/`StatusCode` in SharePoint.
- `candidate.status = WorkflowService.computeStatus(candidate)` — updates the in-memory object immediately, so if `navigateToCandidate` lands you on the detail page (or if the row is still visible for a moment), it's not stale.
- `this.reload()` — refreshes the dashboard table/count so the row's status column and tab bucket (active/rejected/etc.) reflect reality right away, without needing a manual page refresh.
- Error handling mirrors your existing pattern (`this.error = this.humanError(err)`) rather than a bare `console.error`, consistent with the rest of this method.

This should fully close the loop — both the candidate-detail panel and the dashboard now write status through the same corrected `computeStatus()` you already fixed, and both update their own view of the candidate immediately after.