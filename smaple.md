Got it — going through each one precisely, based only on what's actually in your code (not touching anything else).

## 1. People-picker should search both HR groups

Wherever HR-assign uses `<app-people-picker>` (create-cohort / dashboard's HR-assign modal), the `[groups]` input needs both HR groups, not just one:

```html
<app-people-picker [groups]="['HR Cohort Team', 'HR Recruiters']" (picked)="onHrPicked($event)">
</app-people-picker>
```

Since `PeoplePickerComponent.onInput()` calls `sp.getMembersFromGroups(this.groups)` and that already dedupes by user ID (`getMembersFromGroups` in `sharepoint.service.ts`), passing both group titles is enough — anyone in either group will appear once, no duplicates.

## 2. Cohort filter missing on first load, present after refresh

Root cause: `DashboardStateService.loadFromSession()` restores `filters.cohort` from sessionStorage — but on a **fresh** load there's no session, so whatever cohort you default-select in the dropdown only updates the local UI, not `state.filters`, before `reload()` fires.

Fix — in `DashboardComponent.ngOnInit()` (or wherever you pick the default/latest cohort), make sure the cohort is written into state **before** the first `reload()` call:

```ts
ngOnInit(): void {
  this.state.loadFromSession();
  const s = this.state.get();
  this.location = s.location;
  this.tab = s.tab;
  this.selectedCohort = s.selectedCohort;

  if (!this.selectedCohort) {
    this.lookups.activeCohorts().subscribe(cohorts => {
      if (cohorts.length) {
        this.selectedCohort = cohorts[0].title;
        this.state.setCohort(this.selectedCohort); // ← write to state FIRST
      }
      this.reload(); // ← then reload
    });
  } else {
    this.reload();
  }
}
```

The key rule: **never call `reload()` before `state.setCohort()`/`state.set()` has run** for whatever cohort ends up selected — that's exactly what refresh does correctly (session loads before reload) and first-load was skipping.

## 3. Stray dot when role/profile missing

In `candidate-detail.component.html`, the `hero-meta` line:

```html
<span>{{ candidate.location }}</span>
<span class="separator"> · </span>
<span>{{ candidate.roleDesignation }}</span>
```

The separator here is unconditional. Fix, matching the pattern you already use for the profile separator:

```html
<span>{{ candidate.location }}</span>
<span class="separator" *ngIf="candidate.roleDesignation"> · </span>
<span *ngIf="candidate.roleDesignation">{{ candidate.roleDesignation }}</span>
<span class="separator" *ngIf="candidate.roleDesignation && candidate.profile"> · </span>
<span *ngIf="candidate.profile">{{ candidate.profile }}</span>
```

Now the dot only appears when there's actually a role to show.

## 4. MaxMarks in brackets after skill label

**`candidate.model.ts`** — add to `SkillRow`:
```ts
export interface SkillRow {
  label: string;
  slotIndex: number;
  screeningScore: string;
  round1Comment: string;
  round2Comment: string;
  maxMarks?: number; // NEW
}
```

**`lookup.service.ts`** — `skillsForType()` currently selects `['SkillValue', 'SkillOrder']`. Add the field and return it:
```ts
export interface InterviewSkill { value: string; order: number; maxMarks: number; }

skillsForType(interviewType: string): Observable<InterviewSkill[]> {
  ...
  const obs = this.sp
    .getAll<any>(LISTS.interviewSkills, {
      select: ['SkillValue', 'SkillOrder', 'MaxMarks'],
      filter: `Title eq '${safe}'`,
      orderby: 'SkillOrder asc',
      top: 100,
    })
    .pipe(
      map(items => items
        .sort((a, b) => (a.SkillOrder ?? 0) - (b.SkillOrder ?? 0))
        .map(i => ({ value: i.SkillValue, order: i.SkillOrder, maxMarks: i.MaxMarks }))),
      shareReplay(1),
    );
  ...
}
```

This changes the return type from `Observable<string[]>` to `Observable<InterviewSkill[]>` — you'll need to update `interviewTypes()`/callers accordingly, or add a **new** method (e.g. `skillsForTypeDetailed`) if `skillsForType()` is used elsewhere expecting plain strings, to avoid breaking other call sites.

**`candidate-detail.component.ts`** — `attachSkillLabels()`:
```ts
private attachSkillLabels(candidate: Candidate): void {
  this.lookups.skillsForType(candidate.interviewType).subscribe({
    next: skills => {
      candidate.skills = candidate.skills
        .map((slot, i) => ({ ...slot, label: skills[i]?.value ?? '', maxMarks: skills[i]?.maxMarks }))
        .filter(slot => !!slot.label);
      this.candidate = candidate;
      this.loading = false;
    },
    ...
  });
}
```

**`tech-round-panel.component.html`** — wherever `s.label` is rendered as the skill name:

```html
<b>{{ s.label }}<span *ngIf="s.maxMarks"> (Max {{ s.maxMarks }})</span></b>
```
→ `Core Java & JDK 21+ (Max 14)`

## 5. Mgmt panel shouldn't get "assign to me" inside Tech Round 2

The cause: `cohort.config.ts` → `ROUNDS.techRound2.selfAssign` currently includes **both** `'TechPanel'` and `'MgmtPanel'`. That's what makes `WorkflowService.getAccess(candidate, 'techRound2')` return `'assignable'` for a Mgmt viewer, which surfaces the "Assign to me" CTA inside `tech-round-panel.component.html` — that CTA calls `TechRoundPanelComponent.assignToMe('techRound2')`, which just assigns you as the *tech* interviewer, not the skip-and-take-mgmt-round behavior you want.

**Fix — `cohort.config.ts`:**
```ts
export const ROUNDS: Record<RoundKey, { prefix: string; name: string; selfAssign: UserRole[]; assignGroup: string[] }> = {
  techRound1:  { prefix: 'TechRound1',  name: 'Tech Round 1',    selfAssign: ['TechPanel'],  assignGroup: ['Tech Interview Panel'] },
  techRound2:  { prefix: 'TechRound2',  name: 'Tech Round 2',    selfAssign: ['TechPanel'],  assignGroup: ['Tech Interview Panel', 'Mgmt Interview Panel'] }, // ← removed 'MgmtPanel'
  mgmtRound:   { prefix: 'MgmtRound',   name: 'Management Round',selfAssign: ['MgmtPanel'],  assignGroup: ['Mgmt Interview Panel'] },
  onshoreRound:{ prefix: 'OnShoreRound',name: 'Onshore Round',   selfAssign: [],             assignGroup: ['On Shore Interview Panel', 'HR Cohort Team'] },
  hrRound:     { prefix: 'HrRound',     name: 'HR Round',        selfAssign: [],             assignGroup: ['HR Cohort Team'] },
};
```

`assignGroup` stays as-is (that's for HR's assign-dialog picker, unrelated to self-assign) — only `selfAssign` on `techRound2` loses `'MgmtPanel'`.

This means a Mgmt viewer's `r2` access on the tech panel will no longer be `'assignable'` — it'll fall through to whatever your `getAccess` returns for a role that can view but not self-assign (likely `'not-reached'`/`'readonly'` depending on your exact `getAccess` branching, which I haven't seen in full — check that function returns something sensible like `readonly`/`no-cta` there rather than accidentally `no-access`, since Mgmt still needs to *see* tech round 2 status).

**Then, in `mgmt-round-panel.component.html`**, wherever the "assignable" notice for mgmt round is shown (same pattern as onshore's `notice muted`), add the skip warning:

```html
<div class="notice info" *ngIf="access.state === 'assignable'">
  Assigning yourself to the Management Round
  <span *ngIf="candidate.techRound2.selection === 'Pending'"> will mark <b>Tech Round 2 as Not Applicable</b> and</span>
  will make you the interviewer for this round.
</div>
```

This keeps the existing correct auto-skip logic in `MgmtRoundPanelComponent.assignToMe()` exactly as-is — you don't need to touch that method — you're just removing the competing entry point in the tech panel and telling the mgmt user upfront what claiming the round will do.

---

That covers all five as concrete, isolated edits — nothing else in the app touched. Let me know once you've made them and I'll help verify, or if you want to paste the actual `WorkflowService.getAccess()` body so I can confirm exactly what state Mgmt will land on for tech round 2 after the config change.