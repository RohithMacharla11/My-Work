Good — this maps cleanly onto architecture you already have. `odata-filter.service.ts`'s `buildDashboardFilter()` already builds an OData `$filter` clause from `DashboardFilters` (role/profile/interviewType/cohort), and `searchGroup()` already builds a `substringof(...)` OR-clause across `SEARCHABLE_COLUMNS` — but currently the dashboard's search box is wired to **client-side** filtering (`filteredRows` in `dashboard.component.ts`, using `searchTerm` locally against already-fetched rows). You want it moved server-side, combined with the existing filters, and triggered only by a button click — not on every keystroke.

## 1. `odata-filter.service.ts` — expand `SEARCHABLE_COLUMNS`, wire search into the main filter

Update the column list to the four you asked for:
```ts
export const SEARCHABLE_COLUMNS = [
  'CandidateName',
  'CandidateEmailID',
  'CandidatePhoneNumber',
  'CandidateID',
];
```

`searchGroup()` and `buildDashboardFilter()` already exist and already combine `search` into the returned clause via `filters.search` — so **no changes needed there**, since `buildDashboardFilter` already does `[role, profile, type, cohort, search].forEach(c => { if (c) clauses.push(c); })`. It was already designed to include search server-side — it just wasn't being triggered that way from the UI. Good news: this means step 1 is just the column list change above.

## 2. `dashboard.component.ts` — split "typed text" from "active search," gate by button

**Add:**
```ts
pendingSearch = '';   // NEW — what's typed in the box, not yet submitted
```

**Replace `onSearch()`** — currently probably does `this.searchTerm = term; this.currentPage = 1;` (client-side, live). Change it to just track the typed value without triggering anything:

```ts
onSearch(term: string): void {
  this.pendingSearch = term;
}
```

**Add a new method for the button:**
```ts
runSearch(): void {
  this.filters = { ...this.filters, search: this.pendingSearch.trim() || undefined };
  this.currentPage = 1;
  this.reload();
  this.dashboardState.set({ filters: this.filters });
}
```

**In `clearFilters()`**, also reset `pendingSearch` alongside `searchTerm` (or remove `searchTerm` entirely if it's now unused — see step 4):
```ts
clearFilters(): void {
  const cohort = this.selectedCohort;
  this.filters = { ...(cohort ? { cohort } : {}) };
  this.pendingSearch = '';
  this.reload();
  ...
}
```

**`ngOnInit()` / `loadInitialData()` restore path** — if `snap.filters.search` was restored from sessionStorage, also seed `pendingSearch` so the box shows the last active search on reload:

```ts
this.pendingSearch = snap.filters?.search ?? '';
```

## 3. `dashboard.component.html` — add the search button

Find your search input (currently likely `(input)="onSearch(...)"` bound directly):
```html
<input
  type="text"
  placeholder="Search name, email, phone, ID..."
  [value]="pendingSearch"
  (input)="onSearch($any($event.target).value)"
  (keyup.enter)="runSearch()"
/>
<button class="btn primary" (click)="runSearch()">Search</button>
```
(`keyup.enter` is optional but a nice touch — pressing Enter also fires the search, not just the button.)

## 4. `filteredRows` getter — remove the client-side search re-filter

Since search now happens server-side via `$filter`, the client-side re-filtering by `searchTerm` in `UI_SEARCHABLE_COLUMNS` is redundant and would double-filter incorrectly (rows already server-filtered wouldn't need re-checking, and worse, `pendingSearch` typed-but-unsubmitted text shouldn't affect what's shown). Simplify:

```ts
get filteredRows(): Candidate[] {
  return this.rows;   // server already applied search + all other filters
}
```

You can delete `UI_SEARCHABLE_COLUMNS` and the old `searchTerm` field entirely if nothing else references them — or leave them unused if you'd rather not touch more than necessary right now.

## 5. Every other filter — untouched, confirmed

`role`/`profile`/`interviewType`/`cohort` dropdowns already call `onFilterChange()` → `reload()` directly (live, no button) exactly as before — none of that changes. `runSearch()` reuses the exact same `this.filters` object and `reload()` pipeline, so when you click Search, the request naturally includes whatever role/profile/cohort/tab/location filters are currently active, combined via `buildDashboardFilter()`'s existing `.join(' and ')` — search is just one more clause in the same `$filter`, not a separate mechanism.

**Net behavior:** typing in the box does nothing to the data until you click Search (or press Enter) — dropdown filters keep working live as they already did — and once you do search, it's a real server-side `substringof` query across name/email/phone/ID, combined with whatever else is filtered, restored correctly from sessionStorage on refresh.