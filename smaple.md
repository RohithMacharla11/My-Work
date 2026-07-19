Found it — the people-picker is **hardcoded** to always search two fixed groups (`'On Shore Interview Panel'`, `'HR Cohort Team'`), regardless of which round you opened it from. It doesn't even look at what round/panel you're assigning to — that's why the same people always show up no matter where you're picking from.

**The logic you want, mapped to groups:**

| Round | Who should appear |
|---|---|
| Tech Round 1 | Tech Panel only |
| Tech Round 2 | Tech Panel **+** Management Panel |
| Management Round | Management Panel only |
| Onshore Round | Onshore Panel **+** HR |
| HR Round | HR only |

**Fix 1 — `cohort.config.ts`, change `assignGroup` from a single string to an array per round:**
```ts
export const ROUNDS: Record<RoundKey, { prefix: string; name: string; selfAssign: UserRole[]; assignGroup: string[] }> = {
  techRound1:  { prefix: 'TechRound1', name: 'Tech Round 1',      selfAssign: ['TechPanel'],            assignGroup: ['Tech Interview Panel'] },
  techRound2:  { prefix: 'TechRound2', name: 'Tech Round 2',      selfAssign: ['TechPanel','MgmtPanel'], assignGroup: ['Tech Interview Panel', 'Mgmt Interview Panel'] },
  mgmtRound:   { prefix: 'MgmtRound',  name: 'Management Round',  selfAssign: ['MgmtPanel'],            assignGroup: ['Mgmt Interview Panel'] },
  onshoreRound:{ prefix: 'OnShoreRound', name: 'Onshore Round',   selfAssign: [],                       assignGroup: ['On Shore Interview Panel', 'HR Cohort Team'] },
  hrRound:     { prefix: 'HrRound',    name: 'HR Round',          selfAssign: [],                       assignGroup: ['HR Cohort Team'] },
};
```

**Fix 2 — `people-picker.component.ts`,** make the group list an `@Input` instead of hardcoded:
```ts
@Input() groups: string[] = [];
```
Then in `onInput()`, replace:
```ts
const groups = ['On Shore Interview Panel', 'HR Cohort Team'];
```
with:
```ts
const groups = this.groups;
```

**Fix 3 — `dashboard.component.html`,** pass the round's groups into the picker:
```html
<app-people-picker [groups]="ROUNDS[assignCtx.round].assignGroup" (picked)="onPicked($event)"></app-people-picker>
```

**Fix 4 —** wherever `groupName` (singular string) is currently used for the modal's "Only members of `<b>{{ groupName }}</b>` should be chosen" hint text, join the array instead: `{{ ROUNDS[assignCtx.round].assignGroup.join(' or ') }}`.

One thing to confirm with whoever manages your SharePoint groups: I used `'Mgmt Interview Panel'` for Management — check that's the exact group title (case/spacing matters for the API call), since I only saw `'Tech Interview Panel'`, `'On Shore Interview Panel'`, and `'HR Cohort Team'` referenced directly in your code so far.