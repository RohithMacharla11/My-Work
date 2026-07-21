Now that's a clean, unambiguous rule: `submit(round)` (no admin flag / default `false`) → keeps redirecting. `submit(round, true)` (Save Changes admin path) → no redirect, show inline confirmation instead. Same success handler, branch only at the very end based on `adminMode`.

## `tech-round-panel.component.ts` — replace just the `submit()` method

```ts
justSaved: TechKey | null = null;   // NEW

submit(round: TechKey, adminMode = false): void {
  const access = this.access(round);
  const decision = round === 'techRound1' ? this.r1Decision : this.r2Decision;
  if (!adminMode && (!decision || !access.canEditDecision)) return;
  if (this.saving) return;

  const comments = round === 'techRound1' ? this.r1Comments : this.r2Comments;
  const overall = round === 'techRound1' ? this.r1Overall : this.r2Overall;
  const prefix = ROUNDS[round].prefix;
  const me = this.currentUser.get();

  const fields: Record<string, any> = {
    [`${prefix}InterviewFeedback`]: overall,
    [`${prefix}InterviewDate`]: new Date().toISOString(),
  };
  if (decision !== null) fields[`${prefix}InterviewSelection`] = decision;
  if (!adminMode) fields[`${prefix}InterviewedById`] = me.id;
  this.candidate.skills.forEach((s, i) => {
    fields[techCommentCol(s.slotIndex, round)] = comments[i];
  });

  let newTrail = this.candidate.auditTrail ?? '';
  if (adminMode) {
    newTrail = appendAuditEntry(newTrail, {
      name: me.title,
      date: new Date().toISOString(),
      component: ROUNDS[round].name,
    });
    fields[AUDIT_TRAIL_COL] = newTrail;
  }

  this.saving = true;
  this.candidates.submitRound(this.candidate, fields).subscribe({
    next: () => {
      if (decision !== null) this.candidate[round].selection = decision as RoundStatus;
      this.candidate[round].feedback = overall;
      this.candidate.skills.forEach((s, i) => {
        if (round === 'techRound1') s.round1Comment = comments[i];
        else s.round2Comment = comments[i];
      });
      if (adminMode) this.candidate.auditTrail = newTrail;

      this.candidates.patchStatus(this.candidate).subscribe({
        next: () => {
          this.saving = false;
          this.ngOnChanges();
          this.changed.emit();

          if (adminMode) {
            this.justSaved = round;
            setTimeout(() => { this.justSaved = null; }, 3000);
          } else {
            this.router.navigate(['/success']);
          }
        },
        error: () => { this.saving = false; },
      });
    },
    error: () => { this.saving = false; },
  });
}
```

## `tech-round-panel.component.html` — add confirmation chips to the actions footer

```html
<div class="actions">
  <button class="btn primary" *ngIf="r1.canEditDecision" [disabled]="!r1Decision || saving" (click)="submit('techRound1')">Submit Tech Round 1</button>
  <button class="btn primary" *ngIf="r1.canEdit && !r1.canEditDecision" [disabled]="saving" (click)="submit('techRound1', true)">Save Tech Round 1 (Admin)</button>
  <span class="chip pass" *ngIf="justSaved === 'techRound1'"><i></i>Saved</span>

  <button class="btn primary" *ngIf="r2.canEditDecision" [disabled]="!r2Decision || saving" (click)="submit('techRound2')">Submit Tech Round 2</button>
  <button class="btn primary" *ngIf="r2.canEdit && !r2.canEditDecision" [disabled]="saving" (click)="submit('techRound2', true)">Save Tech Round 2 (Admin)</button>
  <span class="chip pass" *ngIf="justSaved === 'techRound2'"><i></i>Saved</span>
</div>
<div class="dim small" *ngIf="lastAdminEdit('techRound1') as edit" style="margin-top:6px">
  Tech Round 1 last edited by {{ edit.name }} · {{ formatFriendlyDate(edit.date) }}
</div>
<div class="dim small" *ngIf="lastAdminEdit('techRound2') as edit" style="margin-top:2px">
  Tech Round 2 last edited by {{ edit.name }} · {{ formatFriendlyDate(edit.date) }}
</div>
```

