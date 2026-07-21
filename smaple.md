I went through the assignment flow end-to-end — `activeRound()`, `getAccess()`, `getDashboardAction()`, `startHrAssign()`/`confirmHrAssign()` in `dashboard.component.ts`, and the modal HTML — and found the exact causes for all three things you described. They're related but distinct bugs. Let me walk through each, then give you the isolated fixes.

## Root cause #1: the people-picker group bug

In `dashboard.component.ts`, `startHrAssign()` currently does this:

```ts
startHrAssign(c: Candidate, round: RoundKey): void {
  this.assignCtx = { candidate: c, round, groupName: ROUNDS[round].assignGroup, needsBusinessUnit: false, picked: null, businessUnit: null };
  this.assignTargetRound = (round === 'onshoreRound' || round === 'hrRound') ? round : 'hrRound';
}
```

Look at the last line: for **any round that isn't onshore or HR** — Tech Round 1, Tech Round 2, Management — `assignTargetRound` gets forced to `'hrRound'` regardless of what was actually clicked. The modal's people-picker is bound to `[groups]="ROUNDS[assignTargetRound].assignGroup"`, so it always ends up showing `ROUNDS.hrRound.assignGroup` = HR Cohort Team / HR Recruiters — no matter which round you were actually trying to assign. That's exactly your symptom. Your `ROUNDS` config in `cohort.config.ts` already has the right group per round (Tech Interview Panel, Mgmt Interview Panel, etc.) — this was purely a bug in what gets *selected* from that config.

## Root cause #2: no radio choice for Tech2/Management

The onshore/HR radio-button pattern exists only for `onshoreRound`/`hrRound` — there's no equivalent block for `techRound2`/`mgmtRound`, so HR was never offered the "which round?" choice you want there.

## Root cause #3: the mandatory-Tech-2 leak

`ROUNDS.techRound2.selfAssign = ['TechPanel', 'MgmtPanel']` — Management is allowed to self-assign Tech Round 2 as an intentional shortcut ("claim Tech 2, skip it, take Management directly"). But this is only supposed to apply when Tech Round 1 **was** recommended (Management is a legitimate alternative). When Tech Round 1 is rejected, Tech Round 2 becomes mandatory, and the code never checks that distinction — so the self-assign icon still shows for Management on the dashboard row in that case. Also, since there's currently no separate status for "Tech1 rejected → Tech2 mandatory" vs. "Tech1 passed → Tech2-or-Mgmt", we have to add that first.

---

## Fix 1 — new status: `PendingTech2` (mandatory, distinct from `PendingTech2OrMgmt`)

**`cohort.config.ts`** — add one enum value and append one status code (append, never renumber — existing SharePoint records already store the old codes):

```ts
export enum WorkflowStatus {
  Rejected = 'Rejected',
  PendingTech1 = 'Pending with Tech Round 1',
  PendingTech2OrMgmt = 'Pending with Tech Round 2 or Management Round',
  PendingMgmt = 'Pending with Management Round',
  PendingOnshoreOrHR = 'Pending with Onshore Round or HR Round',
  PendingOnshore = 'Pending with Onshore',
  PendingHR = 'Pending with HR',
  PendingTech2 = 'Pending with Tech Round 2',   // NEW
}

export const STATUS_CODE: Record<WorkflowStatus, number> = {
  [WorkflowStatus.Rejected]: 0,
  [WorkflowStatus.PendingTech1]: 1,
  [WorkflowStatus.PendingTech2OrMgmt]: 2,
  [WorkflowStatus.PendingMgmt]: 3,
  [WorkflowStatus.PendingOnshoreOrHR]: 4,
  [WorkflowStatus.PendingOnshore]: 5,
  [WorkflowStatus.PendingHR]: 6,
  [WorkflowStatus.PendingTech2]: 7,             // NEW — appended, old codes untouched
};
```

`CODE_TO_STATUS` is auto-derived from `STATUS_CODE` — nothing to change there.

**`ROLE_STATUS_MAP`** (same file) — add the new status into every tier that already carries `PendingTech2OrMgmt`, so dashboards keep showing these candidates to the right roles:

```ts
export const ROLE_STATUS_MAP: Record<UserRole, WorkflowStatus[]> = {
  TechPanel: [
    WorkflowStatus.PendingTech1,
    WorkflowStatus.PendingTech2,          // NEW
    WorkflowStatus.PendingTech2OrMgmt,
  ],
  MgmtPanel: [
    WorkflowStatus.PendingTech1,
    WorkflowStatus.PendingTech2,          // NEW
    WorkflowStatus.PendingTech2OrMgmt,
    WorkflowStatus.PendingMgmt,
  ],
  OnshorePanel: [
    WorkflowStatus.PendingTech1,
    WorkflowStatus.PendingTech2,          // NEW
    WorkflowStatus.PendingTech2OrMgmt,
    WorkflowStatus.PendingMgmt,
    WorkflowStatus.PendingOnshoreOrHR,
    WorkflowStatus.PendingOnshore,
  ],
  // HrAdmin / Recruiter: unchanged — Object.values(WorkflowStatus) already includes it automatically
};
```

