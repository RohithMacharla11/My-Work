Now I have everything I need. Here's the complete, exact diff.

## 1. `workflow.service.ts` — add `canEditDecision`

**Interface:**
```ts
export interface RoundAccess {
  state: AccessState;
  canEdit: boolean;
  canEditDecision: boolean;   // NEW — only the actual assigned interviewer can change Selected/Rejected
  assignedToName?: string;
}
```

**`getAccess()` — every return statement gets the new field, `canEditDecision` is simply `mine`:**

```ts
getAccess(c: Candidate, key: RoundKey): RoundAccess {
  const me = this.currentUser.get();

  if (!this.canView(c, key)) return { state: 'no-access', canEdit: false, canEditDecision: false };
  if (key === 'techRound2' && this.isTechRound2Skipped(c))
    return { state: 'skipped', canEdit: false, canEditDecision: false };
  if (key === 'onshoreRound' && this.isOnshoreSkipped(c))
    return { state: 'skipped', canEdit: false, canEditDecision: false };

  if (!this.hasReached(c, key)) return { state: 'not-reached', canEdit: false, canEditDecision: false };

  const locked = this.isRoundLocked(c, key);
  const active = key === 'mgmtRound'
    ? this.hasReached(c, 'mgmtRound') && this.status(c, 'mgmtRound') === S.Pending
    : this.activeRound(c) === key;
  const assignedTo = this.assignee(c, key);
  const mine = !!assignedTo && assignedTo.id === me.id;
  const canEditByRole = this.currentUser.canEditAnything();
  const inPanel = this.editableRounds().includes(key);

  if (active && !locked) {
    if (!assignedTo && (canEditByRole || this.ownsRoundByRole(key))) {
      return { state: 'assignable', canEdit: false, canEditDecision: false };
    }
    if (mine || canEditByRole) {
      return { state: 'editable', canEdit: true, canEditDecision: mine, assignedToName: assignedTo?.title };
    }
  }

  if (this.currentUser.isAdmin()) {
    return { state: 'editable', canEdit: true, canEditDecision: mine, assignedToName: assignedTo?.title };
  }

  return { state: 'readonly', canEdit: false, canEditDecision: false, assignedToName: assignedTo?.title };
}
```

That's the entire service change. Logic: `canEditDecision` is `true` only when the viewer **is** the assigned interviewer (`mine`) — HR Admin editing someone else's round gets `canEdit: true, canEditDecision: false`. If HR Admin happens to also be the assigned interviewer, `mine` is true and they keep full control, same as any other interviewer.

## 2. Each panel template — same 3-part pattern

Replace the existing `*ngIf="access.canEdit"` (or `r1.canEdit`/`r2.canEdit`) decision block with three variants: **editable buttons** (canEditDecision), **highlighted read-only** (canEdit but not canEditDecision), **plain chip** (fully readonly — you already have this one).

**`hr-round-panel.component.html`** — replace the `Decision` field block:
```html
<div class="field">
  <label>Decision <span class="req" *ngIf="access.canEditDecision">*</span></label>

  <div class="selector" *ngIf="access.canEditDecision">
    <button class="selopt y" [class.on]="decision === Sel" (click)="decision = Sel">✓ Selected</button>
    <button class="selopt n" [class.on]="decision === Rej" (click)="decision = Rej">X Rejected</button>
  </div>

  <div class="selector locked-decision" *ngIf="access.canEdit && !access.canEditDecision">
    <button class="selopt y" [class.on]="candidate.hrRound.selection === Sel" disabled>✓ Selected</button>
    <button class="selopt n" [class.on]="candidate.hrRound.selection === Rej" disabled>X Rejected</button>
  </div>
</div>
```

(Leave the existing bottom `*ngIf="!access.canEdit && (...)"` chip block untouched — that already covers fully-readonly viewers.)

**`mgmt-round-panel.component.html`** — replace the decision `<td>`:
```html
<td class="col-round">
  <div class="selector" *ngIf="access.canEditDecision">
    <button class="selopt y" [class.on]="decision === Sel" (click)="decision = Sel">✓ Selected</button>
    <button class="selopt n" [class.on]="decision === Rej" (click)="decision = Rej">X Rejected</button>
  </div>
  <div class="selector locked-decision" *ngIf="access.canEdit && !access.canEditDecision">
    <button class="selopt y" [class.on]="candidate.mgmtRound.selection === Sel" disabled>✓ Selected</button>
    <button class="selopt n" [class.on]="candidate.mgmtRound.selection === Rej" disabled>X Rejected</button>
  </div>
  <ng-container *ngIf="!access.canEdit">
    <span class="chip pass" *ngIf="candidate.mgmtRound.selection === Sel"><i></i>Selected</span>
    <span class="chip rej" *ngIf="candidate.mgmtRound.selection === Rej"><i></i>Rejected</span>
    <span class="dim" *ngIf="candidate.mgmtRound.selection !== Sel && candidate.mgmtRound.selection !== Rej">Pending</span>
  </ng-container>
</td>
```