## Same pattern for `mgmt-round-panel.component.ts` / `.html`

```ts
justSaved = false;   // NEW

submit(adminMode = false): void {
  if (this.saving) return;
  if (!adminMode && (!this.decision || !this.access.canEditDecision)) return;

  const me = this.currentUser.get();
  const prefix = ROUNDS.mgmtRound.prefix;

  const fields: Record<string, any> = {
    [MGMT_OVERALL_COL]: this.overall,
    [`${prefix}InterviewDate`]: new Date().toISOString(),
  };
  if (this.decision !== null) fields[`${prefix}InterviewSelection`] = this.decision;
  if (!adminMode) fields[`${prefix}InterviewedById`] = me.id;
  this.rows.forEach((row, i) => { fields[MGMT_SKILL_COLS[i]] = row.comment; });

  let newTrail = this.candidate.auditTrail ?? '';
  if (adminMode) {
    newTrail = appendAuditEntry(newTrail, { name: me.title, date: new Date().toISOString(), component: ROUNDS.mgmtRound.name });
    fields[AUDIT_TRAIL_COL] = newTrail;
  }

  this.saving = true;
  this.candidates.submitRound(this.candidate, fields).subscribe({
    next: () => {
      if (this.decision !== null) this.candidate.mgmtRound.selection = this.decision as RoundStatus;
      this.candidate.mgmtRound.feedback = this.overall;
      this.rows.forEach((row, i) => { this.candidate.managementComments[i] = row.comment; });
      if (adminMode) this.candidate.auditTrail = newTrail;

      this.candidates.patchStatus(this.candidate).subscribe({
        next: () => {
          this.saving = false;
          this.ngOnChanges();
          this.changed.emit();
          if (adminMode) {
            this.justSaved = true;
            setTimeout(() => { this.justSaved = false; }, 3000);
          } else {
            this.router.navigate(['/success']);
          }
        },
        error: () => { this.saving = false; },
      });
    },
    error: () => { this.saving = false; },
  });
}
```

```html
<div class="editfoot" *ngIf="access.canEdit">
  <div class="actions">
    <button class="btn primary" *ngIf="access.canEditDecision" [disabled]="!decision || saving" (click)="submit()">Submit round</button>
    <button class="btn primary" *ngIf="access.canEdit && !access.canEditDecision" [disabled]="saving" (click)="submit(true)">Save changes (Admin)</button>
    <span class="chip pass" *ngIf="justSaved"><i></i>Saved</span>
  </div>
  <div class="dim small" *ngIf="lastAdminEdit() as edit" style="margin-top:6px">
    Last edited by {{ edit.name }} · {{ formatDate(edit.date) }}
  </div>
</div>
```

## Same pattern for `onshore-round-panel.component.ts` / `.html`

```ts
justSaved = false;   // NEW

submit(adminMode = false): void {
  if (this.saving) return;
  if (!adminMode && (!this.decision || !this.access.canEditDecision)) return;

  const me = this.currentUser.get();
  const prefix = ROUNDS.onshoreRound.prefix;

  const fields: Record<string, any> = {
    [`${prefix}InterviewFeedback`]: this.overall,
    [`${prefix}InterviewDate`]: new Date().toISOString(),
  };
  if (this.decision !== null) fields[`${prefix}InterviewSelection`] = this.decision;
  if (!adminMode) fields[`${prefix}InterviewedById`] = me.id;

  let newTrail = this.candidate.auditTrail ?? '';
  if (adminMode) {
    newTrail = appendAuditEntry(newTrail, { name: me.title, date: new Date().toISOString(), component: ROUNDS.onshoreRound.name });
    fields[AUDIT_TRAIL_COL] = newTrail;
  }

  this.saving = true;
  this.candidates.submitRound(this.candidate, fields).subscribe({
    next: () => {
      if (this.decision !== null) this.candidate.onshoreRound.selection = this.decision as RoundStatus;
      this.candidate.onshoreRound.feedback = this.overall;
      if (adminMode) this.candidate.auditTrail = newTrail;

      this.candidates.patchStatus(this.candidate).subscribe({
        next: () => {
          this.saving = false;
          this.ngOnChanges();
          this.changed.emit();
          if (adminMode) {
            this.justSaved = true;
            setTimeout(() => { this.justSaved = false; }, 3000);
          } else {
            this.router.navigate(['/success']);
          }
        },
        error: () => { this.saving = false; },
      });
    },
    error: () => { this.saving = false; },
  });
}
```