**`workflow.service.ts`** — `computeStatus()`: only the one `if (tech2 === 'Pending')` line changes; every other branch (tech2 Rejected/Selected, management, onshore, HR) is untouched:

```ts
const tech2 = c.techRound2.selection;
if (tech2 === 'Pending') {
  // Tech Round 1 not recommended -> Tech Round 2 is mandatory, no Management alternative yet.
  return c.techRound1.selection === 'Rejected'
    ? WorkflowStatus.PendingTech2
    : WorkflowStatus.PendingTech2OrMgmt;
}
if (tech2 === 'Rejected') return WorkflowStatus.Rejected;
// ...everything below this is unchanged
```

Also add the new status to `STATUS_MIN_ROLE` inside `statusAllowedForRoles()`, same file:
```ts
const STATUS_MIN_ROLE: Record<WorkflowStatus, UserRole> = {
  [WorkflowStatus.Rejected]: 'HrAdmin',
  [WorkflowStatus.PendingTech1]: 'TechPanel',
  [WorkflowStatus.PendingTech2]: 'TechPanel',          // NEW
  [WorkflowStatus.PendingTech2OrMgmt]: 'TechPanel',
  [WorkflowStatus.PendingMgmt]: 'MgmtPanel',
  [WorkflowStatus.PendingOnshoreOrHR]: 'MgmtPanel',
  [WorkflowStatus.PendingOnshore]: 'OnshorePanel',
  [WorkflowStatus.PendingHR]: 'HrAdmin',
};
```

I checked every other consumer of `WorkflowStatus` — `getDetailedStatusLabel()`, `pipeStage()`/`getNodeState()` in the dashboard, `hasReached()`, `activeRound()` — none of them read the enum directly; they all independently inspect the raw round `.selection` fields. So they need **zero changes** and keep working exactly as before.

## Fix 2 — Tech Round 2 becomes Tech-only when Tech Round 1 was rejected

**`workflow.service.ts`** — `ownsRoundByRole()` gets a candidate parameter and one filter line:

```ts
private ownsRoundByRole(c: Candidate, key: RoundKey): boolean {
  let selfAssignRoles = ROUNDS[key].selfAssign;
  // Tech Round 2 is Tech-only when Tech Round 1 wasn't recommended — Management
  // may only self-assign it as the optional "skip to Management" shortcut,
  // which only applies once Tech Round 1 WAS recommended.
  if (key === 'techRound2' && this.status(c, 'techRound1') === S.Rejected) {
    selfAssignRoles = selfAssignRoles.filter(r => r !== 'MgmtPanel');
  }
  return selfAssignRoles.length
    ? selfAssignRoles.some(r => this.currentUser.hasRole(r))
    : this.isHRUser();
}
```

One call site to update, inside `getAccess()`:
```ts
if (!assignedTo && (canEditByRole || this.ownsRoundByRole(c, key))) {   // was: this.ownsRoundByRole(key)
```