**`onshore-round-panel.component.html`** — replace the `Result` field:
```html
<div class="field">
  <label>Result <span class="req" *ngIf="access.canEditDecision">*</span></label>

  <div class="selector" *ngIf="access.canEditDecision">
    <button class="selopt y" [class.on]="decision === Sel" (click)="decision = Sel">✓ Selected</button>
    <button class="selopt n" [class.on]="decision === Rej" (click)="decision = Rej">X Rejected</button>
  </div>

  <div class="selector locked-decision" *ngIf="access.canEdit && !access.canEditDecision">
    <button class="selopt y" [class.on]="candidate.onshoreRound.selection === Sel" disabled>✓ Selected</button>
    <button class="selopt n" [class.on]="candidate.onshoreRound.selection === Rej" disabled>X Rejected</button>
  </div>
</div>
```
(Leave that panel's `*ngIf="!access.canEdit && (...Sel||...Rej)"` chip block as-is.)

**`tech-round-panel.component.html`** — this one has two decisions (R1/R2), same pattern twice. R1 block:
```html
<td class="col-round">
  <div class="selector" *ngIf="r1.canEditDecision">
    <button class="selopt neutral-y" [class.on]="r1Decision === Sel" (click)="r1Decision = Sel">✓ Recommended</button>
    <button class="selopt neutral-n" [class.on]="r1Decision === Rej" (click)="r1Decision = Rej">X Not Recommended</button>
  </div>
  <div class="selector locked-decision" *ngIf="r1.canEdit && !r1.canEditDecision">
    <button class="selopt neutral-y" [class.on]="decisionOf('techRound1') === Sel" disabled>✓ Recommended</button>
    <button class="selopt neutral-n" [class.on]="decisionOf('techRound1') === Rej" disabled>X Not Recommended</button>
  </div>
  <ng-container *ngIf="!r1.canEdit">
    <span class="chip neutral" *ngIf="decisionOf('techRound1') === Sel"><i></i>Recommended</span>
    <span class="chip neutral" *ngIf="decisionOf('techRound1') === Rej"><i></i>Not Recommended</span>
    <span class="dim" *ngIf="decisionOf('techRound1') !== Sel && decisionOf('techRound1') !== Rej">Pending</span>
  </ng-container>
</td>
```

R2 block — identical pattern, swap `r1`→`r2`, `r1Decision`→`r2Decision`, `'techRound1'`→`'techRound2'`.

## 3. CSS — highlighted-but-locked look

Add to `round-panel.shared.scss`:
```scss
.locked-decision .selopt {
  cursor: not-allowed;
  opacity: 0.6;
}
.locked-decision .selopt.on {
  opacity: 1;
  box-shadow: 0 0 0 2px currentColor inset;  // visually highlight the current decision without allowing change
}
```

## What you don't need to touch

- **Feedback textareas / comment fields** in every panel already gate on `access.canEdit` — unchanged, HR Admin can still edit them.
- **Submit buttons** already gate on `access.canEdit` — unchanged, HR Admin can still submit (their feedback edits, decision stays whatever it already was in `candidate.xRound.selection` since the disabled buttons never touch `decision`/`r1Decision`/`r2Decision` — but wait: **check this** — each panel's `submit()` sends `decision` (the component's editable field) as the field value. If HR Admin can't edit `decision` via UI, `decision` needs to be pre-seeded from the existing selection so submit doesn't silently overwrite it. Confirm each panel's `ngOnChanges()` already sets e.g. `this.decision = this.candidate.hrRound.selection === Pending ? null : this.candidate.hrRound.selection` — from what I can see in `hr-round-panel.component.ts` it does exactly that (`this.decision = ... === Pending ? null : this.candidate.hrRound.selection`). Since HR Admin can't click the buttons, `decision` stays at that pre-seeded value and submit sends the same decision back unchanged — correct behavior, no data loss.
- **"Submitted by" name** — already rendered via `{{ access.assignedToName }}` in each panel's header-meta block, inside the `editable || readonly` container, so it shows regardless of who's viewing.

One thing to verify on your end after applying: for **HrAdmin** with `mgmtRound`/`onshoreRound`/`hrRound` where `decision` starts `null` (round genuinely still Pending, no `mine` and no prior HR edit) — the Submit button's `[disabled]="!decision || saving"` will correctly stay disabled since HR Admin never gets to set `decision` via the locked buttons in that case, which is the right outcome (nothing to submit yet).