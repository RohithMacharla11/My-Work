Two real bugs here, and one of them is more serious than the symptom you noticed — worth fixing properly.

**Bug 1 (serious): HR-assigning a Tech/Management round writes to the wrong fields entirely.** `confirmHrAssign()` always computes `targetPrefix` from the Onshore/HR radio (`assignTargetRound`), even when you're assigning Tech Round 1/2 or Management. Since `startHrAssign()` defaults `assignTargetRound` to `'onshoreRound'` for *any* round that isn't literally `'hrRound'`, assigning a Tech Round 1 candidate via HR was actually writing the interviewer into the **Onshore** fields, not Tech Round 1 — that's also why you were seeing the business-unit field appear, since it's gated on `assignTargetRound === 'onshoreRound'`.

**Bug 2: the Onshore/HR radio and business-unit field still show even after Onshore is already decided** (Selected/Rejected), when reassigning should only be possible into HR.

**Fix 1 — `startHrAssign()` in `dashboard.component.ts`:**
```ts
startHrAssign(c: Candidate, round: RoundKey): void {
  this.assignCtx = {
    candidate: c, round,
    groupName: ROUNDS[round].assignGroup,
    needsBusinessUnit: false,
    picked: null,
    businessUnit: null,
  };
  this.assignTargetRound = round === 'hrRound' ? 'hrRound' : 'onshoreRound';
}

onshoreDecided(c: Candidate): boolean {
  return c.onshoreRound.selection === RoundStatus.Selected || c.onshoreRound.selection === RoundStatus.Rejected;
}
```

**Fix 2 — replace `confirmHrAssign()` entirely:**
```ts
confirmHrAssign(): void {
  if (!this.assignCtx?.picked) return;
  const { candidate, round } = this.assignCtx;
  const user = this.assignCtx.picked;
  const businessUnit = this.assignCtx.businessUnit;
  const isOnshoreOrHr = round === 'onshoreRound' || round === 'hrRound';

  const targetPrefix = isOnshoreOrHr
    ? (this.assignTargetRound === 'hrRound' ? ROUNDS['hrRound'].prefix : ROUNDS['onshoreRound'].prefix)
    : ROUNDS[round].prefix;   // Tech1/Tech2/Mgmt now use THEIR OWN prefix

  const fields: Record<string, any> = { [`${targetPrefix}InterviewedById`]: user.id };

  if (isOnshoreOrHr && this.assignTargetRound === 'onshoreRound') {
    if (businessUnit) fields['OnshoreRoundDepartment'] = businessUnit;
    fields['OnShoreRoundInterviewSelection'] = 'Pending';
  }
  const skipToHr = isOnshoreOrHr && this.assignTargetRound === 'hrRound' && !this.onshoreDecided(candidate);

  this.candidates.updateItem(LOCATION_LIST[candidate.location], candidate.id, fields).pipe(
    switchMap(() => skipToHr ? this.candidates.skipOnshoreRound(candidate) : of(void 0))
  ).subscribe({
    next: () => {
      this.assignCtx = null;
      const me = this.currentUser.get();
      if (user.id === me.id) this.navigateToCandidate(candidate);
      else this.reload();
    },
    error: err => { this.error = this.humanError(err); this.assignCtx = null; },
  });
}
```

**Fix 3 — `dashboard.component.html`,** wrap the "Choose which round" radio block with the extra condition so it (and the business-unit field, which lives inside it) only appears when relevant:
```html
<ng-container *ngIf="(assignCtx.round === 'onshoreRound' || assignCtx.round === 'hrRound') && !onshoreDecided(assignCtx.candidate)">
  <!-- existing radio group + business unit field, unchanged -->
</ng-container>
```
Assigning Tech Round 1/2 or Management now falls outside this block entirely — no radio, no business unit, just the plain people-picker. And once Onshore is Selected/Rejected, reassigning always goes straight to HR with no radio shown.

Save, recompile, refresh, then retest: assign a Tech Round 1 candidate via HR (should go to Tech Round 1, no business unit shown) and reassign a completed-Onshore candidate (should skip straight to HR, no radio).