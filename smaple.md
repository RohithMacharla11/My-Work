This lines up exactly with the bug we found earlier — the `StatusCode` Edm.String error is very likely still biting you here, just showing up in a different place: **the UI not refreshing after assignment**, not the assignment itself failing.

**What's actually happening**

Look at `assignToMe()` in `mgmt-round-panel.component.ts`:

```typescript
this.candidates.submitRound(this.candidate, fields).subscribe(() => {
  this.candidate.mgmtRound.interviewedBy = user;
  if (skippingT2) this.candidate.techRound2.selection = RoundStatus.NotApplicable;
  this.candidates.patchStatus(this.candidate).subscribe(() => this.ngOnChanges());
});
```

The local `interviewedBy` mutation happens fine — so the candidate object in memory (and in SharePoint) genuinely does say "assigned to you." But `ngOnChanges()` — the thing that recomputes `this.access` (the object your template reads `state`/`canEdit`/`assignedToName` from) — is **nested inside `patchStatus()`'s success callback**, with no error handler.

If `patchStatus()` throws (the `StatusCode` type-mismatch bug), that inner subscribe's `next` callback never fires — so `ngOnChanges()` never runs. Your `access` object stays exactly as it was *before* you assigned yourself: still showing "assignable"/locked-for-me, even though `interviewedBy` is now correctly set underneath it. That's precisely "locked for others, but I can't edit it either" — the UI simply never refreshed to reflect the assignment that did succeed.

**The fix — two parts**

1. **Confirm `StatusCode` is a true Number column** (from the earlier fix) — if it's still Text, `patchStatus()` will keep silently failing everywhere, not just here.

2. **Decouple the UI refresh from `patchStatus()` success** — `ngOnChanges()` should run regardless of whether the status patch succeeds, since the round data itself already changed locally and in SharePoint:

```typescript
assignToMe(): void {
  const me = this.currentUser.get();
  const user: SharePointUser = { id: me.id, title: me.title, email: me.email };
  const mgmtPrefix = ROUNDS.mgmtRound.prefix;

  const fields: Record<string, any> = {
    [`${mgmtPrefix}InterviewedById`]: me.id,
  };
  const skippingT2 = this.candidate.techRound2.selection === RoundStatus.Pending;
  if (skippingT2) {
    fields[`${ROUNDS.techRound2.prefix}InterviewSelection`] = RoundStatus.NotApplicable;
  }

  this.candidates.submitRound(this.candidate, fields).subscribe({
    next: () => {
      this.candidate.mgmtRound.interviewedBy = user;
      if (skippingT2) this.candidate.techRound2.selection = RoundStatus.NotApplicable;

      this.ngOnChanges();   // FIXED — always refresh access immediately, don't wait on patchStatus

      this.candidates.patchStatus(this.candidate).subscribe({
        next: () => {},   // status column write is secondary; access already reflects reality
        error: err => console.error('patchStatus failed after assignToMe', err),
      });
    },
    error: err => console.error('submitRound failed in assignToMe', err),
  });
}
```

**This same coupling bug needs the identical fix in every other self-assign/unassign flow** — `tech-round-panel.component.ts`'s `assignToMe()`, and `dashboard.component.ts`'s `confirmSelfAssign()` / `confirmHrAssign()` / `confirmUnassign()` — anywhere `ngOnChanges()` (panels) or `reload()` (dashboard) is nested inside `patchStatus()`'s success callback rather than called unconditionally right after the local mutation. Want me to go through those four and rewrite each with the same "mutate → refresh UI → patch status independently" ordering?