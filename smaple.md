Let's fix the redirect first since that's unambiguous, then narrow down the tech panel issue with a specific check.

## 1. Stop redirecting to dashboard — show inline "Saved" instead

Right now every `submit()` (all four panels) ends with `this.router.navigate(['/success'])`, which bounces through `RedirectComponent` back to the dashboard. Replace that with a local saved-flag that shows a message in place.

**Add to each panel's `.ts`** (tech/mgmt/onshore/hr — same one-liner added near `saving`):
```ts
saving = false;
justSaved = false;   // NEW
```

**In every `submit()`'s final success callback**, replace:
```ts
this.saving = false;
this.ngOnChanges();
this.changed.emit();
this.router.navigate(['/success']);
```
with:
```ts
this.saving = false;
this.justSaved = true;
this.ngOnChanges();
this.changed.emit();
setTimeout(() => { this.justSaved = false; }, 3000);
```

This applies to **every** `patchStatus(...).subscribe({ next: ... })` block across all four panels/rounds — hr, mgmt, onshore, tech(R1), tech(R2). Same three-line swap each time: drop the `router.navigate`, set `justSaved = true`, clear it after a few seconds.

**Template — add a small confirmation banner** (put near each panel's action buttons, e.g. `mgmt-round-panel.component.html`):
```html
<div class="chip pass" *ngIf="justSaved" style="margin-top:6px">
  <i></i>Saved
</div>
```
Same snippet in `hr-round-panel.component.html`, `onshore-round-panel.component.html`, and `tech-round-panel.component.html` (once per round, or one shared flag if you'd rather show it once for the whole tech card — your call).

That fully removes the forced navigation — user stays on the candidate detail page, sees "Saved" for 3 seconds, done.

## 2. Tech panel admin edit not appearing — need one thing from you

This is very likely the **`r1Decision`/`r2Decision` swap bug** I flagged earlier (screenshot showed the R2 decision buttons still wired to `r1Decision` instead of `r2Decision`) — if that swap wasn't applied yet, it can cascade into confusing behavior on that panel specifically, since R2's decision state never updates correctly.

Can you confirm: did you apply this specific change to `tech-round-panel.component.html` (around the R2 decision block)?
```html
<div class="selector" *ngIf="r2.canEditDecision">
  <button class="selopt neutral-y" [class.on]="r2Decision === Sel" (click)="r2Decision = Sel">✓ Recommended</button>
  <button class="selopt neutral-n" [class.on]="r2Decision === Rej" (click)="r2Decision = Rej">X Not Recommended</button>
</div>
```

(swapping `r1Decision` → `r2Decision` on both the `[class.on]` and `(click)` bindings)

And separately — when you say "can't see the editing," do you mean:
- (a) The feedback **textareas** don't appear at all for HR Admin on the tech panel, or
- (b) The **"Save changes (Admin)"** button specifically doesn't appear, or
- (c) Something appears but clicking it does nothing?

Tell me which of those three, and whether the R1/R2 swap fix is in — that'll tell me exactly which line is still wrong rather than me guessing at a third patch.