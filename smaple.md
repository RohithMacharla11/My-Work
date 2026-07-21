Much simpler — one column, no schema changes needed beyond what you already have. Here's the full design.

## 1. `cohort.config.ts` — just the column name
```ts
export const AUDIT_TRAIL_COL = 'AuditTrail';
```

## 2. New file — `core/utils/audit-trail.util.ts`

```ts
export interface AuditEntry {
  name: string;
  date: string;       // ISO string
  component: string;  // e.g. "HR Round"
}

const ENTRY_SEP = ';';
const FIELD_SEP = '|';

export function parseAuditTrail(raw: string | null | undefined): AuditEntry[] {
  if (!raw) return [];
  return raw
    .split(ENTRY_SEP)
    .map(e => e.trim())
    .filter(Boolean)
    .map(e => {
      const [name, date, component] = e.split(FIELD_SEP);
      return { name: name ?? '', date: date ?? '', component: component ?? '' };
    });
}

export function appendAuditEntry(
  raw: string | null | undefined,
  entry: AuditEntry
): string {
  const newEntry = `${entry.name}${FIELD_SEP}${entry.date}${FIELD_SEP}${entry.component}`;
  return raw ? `${raw}${ENTRY_SEP}${newEntry}` : newEntry;
}

/** Latest entry for a specific round/component (last one appended for that component). */
export function latestAuditEntryFor(raw: string | null | undefined, component: string): AuditEntry | null {
  const entries = parseAuditTrail(raw).filter(e => e.component === component);
  return entries.length ? entries[entries.length - 1] : null;
}
```

## 3. `candidate.model.ts` — one field
```ts
auditTrail?: string;
```

## 4. `candidate.service.ts`

Import `AUDIT_TRAIL_COL` alongside your other config imports, add to `SELECT_FIELDS`/`DASHBOARD_SELECT_FIELDS` if you want it dashboard-visible, and in `mapCandidate()`:
```ts
auditTrail: item[AUDIT_TRAIL_COL] ?? '',
```

## 5. Panel `submit(adminMode)` — build + append + push

`hr-round-panel.component.ts` (same pattern for mgmt/onshore/tech, shown once):

```ts
import { ROUNDS, AUDIT_TRAIL_COL } from '../../../../core/config/cohort.config';
import { appendAuditEntry, latestAuditEntryFor } from '../../../../core/utils/audit-trail.util';

get isAdminOverride(): boolean {
  return this.access?.canEdit === true && this.access?.canEditDecision === false;
}

lastAdminEdit() {
  return latestAuditEntryFor(this.candidate.auditTrail, ROUNDS.hrRound.name);
}

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
  if (!adminMode) {
    fields[`${prefix}InterviewedById`] = me.id;
  }

  let newTrail = this.candidate.auditTrail ?? '';
  if (adminMode) {
    newTrail = appendAuditEntry(newTrail, {
      name: me.title,
      date: new Date().toISOString(),
      component: ROUNDS.hrRound.name,
    });
    fields[AUDIT_TRAIL_COL] = newTrail;
  }

  this.saving = true;
  this.candidates.submitRound(this.candidate, fields).subscribe({
    next: () => {
      this.candidate.hrRound.offerSent = this.offerSent;
      this.candidate.hrRound.offerAccepted = this.offerAccepted;
      if (this.decision !== null) this.candidate.hrRound.selection = this.decision as RoundStatus;
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

## 6. Template — two buttons + last-edit line

```html
<div class="actions" *ngIf="access.canEditDecision">
  <button class="btn primary" [disabled]="!decision || saving" (click)="submit()">Save</button>
</div>

<div class="actions" *ngIf="isAdminOverride">
  <button class="btn primary" [disabled]="saving" (click)="submit(true)">Save changes (Admin)</button>
</div>

<div class="dim small" *ngIf="lastAdminEdit() as edit" style="margin-top:6px">
  Last edited by {{ edit.name }} · {{ formatDate(edit.date) }}
</div>
```

## Repeat for the other three panels

- **`mgmt-round-panel.component.ts`** — same, `ROUNDS.mgmtRound.name`/`prefix`, decision field is `${prefix}InterviewSelection`, `MGMT_OVERALL_COL`, `MGMT_SKILL_COLS` untouched.
- **`onshore-round-panel.component.ts`** — same, `ROUNDS.onshoreRound.name`/`prefix`.
- **`tech-round-panel.component.ts`** — `submit(round: TechKey, adminMode = false)`, use `ROUNDS[round].name` and `ROUNDS[round].prefix` dynamically, same append logic keyed to whichever round (R1 or R2) was saved. Template needs two admin buttons (one per round), each calling `submit('techRound1', true)` / `submit('techRound2', true)`, gated by `r1.canEdit && !r1.canEditDecision` / `r2.canEdit && !r2.canEditDecision`.

## Nothing else changes
- `ROUNDS[key].name` already exists in your config — that's what supplies the "component" string, so no new constant needed for round names.
- No new SharePoint columns beyond confirming `AuditTrail` is a **multi-line text** field (needs to be, since entries will grow past 255 chars over time) — single-line text truncates silently in SharePoint, worth double-checking that column's type before this goes live.
- One audit column now covers all five rounds — you can also render a **full history** anywhere by calling `parseAuditTrail(candidate.auditTrail)` and looping, if you ever want a complete admin-changes log view instead of just "latest per round."