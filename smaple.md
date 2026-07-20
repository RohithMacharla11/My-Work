Two separate changes — one CSS/HTML tweak for alignment, one config addition for the new skill columns.

## 1. Skill name + max marks on one line, right-aligned

Right now `(Max {{ s.maxMarks }})` sits right after the label text, so it drifts left/right depending on label length. To pin it to the right edge of the column consistently, wrap both in a flex row inside the cell.

**`tech-round-panel.component.html`** — find the skill cell:
```html
<td class="col-skill">
  <b>{{ s.label }}</b><span *ngIf="s.maxMarks"> (Max {{ s.maxMarks }})</span>
</td>
```

Change to:
```html
<td class="col-skill">
  <div class="skill-row-flex">
    <b>{{ s.label }}</b>
    <span class="max-marks" *ngIf="s.maxMarks">Max {{ s.maxMarks }}</span>
  </div>
</td>
```

**`round-panel.shared.scss`** (or `tech-round-panel.component.scss` if you want it scoped only to this table) — add:

```scss
.skill-row-flex {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 10px;
  white-space: nowrap;
}

.max-marks {
  color: var(--muted, #8a8a8a);
  font-size: 0.85em;
  font-weight: 500;
  flex-shrink: 0;
}
```

Since `.col-skill` is a fixed-width table column, every row's `max-marks` span now lands flush against the same right edge — straight vertical line down the column, label always on one line to the left of it.

## 2. Add 4 new management round skills

These are internal SharePoint column names on the `ManagementRound` fields (`SkillOneMR`…`SkillFiveMR` pattern), so they go straight into the config array. Everything downstream — `SELECT_FIELDS`, the mgmt panel's row-building, the submit payload — already loops over this array by index, so adding entries here is the only code change needed.

**`cohort.config.ts`:**
```ts
export const MGMT_SKILL_COLS = [
  'SkillOneMR',
  'SkillTwoMR',
  'SkillThreeMR',
  'SkillFourMR',
  'SkillFiveMR',
  'Justification',
  'Constrains',
  'PotentialOpportunities',
  'PointsOfAttention',
];
```

**One thing you need to do on the SharePoint side, not code:** `MgmtRoundPanelComponent.ngOnChanges()` builds its rows like this —

```ts
this.lookups.managementSkills().subscribe(labels => {
  const n = Math.min(labels.length, MGMT_SKILL_COLS.length);
  this.rows = Array.from({ length: n }, (_, i) => ({
    label: labels[i],
    comment: this.candidate.managementComments[i] ?? '',
  }));
});
```

`labels` comes from the `ManagementSkills` SharePoint list (titles), zipped **by index** with `MGMT_SKILL_COLS`. So the row count is `Math.min(labels.length, MGMT_SKILL_COLS.length)` — meaning you need **4 new items added to the `ManagementSkills` list** (titled `Justification`, `Constrains`, `Potential opportunities`, `Points of attention` — whatever display label you want) in the same order as the columns above, or the zip will misalign (e.g. row 6 could show the wrong label against `Justification`'s column).

Also double check the internal column names on the `ManagementRound`-related SharePoint fields — `Constrains` (missing the second "t") looks like it might be a typo either from the list design or just how you typed it here. If the actual SharePoint internal name is `Constraints`, use that exact spelling in the config array — internal names must match exactly or the field write/read will silently no-op on that column.