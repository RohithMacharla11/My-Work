This confirms the actual bug: `MgmtRoundPanelComponent.assignToMe()` mutates `candidate.techRound2.selection` directly on the shared candidate object and calls `this.ngOnChanges()` — but that only refreshes the **mgmt panel's own** state. `TechRoundPanelComponent` is a sibling component bound to the same `[candidate]` input; since you're mutating the object in place (not replacing the reference), Angular's `OnChanges` never fires on the tech panel — it only fires when the `@Input` reference itself changes. So the tech panel keeps showing stale "awaiting feedback" until a full page refresh re-fetches the candidate as a brand-new object.

The fix: have each round panel tell the parent when it changed something, and have the parent refresh the whole candidate so all four panels re-sync together.

**1. Add an output to each round panel** (`tech-round-panel.component.ts`, `mgmt-round-panel.component.ts`, `onshore-round-panel.component.ts`, `hr-round-panel.component.ts`):

```ts
@Output() changed = new EventEmitter<void>();
```

**2. Emit it wherever a write succeeds** — in each panel's `submit()` and `assignToMe()` success callbacks. For `mgmt-round-panel.component.ts`, in `assignToMe()`:

```ts
this.candidates.submitRound(this.candidate, fields).subscribe({
  next: () => {
    this.candidate.mgmtRound.interviewedBy = user;
    if (skippingT2) this.candidate.techRound2.selection = RoundStatus.NotApplicable;

    this.ngOnChanges();
    this.changed.emit();   // ← NEW

    this.candidates.patchStatus(this.candidate).subscribe({
      next: () => {},
      error: err => console.error('patchStatus failed after assignToMe', err),
    });
  },
  error: err => console.error('submitRound failed in assignToMe', err),
});
```

Same one-line addition (`this.changed.emit();`) right after `this.ngOnChanges()` in `submit()` for every panel — tech, mgmt, onshore, hr — so any write anywhere refreshes the whole candidate.

**3. Wire it up in `candidate-detail.component.html`:**

```html
<app-tech-round-panel [candidate]="candidate" (changed)="refresh()"></app-tech-round-panel>
<app-mgmt-round-panel [candidate]="candidate" (changed)="refresh()"></app-mgmt-round-panel>
<app-onshore-round-panel [candidate]="candidate" (changed)="refresh()"></app-onshore-round-panel>
<app-hr-round-panel [candidate]="candidate" (changed)="refresh()"></app-hr-round-panel>
```

**4. Add `refresh()` to `candidate-detail.component.ts`** — re-fetch the candidate as a new object, which forces `OnChanges` in every panel since the reference actually changes this time:

```ts
refresh(): void {
  const location = this.route.snapshot.paramMap.get('location') as LocationKey;
  const id = Number(this.route.snapshot.paramMap.get('id'));
  this.candidates.getById(location, id).subscribe({
    next: candidate => {
      if (!candidate) return;
      this.attachSkillLabels(candidate);
    },
  });
}
```

This reuses your existing `attachSkillLabels()` (which already does `this.candidate = candidate; this.loading = false;`), so skill labels stay attached correctly on every refresh too.

Now: assigning from inside the mgmt panel skips Tech Round 2 → emits `changed` → parent re-fetches candidate → new object reference flows into the tech panel → its `ngOnChanges()` fires → shows "N/A" immediately, no manual page refresh needed. Same fix applies automatically to any other cross-panel state changes (e.g. HR round unassign resetting onshore fields) going forward.