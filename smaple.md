This is a straightforward SharePoint redirect pattern — `NewForm.aspx` (SharePoint's native "add item" page) accepts a `Source` query parameter, and after Save or Cancel it sends the user back to whatever URL you put there. Since your app is a SPA using hash routing, `Source` needs to be the full URL back to your dashboard route.

## `create-cohort.component.ts` — replace the create/open logic

```ts
import { SharePointContextService } from '../../core/services/sharepoint-context.service';
import { LISTS } from '../../core/config/cohort.config';

constructor(private ctx: SharePointContextService) {}

openNewCohortForm(): void {
  const dashboardUrl = `${window.location.origin}${window.location.pathname}#/dashboard`;
  const listUrlName = LISTS.cohortSchedule.replace(/ /g, '%20');   // see note below
  const newFormUrl =
    `${this.ctx.siteUrl}/Lists/${listUrlName}/NewForm.aspx?Source=${encodeURIComponent(dashboardUrl)}`;

  window.location.href = newFormUrl;
}
```

Wire it to whatever button currently opens your custom create-cohort modal:
```html
<button class="btn primary" (click)="openNewCohortForm()">Create Cohort</button>
```

## What this does

- Navigates the browser straight to SharePoint's native New Item form for the **Cohort Schedule** list — no custom modal, exactly SharePoint's own UI.
- User fills it in and clicks **Save** (or Cancel) on that native page.
- SharePoint then redirects back to whatever `Source` URL you passed — here, your dashboard route (`#/dashboard`) — landing them right back in the app.

## One thing to verify before this works

`NewForm.aspx`'s URL segment isn't always the list's display title with spaces — SharePoint sometimes internally maps it differently (e.g. `Cohort%20Schedule` vs `CohortSchedule` vs an auto-generated internal name). The safest way to get it exactly right: open your **Cohort Schedule** list in the browser directly, click "+ New" there once, and copy the exact URL SharePoint shows you (it'll look like `.../Lists/Cohort%20Schedule/NewForm.aspx` or similar) — then hardcode that path (or just the list segment) into the constant above instead of deriving it from `LISTS.cohortSchedule`, since a display-title-based guess can silently 404 if the internal URL name differs.

Can you grab that URL for me? Once I see the real segment I'll lock the exact string in instead of the `.replace(/ /g, '%20')` guess.