And in `getDashboardAction()`, reuse the same helper instead of the raw duplicate check (this is what drives the dashboard row's self-assign icon):
```ts
if (!assignedTo && !this.isHRUser()) {
  base.canSelfAssign = this.ownsRoundByRole(c, active);   // was: ROUNDS[active].selfAssign.some(r => this.currentUser.hasRole(r))
}
```

I verified this is byte-for-byte equivalent to the old behavior for every round *except* the one case being fixed — onshore/HR (empty `selfAssign` arrays) fall back to `isHRUser()` in both old and new code and resolve identically inside this already-`!isHRUser()`-gated branch. `TechRoundPanelComponent.canSelfAssignR2()` (the Tech panel's own inline "assign to me" button) was **already** hardcoded to `TechPanel`-only, so it needs no change — this fix only closes the gap on the dashboard row icon and the `getAccess()`-driven state.

## Fix 3 — radio buttons + correct people-picker group for Tech2/Management

**`dashboard.component.ts`**, field declaration — widen the type (safe superset, `RoundKey` already includes the two existing values):
```ts
assignTargetRound: RoundKey = 'onshoreRound';   // was: 'onshoreRound' | 'hrRound'
```

`startHrAssign()` — remove the bad default entirely, just default to the round that was actually clicked:
```ts
startHrAssign(c: Candidate, round: RoundKey): void {
  this.assignCtx = { candidate: c, round, groupName: ROUNDS[round].assignGroup, needsBusinessUnit: false, picked: null, businessUnit: null };
  this.assignTargetRound = round;   // was the buggy ternary forcing 'hrRound'
}
```

Add one small helper for the mandatory-case picker scoping (additive only, doesn't touch `ROUNDS` config or anything else that reads it):
```ts
pickerGroups(): string[] {
  const ctx = this.assignCtx;
  if (!ctx) return [];
  const target = this.assignTargetRound;
  if (target === 'techRound2' && ctx.candidate.techRound1.selection === 'Rejected') {
    return ROUNDS.techRound2.assignGroup.filter(g => g !== 'Mgmt Interview Panel');
  }
  return ROUNDS[target].assignGroup;
}
```

`confirmHrAssign()` — generalize to use `assignTargetRound` (the actually-chosen target) instead of the original `round` for prefix resolution, and add the Tech2-skip-on-direct-Management-assign behavior you described:
```ts
confirmHrAssign(): void {
  if (!this.assignCtx?.picked) return;
  const { candidate } = this.assignCtx;
  const user = this.assignCtx.picked;
  const businessUnit = this.assignCtx.businessUnit;
  const target = this.assignTargetRound;

  const targetPrefix = ROUNDS[target].prefix;
  const fields: Record<string, any> = { [`${targetPrefix}InterviewedById`]: user.id };

  if (target === 'onshoreRound') {
    if (businessUnit) fields['OnshoreRoundDepartment'] = businessUnit;
    fields['OnShoreRoundInterviewSelection'] = 'Pending';
  }

  if (target === 'mgmtRound' && candidate.techRound2.selection === RoundStatus.Pending) {
    // Assigning Management directly skips Tech Round 2, same as the existing self-assign shortcut.
    fields[`${ROUNDS.techRound2.prefix}InterviewSelection`] = RoundStatus.NotApplicable;
  }

  const skipToHr = target === 'hrRound' && !this.onshoreDecided(candidate);

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
I traced this carefully: for onshore/HR, `ROUNDS[this.assignTargetRound].prefix` produces **exactly** the same prefix the old ternary produced in every case — this is a safe generalization, not a behavior change, for those two rounds.

**`dashboard.component.html`** — two small template edits inside the HR-assign modal:

1. Fix the people-picker binding (was `ROUNDS[assignTargetRound].assignGroup`, now uses the new scoped helper):
```html
<app-people-picker [groups]="pickerGroups()" (picked)="onPicked($event)"></app-people-picker>
<p class="modal-hint">Only members of <b>{{ pickerGroups().join(' or ') }}</b> should be chosen.</p>
```

2. Add the new radio block, placed right alongside the existing onshore/HR one (that block stays completely untouched):
```html
<ng-container *ngIf="assignCtx.round === 'techRound2' && assignCtx.candidate.techRound1.selection === 'Selected'">
  <div class="field">
    <label>Assign to which round?</label>
    <div class="radio-group">
      <label class="radio-option">
        <input type="radio" name="targetRound2" value="techRound2" [(ngModel)]="assignTargetRound">
        <span>Tech Round 2</span>
      </label>
      <label class="radio-option">
        <input type="radio" name="targetRound2" value="mgmtRound" [(ngModel)]="assignTargetRound">
        <span>Management Round</span>
      </label>
    </div>
  </div>
  <div *ngIf="assignTargetRound === 'mgmtRound'" class="warn-banner">
    <b>Notice:</b> Assigning Management directly will automatically skip Tech Round 2.
  </div>
</ng-container>
```

The `*ngIf` condition is the key piece: it only shows when `techRound1.selection === 'Selected'`, which is precisely the new `PendingTech2OrMgmt` case. When Tech 1 was rejected (`PendingTech2`, mandatory), this block simply never renders — no radio, no Management option, single target Tech Round 2, and (per Fix 2 above) `pickerGroups()` also excludes Mgmt Interview Panel members from the picker in that state.

---

### What stays completely untouched
Onshore/HR round assignment (radio, banner, business-unit field, `canConfirmHrAssign`), every panel's own `submit()`, `assignToMe()`, unassign flows, `hasReached()`, `activeRound()`, `isVisibleFor()`, the pipeline visualization, and all four round-panel components. I confirmed each of these either doesn't touch the code paths above or produces byte-identical output for every case except the three you asked to fix.

I've saved this round of fixes to memory so I don't lose track between sessions.

Let me know how testing goes, or send the next refinement whenever you're ready.