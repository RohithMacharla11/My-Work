Here's the exact same pattern applied to `mgmt`, `onshore`, and `tech` panels, matching the real code from your screenshots.

## `mgmt-round-panel.component.ts`

Add to imports:
```ts
import { AUDIT_TRAIL_COL } from '../../../../core/config/cohort.config';
import { appendAuditEntry, latestAuditEntryFor } from '../../../../core/utils/audit-trail.util';
```

Add near the top of the class:
```ts
get isAdminOverride(): boolean {
  return this.access?.canEdit === true && this.access?.canEditDecision === false;
}

lastAdminEdit() {
  return latestAuditEntryFor(this.candidate.auditTrail, ROUNDS.mgmtRound.name);
}
```

Replace `submit()` (lines 99–139 in your screenshot):
```ts
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

      this.ngOnChanges();
      this.changed.emit();
      this.candidates.patchStatus(this.candidate).subscribe({
        next: () => { this.saving = false; this.ngOnChanges(); this.router.navigate(['/success']); },
        error: () => { this.saving = false; },
      });
    },
    error: () => { this.saving = false; },
  });
}
```

`mgmt-round-panel.component.html` — replace the single Submit button:
```html
<div class="editfoot" *ngIf="access.canEdit">
  <div class="actions">
    <button class="btn primary" *ngIf="access.canEditDecision" [disabled]="!decision || saving" (click)="submit()">Submit round</button>
    <button class="btn primary" *ngIf="isAdminOverride" [disabled]="saving" (click)="submit(true)">Save changes (Admin)</button>
  </div>
  <div class="dim small" *ngIf="lastAdminEdit() as edit" style="margin-top:6px">
    Last edited by {{ edit.name }} · {{ formatDate(edit.date) }}
  </div>
</div>
```

---

## `onshore-round-panel.component.ts`

Same imports as above. Add:
```ts
get isAdminOverride(): boolean {
  return this.access?.canEdit === true && this.access?.canEditDecision === false;
}

lastAdminEdit() {
  return latestAuditEntryFor(this.candidate.auditTrail, ROUNDS.onshoreRound.name);
}
```

Replace `submit()` (lines 50–86):
```ts
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

      this.ngOnChanges();
      this.changed.emit();
      this.candidates.patchStatus(this.candidate).subscribe({
        next: () => { this.saving = false; this.ngOnChanges(); this.router.navigate(['/success']); },
        error: () => { this.saving = false; },
      });
    },
    error: () => { this.saving = false; },
  });
}
```

`onshore-round-panel.component.html` — replace the Submit button:
```html
<div class="actions" *ngIf="access.canEdit">
  <button class="btn primary" *ngIf="access.canEditDecision" [disabled]="!decision || saving" (click)="submit()">Submit round</button>
  <button class="btn primary" *ngIf="isAdminOverride" [disabled]="saving" (click)="submit(true)">Save changes (Admin)</button>
</div>
<div class="dim small" *ngIf="lastAdminEdit() as edit" style="margin-top:6px">
  Last edited by {{ edit.name }} · {{ formatDate(edit.date) }}
</div>
```

---

## `tech-round-panel.component.ts`

Same imports. Add:
```ts
lastAdminEdit(round: TechKey) {
  return latestAuditEntryFor(this.candidate.auditTrail, ROUNDS[round].name);
}
```
(`r1.canEdit && !r1.canEditDecision` / `r2.canEdit && !r2.canEditDecision` already work directly in the template — no separate getter needed since it's per-round.)

Replace `submit(round: TechKey)` (lines 85–127):
```ts
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
  this.candidate.skills.forEach((s, i) => { fields[techCommentCol(s.slotIndex, round)] = comments[i]; });

  let newTrail = this.candidate.auditTrail ?? '';
  if (adminMode) {
    newTrail = appendAuditEntry(newTrail, { name: me.title, date: new Date().toISOString(), component: ROUNDS[round].name });
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
          this.router.navigate(['/success']);
        },
        error: () => { this.saving = false; },
      });
    },
    error: () => { this.saving = false; },
  });
}
```

(Note: I added `this.changed.emit()` here since I noticed it was missing from the original — this panel's success callback never told the parent to refresh, unlike mgmt/onshore. Worth confirming that was intentional before; if it wasn't, this fixes a second latent sync bug.)

`tech-round-panel.component.html` — replace the submit-footer actions block (lines 227–234):
```html
<div class="actions">
  <button class="btn primary" *ngIf="r1.canEditDecision" [disabled]="!r1Decision || saving" (click)="submit('techRound1')">Submit Tech Round 1</button>
  <button class="btn primary" *ngIf="r1.canEdit && !r1.canEditDecision" [disabled]="saving" (click)="submit('techRound1', true)">Save Tech Round 1 (Admin)</button>

  <button class="btn primary" *ngIf="r2.canEditDecision" [disabled]="!r2Decision || saving" (click)="submit('techRound2')">Submit Tech Round 2</button>
  <button class="btn primary" *ngIf="r2.canEdit && !r2.canEditDecision" [disabled]="saving" (click)="submit('techRound2', true)">Save Tech Round 2 (Admin)</button>
</div>
<div class="dim small" *ngIf="lastAdminEdit('techRound1') as edit" style="margin-top:6px">
  Tech Round 1 last edited by {{ edit.name }} · {{ formatFriendlyDate(edit.date) }}
</div>
<div class="dim small" *ngIf="lastAdminEdit('techRound2') as edit" style="margin-top:2px">
  Tech Round 2 last edited by {{ edit.name }} · {{ formatFriendlyDate(edit.date) }}
</div>
```

That's all four panels done consistently (`hr` from earlier, plus `mgmt`/`onshore`/`tech` here) — same audit-trail append logic, same two-button pattern, same "last edited by" line everywhere.