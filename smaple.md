Good — this one's simpler and doesn't cross role/query boundaries, so it's a clean, contained change. Let me confirm what I checked first: **the HR round panel has no "overall comment" field at all** (its state is just `decision`, `offerSent`, `offerAccepted`) — so nothing to change there. The other four fields you want mandatory live in Tech Round 1, Tech Round 2, Management Round, and Onshore Round.

Also worth noting: the Onshore panel's "Overall feedback" label already *shows* a required asterisk (`<span class="req" *ngIf="access.canEdit">*</span>`) — but nothing was actually enforcing it before submission. So part of this fix is making that visual promise real, not just adding it fresh everywhere.

## Tech Round 1 & 2 — `tech-round-panel.component.ts`

`submit()` currently only blocks on a missing decision. Add the overall-comment check (the `overall` variable already exists a few lines down — just hoist it above the guard):

```ts
submit(round: TechKey, adminMode = false): void {
  const access = this.access(round);
  const decision = round === 'techRound1' ? this.r1Decision : this.r2Decision;
  const overall = round === 'techRound1' ? this.r1Overall : this.r2Overall;

  if (!adminMode && (!decision || !access.canEditDecision || !overall.trim())) return;
  if (this.saving) return;

  const comments = round === 'techRound1' ? this.r1Comments : this.r2Comments;
  // ...rest of the method unchanged (no more `const overall = ...` line lower down — it's already declared above now)
}
```

**`tech-round-panel.component.html`** — submit buttons and the overall label:
```html
<button class="btn primary" *ngIf="r1.canEditDecision" [disabled]="!r1Decision || !r1Overall.trim() || saving" (click)="submit('techRound1')">Submit Tech Round 1</button>
<button class="btn primary" *ngIf="r2.canEditDecision" [disabled]="!r2Decision || !r2Overall.trim() || saving" (click)="submit('techRound2')">Submit Tech Round 2</button>
```

```html
<td class="col-skill"><b>Overall comment <span class="req">*</span></b></td>
```
The **admin "Save (Admin)" buttons stay exactly as they are** (`[disabled]="saving"` only) — same as how they already bypass the decision requirement, admin overrides intentionally aren't blocked by mandatory-field checks.

## Onshore Round — `onshore-round-panel.component.ts`

```ts
submit(adminMode = false): void {
  if (this.saving) return;
  if (!adminMode && (!this.decision || !this.access.canEditDecision || !this.overall.trim())) return;
  // ...rest unchanged
}
```
**`onshore-round-panel.component.html`** — just the submit button (the required asterisk on the label is already there):
```html
<button class="btn primary" *ngIf="access.canEditDecision" [disabled]="!decision || !overall.trim() || saving" (click)="submit()">Submit round</button>
```

## Management Round — `mgmt-round-panel.component.ts`

This is the one with the extra requirement — every skill/topic row (Justification, Constraints, Potential Opportunities, Points of Attention, etc.) needs a comment, not just the overall field:

```ts
get allTopicsFilled(): boolean {
  return this.rows.every(r => !!r.comment && r.comment.trim().length > 0);
}

submit(adminMode = false): void {
  if (this.saving) return;
  if (!adminMode && (!this.decision || !this.access.canEditDecision || !this.overall.trim() || !this.allTopicsFilled)) return;
  // ...rest unchanged
}
```

**`mgmt-round-panel.component.html`** — submit button, plus asterisks on each topic row and the overall row:
```html
<button class="btn primary" *ngIf="access.canEditDecision" [disabled]="!decision || !overall.trim() || !allTopicsFilled || saving" (click)="submit()">Submit round</button>
```

```html
<td class="col-skill"><b>{{ row.label }} <span class="req">*</span></b></td>
```
```html
<td class="col-skill"><b>Overall comment <span class="req">*</span></b></td>
```

One honest caveat: I've seen Management round's per-skill row markup and its submit button precisely, but I haven't been shown the exact HTML around its "Overall comment" row specifically — I've written that line matching the identical pattern the other three panels use. If it turns out to be structured differently in your actual file, send me that snippet and I'll match it exactly rather than you having to adapt my guess.

Everything above only touches `submit()` guards and `[disabled]` bindings — no changes to `getAccess()`, assignment logic, the OnshorePanel visibility fix, or anything from earlier in this conversation. Ready for the CSS whenever you are.

Whenever you're ready for the CSS, send over `round-panel.shared.scss` (or the relevant `.col-round`/`.col-score` rule) and we'll finish the Tech Round column width fix.