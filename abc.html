Good catch — that's a real gap. Right now `isMine` is computed purely by comparing your user ID against the assignee, with **no check that your current roles still entitle you to act on that round**. So if your roles changed (or you hold multiple roles and the round in question isn't one your active roles can own), the code still treats you as "mine" and shows Continue/Unassign.

**Fix in `workflow.service.ts`** — there are two places that compute this the same unsafe way: `getAccess()` and `getDashboardAction()`. Both need a role check added alongside the ID match.

Add this helper near your other private helpers:

```ts
/** Does the current user hold a role that's actually entitled to own this round? */
private ownsRoundByRole(key: RoundKey): boolean {
  const selfAssignRoles = ROUNDS[key].selfAssign;
  // Rounds with no selfAssign roles (Onshore, HR) are HR-only ownership
  return selfAssignRoles.length
    ? selfAssignRoles.some(r => this.currentUser.hasRole(r))
    : this.isHRUser();
}
```

**In `getAccess()`**, find:
```ts
const mine = !!assignedTo && assignedTo.id === me.id;
```
Replace with:
```ts
const mine = !!assignedTo && assignedTo.id === me.id && this.ownsRoundByRole(key);
```

**In `getDashboardAction()`**, find:
```ts
base.isMine = !!assignedTo && assignedTo.id === me.id;
```
Replace with:
```ts
base.isMine = !!assignedTo && assignedTo.id === me.id && this.ownsRoundByRole(active);
```

That's it — `canUnassign` and the template's Continue/Unassign visibility both derive from `isMine`, so once `isMine` correctly becomes `false` for a round your current roles don't cover, the dashboard.component.html changes from before automatically fall back to just the Open button for that row, since `!action(c).isMine` renders Open instead of Continue.

One thing worth double-checking with whoever manages roles: HR Admin/Recruiter already bypass all of this via `canHrAssign`/`canEditByRole`, so this fix only affects Tech/Management/Onshore panel members — which is exactly the case you're describing.