```html
<div class="actions" *ngIf="access.canEdit">
  <button class="btn primary" *ngIf="access.canEditDecision" [disabled]="!decision || saving" (click)="submit()">Submit round</button>
  <button class="btn primary" *ngIf="access.canEdit && !access.canEditDecision" [disabled]="saving" (click)="submit(true)">Save changes (Admin)</button>
  <span class="chip pass" *ngIf="justSaved"><i></i>Saved</span>
</div>
<div class="dim small" *ngIf="lastAdminEdit() as edit" style="margin-top:6px">
  Last edited by {{ edit.name }} · {{ formatDate(edit.date) }}
</div>
```

## Same pattern for `hr-round-panel.component.ts` / `.html`

```ts
justSaved = false;   // NEW

submit(adminMode = false): void {
  if (this.saving) return;
  if (!adminMode && (!this.decision || !this.access.canEditDecision)) return;

  const me = this.currentUser.get();
  const prefix = ROUNDS.hrRound.prefix;

  const fields: Record<string, any> = {
    [`${prefix}OfferSent`]: this.offerSent,
    [`${prefix}OfferAccepted`]: this.offerAccepted,
    [`${prefix}InterviewDate`]: new Date().toISOString(),
  };
  if (this.decision !== null) fields[`${prefix}InterviewSelection`] = this.decision;
  if (!adminMode) fields[`${prefix}InterviewedById`] = me.id;

  let newTrail = this.candidate.auditTrail ?? '';
  if (adminMode) {
    newTrail = appendAuditEntry(newTrail, { name: me.title, date: new Date().toISOString(), component: ROUNDS.hrRound.name });
    fields[AUDIT_TRAIL_COL] = newTrail;
  }

  this.saving = true;
  this.candidates.submitRound(this.candidate, fields).subscribe({
    next: () => {
      this.candidate.hrRound.offerSent = this.offerSent;
      this.candidate.hrRound.offerAccepted = this.offerAccepted;
      if (this.decision !== null) this.candidate.hrRound.selection = this.decision as RoundStatus;
      if (adminMode) this.candidate.auditTrail = newTrail;

      this.candidates.patchStatus(this.candidate).subscribe({
        next: () => {
          this.saving = false;
          this.ngOnChanges();
          this.changed.emit();
          if (adminMode) {
            this.justSaved = true;
            setTimeout(() => { this.justSaved = false; }, 3000);
          } else {
            this.router.navigate(['/success']);
          }
        },
        error: () => { this.saving = false; },
      });
    },
    error: () => { this.saving = false; },
  });
}
```

```html
<div class="actions" *ngIf="access.canEdit">
  <button class="btn primary" *ngIf="access.canEditDecision" [disabled]="!decision || saving" (click)="submit()">Save</button>
  <button class="btn primary" *ngIf="access.canEdit && !access.canEditDecision" [disabled]="saving" (click)="submit(true)">Save changes (Admin)</button>
  <span class="chip pass" *ngIf="justSaved"><i></i>Saved</span>
</div>
<div class="dim small" *ngIf="lastAdminEdit() as edit" style="margin-top:6px">
  Last edited by {{ edit.name }} · {{ formatDate(edit.date) }}
</div>
```

## Rule summary, now identical across all four panels
- **Normal "Submit"** (`submit()` / `submit(round)`, `adminMode` defaults `false`) → unchanged, still `router.navigate(['/success'])`.
- **"Save changes (Admin)"** (`submit(true)` / `submit(round, true)`) → no navigation, sets `justSaved`, shows inline "Saved" chip for 3 seconds, stays on the page.