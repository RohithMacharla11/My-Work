Good catches, both scoped precisely to what you meant.

## Fix 1 — Tech Round 2 picker should only show Tech Interview Panel

You're right — my earlier `pickerGroups()` only stripped "Mgmt Interview Panel" from the picker for the *mandatory* case (Tech 1 rejected). But once you deliberately pick the **"Tech Round 2"** radio option (as opposed to "Management Round"), the intent is unambiguous — you want a Tech reviewer, full stop, regardless of whether Tech 1 was passed or rejected. The "Management Round" radio choice is what should surface Mgmt Interview Panel members — and it already does that correctly on its own, since it maps straight to `ROUNDS.mgmtRound.assignGroup = ['Mgmt Interview Panel']`.

**`dashboard.component.ts`** — simplify the condition so it always applies to the `techRound2` target, not just the mandatory case:

```ts
pickerGroups(): string[] {
  const ctx = this.assignCtx;
  if (!ctx) return [];
  const target = this.assignTargetRound;
  if (target === 'techRound2') {
    return ROUNDS.techRound2.assignGroup.filter(g => g !== 'Mgmt Interview Panel');
  }
  return ROUNDS[target].assignGroup;
}
```
Everything else — the radio itself, `Management Round` target resolution, onshore/HR handling — is untouched.

## Fix 2 — Tech Round 1 should fill the row when Tech Round 2 is hidden

I don't have visibility into your actual `round-panel.shared.scss`, so rather than guess at whatever percentage-width rules are in there (and risk breaking the two-column layout once Tech 2 reappears), I'll use plain HTML `colspan` — it's layout-engine agnostic, works identically whether the table uses fixed or auto layout, and needs zero CSS changes.

**`tech-round-panel.component.html`** — add one attribute binding to each of Tech Round 1's four cells (header, per-skill row, overall-comment row, decision row). Tech Round 2's cells keep their existing `*ngIf="showTechRound2Column"` exactly as before — nothing about them changes.

Header:
```html
<th class="col-round" [attr.colspan]="showTechRound2Column ? 1 : 2">
  <!-- ...unchanged Tech Round 1 header markup... -->
</th>
```

Per-skill row cell:
```html
<td class="col-round" [attr.colspan]="showTechRound2Column ? 1 : 2">
  <!-- ...unchanged R1 comment textarea markup... -->
</td>
```

Overall-comment row cell:
```html
<td class="col-round" [attr.colspan]="showTechRound2Column ? 1 : 2">
  <!-- ...unchanged r1Overall textarea markup... -->
</td>
```

Decision row cell:
```html
<td class="col-round" [attr.colspan]="showTechRound2Column ? 1 : 2">
  <!-- ...unchanged r1Decision selector markup... -->
</td>
```

With `colspan="2"`, Tech Round 1's cell physically occupies the space of both round columns whenever Tech Round 2 is hidden — no gap on the right, no dependence on whatever width rules your SCSS already has. The moment `showTechRound2Column` flips back to `true` (Tech 1 gets decided), `colspan` drops back to `1`, Tech Round 2's `*ngIf` cells re-enter the DOM, and you're back to the exact two-column layout you have today.

I've logged both fixes to memory.

Let me know how it renders — and whether the "Not Recommended" status label is now flipping to `PendingTech2` correctly once you resubmit a rejected Tech Round 1.