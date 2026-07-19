That actually points to a specific browser behavior: `sessionStorage` is supposed to clear when a tab closes, but Chrome/Edge deliberately **restore** sessionStorage when you reopen via "reopen closed tab" (Ctrl+Shift+T) or when the browser restores a session after a crash/restart — that's likely what you're hitting, not a bug in the literal sense, but it's giving you stale data you don't want.

The cleanest fix isn't to fight sessionStorage's edge-case restore behavior — it's to stop trusting the *stored cohort* as authoritative and always re-validate it against what's actually available right now.

**In `dashboard.component.ts`, `loadInitialData()`,** find:
```ts
if (coharts.length > 0) {
  if (!this.selectedCohort) {
    // No cohort was persisted → pick the newest one as a fallback
    this.selectedCohort = cohorts[0].title;
  }
  this.filters = { ...this.filters, cohort: this.selectedCohort ?? undefined };
  this.dashboardState.setCohort(this.selectedCohort ?? undefined);
}
```

Replace with:
```ts
if (cohorts.length > 0) {
  // Validate any restored cohort still exists in the live list — a
  // stale sessionStorage restore (e.g. Chrome's "reopen closed tab")
  // should never show a cohort that's no longer real.
  const stillValid = this.selectedCohort && cohorts.some(c => c.title === this.selectedCohort);
  if (!stillValid) {
    this.selectedCohort = cohorts[0].title;
  }
  this.filters = { ...this.filters, cohort: this.selectedCohort ?? undefined };
  this.dashboardState.setCohort(this.selectedCohort ?? undefined);
}
```

This directly solves "whatever's shown in the dropdown should be shown in the particular list" — even if sessionStorage hands back an old cohort title, it now only survives if that cohort still genuinely appears in the freshly-fetched `cohorts` list; otherwise it silently falls back to the newest real one, exactly like a first-time load.

If you'd rather go further and stop *any* persistence across tab closes entirely (not just cohort), tell me — that's a bigger change (removing `saveToSession`/`loadFromSession` calls from `DashboardStateService` altogether) and would also drop your saved filters/tab/location on every reopen, which might not be what you want for a normal same-session refresh.