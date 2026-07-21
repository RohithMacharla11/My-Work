Good — and noted: I will **not** touch `pipeCells()`, `getNodeState()`, `getLineState()`, or the dashboard table's pipeline dots. That stays exactly as it is; my earlier suggestion is withdrawn.

## Fix: hide the Tech Round 2 column in the candidate-detail feedback table until Tech 1 is decided

This is entirely inside `tech-round-panel.component.html` and `tech-round-panel.component.ts` — the same file you saw the skill/feedback grid in on the candidate-detail page. It's a completely separate template from the dashboard, so this change can't touch the pipeline dots you want left alone.

**`tech-round-panel.component.ts`** — add one small getter:
```ts
get showTechRound2Column(): boolean {
  return this.candidate.techRound1.selection !== RoundStatus.Pending;
}
```
(`RoundStatus` is already imported in this file, so no new import needed.)

**`tech-round-panel.component.html`** — add `*ngIf="showTechRound2Column"` to the Tech Round 2 header cell and to its three matching body cells (skill rows, overall-comment row, decision row). Nothing about the Tech Round 1 column, or anything inside it, changes.

Header:
```html
<th class="col-round" *ngIf="showTechRound2Column">
  <div class="round-head">
    <b>Tech Round 2</b>
    <!-- ...unchanged chips/state markup... -->
  </div>
  <!-- ...unchanged small/date markup... -->
</th>
```

Per-skill row (inside `*ngFor="let s of candidate.skills; let i = index"`):
```html
<td class="col-round" *ngIf="showTechRound2Column">
  <!-- ...unchanged R2 comment textarea / fb / dim markup... -->
</td>
```

Overall-comment row:
```html
<td class="col-round" *ngIf="showTechRound2Column">
  <!-- ...unchanged r2Overall textarea / feedback markup... -->
</td>
```

Decision row:
```html
<td class="col-round" *ngIf="showTechRound2Column">
  <!-- ...unchanged r2Decision selector markup... -->
</td>
```

Once `techRound1.selection` moves to `Selected` (recommended) or `Rejected` (not recommended) — i.e. exactly when the status becomes `PendingTech2OrMgmt` or the new `PendingTech2` — the column reappears automatically and both tables show side by side, same as today. While it's `Pending`, only the Tech Round 1 column renders, so it naturally takes the full row.

The "assign CTA" footer block (Tech Round 2 "unassigned, assign to me" notice) further down the same file is unaffected — it's already independently gated by `r2.state === 'assignable'`, which can't be true while Tech 1 hasn't been decided anyway (Tech Round 2 isn't reachable yet at that point).

## On the status still showing "Pending with Tech Round 2 or Management Round" after a rejection

This is the exact behavior the `PendingTech2` split was meant to fix, so if you're still seeing the old combined label after rejecting Tech Round 1, it most likely means the `computeStatus()` edit from last time hasn't landed in your actual file yet (I can only hand you the diff — I don't have write access to your repo). Two things worth double-checking on your end:

1. In `workflow.service.ts`, confirm `computeStatus()` now has:
   ```ts
   if (tech2 === 'Pending') {
     return c.techRound1.selection === 'Rejected'
       ? WorkflowStatus.PendingTech2
       : WorkflowStatus.PendingTech2OrMgmt;
   }
   ```
2. Remember this only takes effect **at the moment a round is submitted** — `patchStatus()` (which calls `computeStatus()`) only runs right after a `submitRound()` call, not continuously. So if you're looking at a candidate who was already sitting at "PendingTech2OrMgmt" from before you rejected them, the label only refreshes once you submit Tech Round 1 as Not Recommended (or any other round) again for that candidate — it won't silently update on its own for existing untouched records.

If you've confirmed both of those and it's still showing the old label, tell me and I'll dig into whether something else is reading/caching the status separately.