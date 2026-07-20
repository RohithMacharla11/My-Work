Good set of catches — these are all real, and #1 and #2 are actually the same root cause, which is worth understanding before you patch it.

## 1 & 2. People-picker showing wrong group + HR assignment saving as "Unassigned" — same bug

In `dashboard.component.html`'s HR-assign modal, the people-picker is bound like this:

```html
<app-people-picker [groups]="ROUNDS[assignCtx.round].assignGroup" (picked)="onPicked($event)"></app-people-picker>
```

But when the candidate's on-shore round hasn't been decided yet, you show a **radio choice** (`assignTargetRound`) letting HR pick *which* round to assign — onshore or HR. The people-picker's `[groups]` binding never looks at that radio value; it's locked to `assignCtx.round` (fixed at the moment the dialog opened) for the entire time the dialog is open. So switching the radio button visually looks like it changes something, but the picker keeps showing whichever group was set initially — that's your bug #1.

Bug #2 is the downstream effect: `confirmHrAssign()`'s `targetPrefix` **does** correctly compute off `assignTargetRound`, but if the picker never actually filtered to the right group, HR could easily end up picking a person from the *wrong* group (e.g. an Onshore panelist while `assignTargetRound` silently says `'hrRound'`) — the field gets written correctly, but to a person who isn't really an HR user, which is why it can look like nothing saved.

**Fix — both the people-picker and the note text need to react live to the radio selection:**

```html
<ng-container *ngIf="(assignCtx.round === 'onshoreRound' || assignCtx.round === 'hrRound') && !onshoreDecided(assignCtx.candidate)">
  <div class="field">
    <label>Assign to which round?</label>
    <div class="radio-group">
      <label class="radio-option">
        <input type="radio" name="targetRound" value="onshoreRound" [(ngModel)]="assignTargetRound">
        <span>On-shore round</span>
      </label>
      <label class="radio-option">
        <input type="radio" name="targetRound" value="hrRound" [(ngModel)]="assignTargetRound">
        <span>HR round</span>
      </label>
    </div>
  </div>
</ng-container>

<div class="field">
  <label>Search user</label>
  <app-people-picker [groups]="ROUNDS[assignTargetRound].assignGroup" (picked)="onPicked($event)"></app-people-picker>
  <p class="modal-hint">
    Only members of <b>{{ ROUNDS[assignTargetRound].assignGroup.join(' or ') }}</b> should be chosen.
  </p>
</div>
```

Key change: **both** the `[groups]` binding and the hint text now read `ROUNDS[assignTargetRound]` instead of `ROUNDS[assignCtx.round]` / the static `assignCtx.groupName`. Since `assignTargetRound` is a plain component property, Angular re-evaluates this on every change detection cycle — so flipping the radio instantly updates which group the picker searches, with no extra code needed in the component.

**Also double check `startHrAssign()`** sets a sane default so the picker isn't empty before the user touches the radio:
```typescript
startHrAssign(c: Candidate, round: RoundKey): void {
  this.assignCtx = { candidate: c, round, groupName: ROUNDS[round].assignGroup, needsBusinessUnit: false, picked: null, businessUnit: null };
  this.assignTargetRound = (round === 'onshoreRound' || round === 'hrRound') ? round : 'hrRound';
}
```

You can delete `groupName` from `AssignContext` and the modal entirely if you want — it's now redundant with the live getter above.

## 3. Toggle behavior — click again to deselect

This needs the same one-line change everywhere a decision/offer button exists. Pattern: instead of always setting the value, flip to `null` if it's already that value.

**`tech-round-panel.component.html`:**
```html
<button class="selopt neutral-y" [class.on]="r1Decision === Sel" (click)="r1Decision = (r1Decision === Sel ? null : Sel)">✓ Recommended</button>
<button class="selopt neutral-n" [class.on]="r1Decision === Rej" (click)="r1Decision = (r1Decision === Rej ? null : Rej)">✕ Not Recommended</button>
<!-- same pattern for r2Decision -->
```

**`mgmt-round-panel.component.html` / `onshore-round-panel.component.html`:**

