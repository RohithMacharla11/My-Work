Two changes: add a Pending button next to Offer Accepted's Yes/No, and gate the Selected decision behind `offerAccepted === 'Yes'`.

## 1. `candidate.model.ts` — allow `'Pending'` for offer-accepted

```ts
export type YesNo = 'Yes' | 'No' | null;
export type OfferAcceptedStatus = 'Yes' | 'No' | 'Pending' | null;   // NEW
```

Then change `HrRound.offerAccepted` to use the new type:
```ts
export interface HrRound extends InterviewRound {
  offerSent: YesNo;
  offerAccepted: OfferAcceptedStatus;   // was YesNo
}
```

## 2. `hr-round-panel.component.ts` — update the field type

```ts
offerAccepted: OfferAcceptedStatus = null;   // was YesNo
```
(update the import to include `OfferAcceptedStatus` alongside `YesNo`)

## 3. `hr-round-panel.component.html` — Offer Accepted block, full replacement

```html
<div class="field" *ngIf="offerSent === 'Yes'">
  <label>Offer accepted?</label>
  <div class="selector" *ngIf="access.canEdit">
    <button class="selopt y" [class.on]="offerAccepted === 'Yes'" (click)="offerAccepted = (offerAccepted === 'Yes' ? null : 'Yes')">Yes</button>
    <button class="selopt n" [class.on]="offerAccepted === 'No'" (click)="offerAccepted = (offerAccepted === 'No' ? null : 'No')">No</button>
    <button class="selopt p" [class.on]="offerAccepted === 'Pending'" (click)="offerAccepted = (offerAccepted === 'Pending' ? null : 'Pending')">Pending</button>
  </div>
  <span class="fb" *ngIf="!access.canEdit">{{ candidate.hrRound.offerAccepted || '—' }}</span>
</div>
```

(Add a small style for the new button in `round-panel.shared.scss` if `.selopt.p` doesn't already exist:)

```scss
.selopt.p.on { background: var(--amber, #d69e00); color: #fff; }
```

## 4. Gate "Selected" decision on `offerAccepted === 'Yes'` — the actual rule

**`hr-round-panel.component.html`** — Decision block, replace with:
```html
<div class="field">
  <label>Decision <span class="req" *ngIf="access.canEditDecision">*</span></label>

  <div class="selector" *ngIf="access.canEditDecision">
    <button
      class="selopt y"
      [class.on]="decision === Sel"
      [disabled]="offerAccepted !== 'Yes'"
      [title]="offerAccepted !== 'Yes' ? 'Offer must be accepted before marking Selected' : ''"
      (click)="decision = Sel">
      ✓ Selected
    </button>
    <button class="selopt n" [class.on]="decision === Rej" (click)="decision = Rej">X Rejected</button>
  </div>

  <div class="selector locked-decision" *ngIf="access.canEdit && !access.canEditDecision">
    <button class="selopt y" [class.on]="candidate.hrRound.selection === Sel" disabled>✓ Selected</button>
    <button class="selopt n" [class.on]="candidate.hrRound.selection === Rej" disabled>X Rejected</button>
  </div>

  <p class="dim small" *ngIf="access.canEditDecision && offerAccepted !== 'Yes'">
    Selected is disabled until the candidate accepts the offer.
  </p>
</div>
```

## 5. `hr-round-panel.component.ts` — `submit()` safety check

Even with the button disabled, add a guard in `submit()` in case `decision` was set to `Sel` before offer status changed:

```ts
submit(adminMode = false): void {
  if (this.saving) return;
  if (!adminMode && (!this.decision || !this.access.canEditDecision)) return;
  if (!adminMode && this.decision === this.Sel && this.offerAccepted !== 'Yes') return;   // NEW guard

  // ...rest unchanged
}
```

That's it — Pending is now a real third state for Offer Accepted (matches your `YesNo`-based pattern elsewhere but scoped only to this field via the new `OfferAcceptedStatus` type), and the Selected decision button is disabled with an explanatory note until the offer is actually accepted.