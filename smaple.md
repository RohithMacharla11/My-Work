Two changes needed — point all three location keys at the same list, and filter by the `Location` column instead of by list name.

**1. `cohort.config.ts` — make all three keys resolve to the same list:**
```ts
export const LOCATION_LIST: Record<LocationKey, string> = {
  Mumbai: LISTS.mumbai,
  Chennai: LISTS.mumbai,
  Bangalore: LISTS.mumbai,
};
```

**2. `candidate.service.ts` — add a location filter clause to every query.** Update `queryFor` to accept and apply it:
```ts
private queryFor(prescreen: 'Selected' | 'Rejected', filters: DashboardFilters, location: LocationKey): ListQuery {
  const baseFilter = buildDashboardFilter(prescreen, filters);
  const locClause = `Location eq '${location.replace(/'/g, "''")}'`;
  const combined = baseFilter ? `(${baseFilter}) and ${locClause}` : locClause;
  return {
    select: SELECT_FIELDS,
    expand: EXPAND_FIELDS,
    filter: combined,
    orderby: 'Modified desc',
    top: SERVER_PAGE_SIZE,
  };
}
```

Update the two callers to pass `location` through:
```ts
getActivePage(location: LocationKey, filters: DashboardFilters): Observable<Page<Candidate>> {
  return this.sp
    .getPage<any>(LOCATION_LIST[location], this.queryFor('Selected', filters, location))
    .pipe(map(page => this.mapPage(page, location)));
}

getRejectedPage(location: LocationKey, filters: DashboardFilters): Observable<Page<Candidate>> {
  return this.sp
    .getPage<any>(LOCATION_LIST[location], this.queryFor('Rejected', filters, location))
    .pipe(map(page => this.mapPage(page, location)));
}
```

**3. Still in `candidate.service.ts` — `mapCandidate()` should read the actual `Location` value from the item, not just echo back whichever tab you queried from** (matters once you filter/search across tabs or view a single record):

```ts
private mapCandidate(item: any, location: LocationKey): Candidate {
  return {
    id: item.Id,
    location: (item.Location as LocationKey) ?? location,
    // ...rest unchanged
```

`getById()` needs no change — `Id` is already unique across the whole list, and it now resolves to the same list regardless of which location tab you came from.

Save, recompile, refresh — Mumbai/Chennai/Bangalore tabs now all query `MumbaiInterview`, each filtered server-side by its own `Location` value, and the correct location displays on each candidate row instead of whatever tab you happened to click.