Got it — one bug causing both symptoms. `needsBusinessUnit` is calculated **once**, when you first click the assign button, based on whichever round you clicked from — but it never updates when you flip the radio button between "Onshore round" / "HR round" inside the modal. So:

- Click assign from the **HR round row** → `needsBusinessUnit` is set `false` permanently → even if that candidate still needs the Onshore selection, the field never appears.
- Click assign from the **Onshore row** to reassign → `needsBusinessUnit` was `true` at open time, but if you're reassigning an already-HR-skipped candidate, the stored value doesn't line up with what the radio is actually pointing to, so it can show/hide wrong depending on the path taken.

The real fix is to stop storing `needsBusinessUnit` as a snapshot and instead derive it live from whichever radio option (`assignTargetRound`) is currently selected.

**Fix 1 — `dashboard.component.html`,** replace the business-unit field's condition:
```html
<div class="field" *ngIf="assignCtx.needsBusinessUnit">
```
with:
```html
<div class="field" *ngIf="assignTargetRound === 'onshoreRound'">
```

**Fix 2 — same file, `canConfirmHrAssign` check.** In `dashboard.component.ts`, find:
```ts
get canConfirmHrAssign(): boolean {
  const a = this.assignCtx;
  return !!a && !!a.picked && (!a.needsBusinessUnit || !!a.businessUnit);
}
```
Replace with:
```ts
get canConfirmHrAssign(): boolean {
  const a = this.assignCtx;
  if (!a || !a.picked) return false;
  return this.assignTargetRound !== 'onshoreRound' || !!a.businessUnit;
}
```

**Fix 3 — `startHrAssign()`,** you can leave `needsBusinessUnit` in the interface for now (harmless), but it's no longer the source of truth — the template and validation both now read live off `assignTargetRound`, which already updates correctly via `[(ngModel)]` when the radio changes.

That's it — save, recompile, refresh. Now the business-unit field shows/hides in real time as you toggle the Onshore/HR radio, regardless of which row you opened the modal from.