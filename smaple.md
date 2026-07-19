Two more instances of the same root bug — `assignTargetRound` defaults to `'onshoreRound'` for *every* non-HR round (including Management), and the warning banner / business-unit field only check `assignTargetRound`, not whether this is even an Onshore/HR assignment in the first place.

**Fix 1 — `startHrAssign()` in `dashboard.component.ts`,** only set a meaningful target when it's actually relevant:
```ts
startHrAssign(c: Candidate, round: RoundKey): void {
  this.assignCtx = {
    candidate: c, round,
    groupName: ROUNDS[round].assignGroup,
    needsBusinessUnit: false,
    picked: null,
    businessUnit: null,
  };
  this.assignTargetRound = round === 'hrRound' ? 'hrRound'
    : round === 'onshoreRound' ? 'onshoreRound'
    : 'hrRound';   // dummy value for Tech/Mgmt — never read since the ng-container hides these fields for them
}
```

**Fix 2 — `dashboard.component.html`, business-unit field.** Find:
```html
<div class="field" *ngIf="assignTargetRound === 'onshoreRound'">
```
Replace with:
```html
<div class="field" *ngIf="(assignCtx.round === 'onshoreRound' || assignCtx.round === 'hrRound') && assignTargetRound === 'onshoreRound'">
```

**Fix 3 — same file, the warning banner.** Find:
```html
<div *ngIf="assignTargetRound === 'hrRound'" class="warn-banner">
  <b>Notice:</b> Assigning an HR recruiter will automatically skip the On-shore round.
</div>
```
Replace with:
```html
<div *ngIf="(assignCtx.round === 'onshoreRound' || assignCtx.round === 'hrRound') && assignTargetRound === 'hrRound' && !onshoreDecided(assignCtx.candidate)" class="warn-banner">
  <b>Notice:</b> Assigning an HR recruiter will automatically skip the On-shore round.
</div>
```

Also fix `canConfirmHrAssign()` the same way, since it still checks bare `assignTargetRound`:
```ts
get canConfirmHrAssign(): boolean {
  const a = this.assignCtx;
  if (!a || !a.picked) return false;
  const isOnshoreOrHr = a.round === 'onshoreRound' || a.round === 'hrRound';
  return !isOnshoreOrHr || this.assignTargetRound !== 'onshoreRound' || !!a.businessUnit;
}
```

Save, recompile, refresh. Now: HR round assign shows no radio, no warning, no business unit. Management/Tech assign shows no radio, no warning, no business unit. Only Onshore/HR (undecided) shows the radio + conditional warning/business-unit.