```html
<button class="selopt y" [class.on]="decision === Sel" (click)="decision = (decision === Sel ? null : Sel)">✓ Selected</button>
<button class="selopt n" [class.on]="decision === Rej" (click)="decision = (decision === Rej ? null : Rej)">✕ Rejected</button>
```

**`hr-round-panel.component.html`** — same for decision, plus offer fields:
```html
<button class="selopt y" [class.on]="decision === Sel" (click)="decision = (decision === Sel ? null : Sel)">✓ Selected</button>
<button class="selopt n" [class.on]="decision === Rej" (click)="decision = (decision === Rej ? null : Rej)">✕ Rejected</button>

<button class="selopt y" [class.on]="offerSent === 'Yes'" (click)="offerSent = (offerSent === 'Yes' ? null : 'Yes')">Yes</button>
<button class="selopt n" [class.on]="offerSent === 'No'" (click)="offerSent = (offerSent === 'No' ? null : 'No')">No</button>

<button class="selopt y" [class.on]="offerAccepted === 'Yes'" (click)="offerAccepted = (offerAccepted === 'Yes' ? null : 'Yes')">Yes</button>
<button class="selopt n" [class.on]="offerAccepted === 'No'" (click)="offerAccepted = (offerAccepted === 'No' ? null : 'No')">No</button>
```

**Component-side note:** your existing `submit()`/`decision`/`offerSent`/`offerAccepted` types are already `RoundStatus | null` / `YesNo` (`'Yes' | 'No' | null`), so this doesn't need any type change — `null` is already a valid, expected state everywhere. The Save button's `[disabled]="!decision || saving"` already correctly re-disables itself the moment a toggle clears back to `null`, so nothing else breaks.

## 4. Hide offer fields until Selected is chosen (hr-round-panel only)

Wrap the offer-sent/offer-accepted block so it only appears once `decision === Sel`:

```html
<div class="field">
  <label>Decision <span class="req">*</span></label>
  <div class="selector">
    <button class="selopt y" [class.on]="decision === Sel" (click)="decision = (decision === Sel ? null : Sel)">✓ Selected</button>
    <button class="selopt n" [class.on]="decision === Rej" (click)="decision = (decision === Rej ? null : Rej)">✕ Rejected</button>
  </div>
</div>

<!-- Offer fields only appear once Selected is chosen -->
<div class="two-col" *ngIf="decision === Sel">
  <div class="field">
    <label>Offer sent?</label>
    <div class="selector" *ngIf="access.canEdit">
      <button class="selopt y" [class.on]="offerSent === 'Yes'" (click)="offerSent = (offerSent === 'Yes' ? null : 'Yes')">Yes</button>
      <button class="selopt n" [class.on]="offerSent === 'No'" (click)="offerSent = (offerSent === 'No' ? null : 'No')">No</button>
    </div>
    <span class="fb" *ngIf="!access.canEdit">{{ candidate.hrRound.offerSent || '–' }}</span>
  </div>
  <div class="field">
    <label>Offer accepted?</label>
    <div class="selector" *ngIf="access.canEdit">
      <button class="selopt y" [class.on]="offerAccepted === 'Yes'" (click)="offerAccepted = (offerAccepted === 'Yes' ? null : 'Yes')">Yes</button>
      <button class="selopt n" [class.on]="offerAccepted === 'No'" (click)="offerAccepted = (offerAccepted === 'No' ? null : 'No')">No</button>
    </div>
    <span class="fb" *ngIf="!access.canEdit">{{ candidate.hrRound.offerAccepted || '–' }}</span>
  </div>
</div>
```

Also worth adding: since offer fields are now hidden when Rejected, clear them in `submit()` so stale values from an earlier Selected→Rejected flip-flop don't silently get saved:
```typescript
submit(): void {
  if (!this.decision || this.saving) return;
  if (this.decision === this.Rej) {
    this.offerSent = null;
    this.offerAccepted = null;
  }
  // ...rest of existing submit logic unchanged
}
```

Want me to also apply the same "hide until Selected" treatment to the notice text below it ("Answering 'Offer accepted' finalizes this round...") so it doesn't render at all while the fields are hidden?