Sure — same pattern as the base64 icon: a button that reads the `FileNamePattern` field (comma-separated names/wildcards like `report_*.pdf, *.csv, invoice_?.docx`), converts each into a proper regex, and joins them with `|` into `FileNamePatternRegEx`.

**1. Add the conversion method in `metadata-management.component.ts`**

```ts
generateRegexFromFileNamePattern() {
  const raw = this.metadataManagementForm.get('FileNamePattern')?.value;
  if (!raw) return;

  const patterns = raw
    .split(',')
    .map((p: string) => p.trim())
    .filter((p: string) => p.length > 0);

  const regexParts = patterns.map((p: string) => this.wildcardToRegex(p));
  const combinedRegex = regexParts.join('|');

  this.metadataManagementForm.get('FileNamePatternRegEx')?.setValue(combinedRegex);
}

private wildcardToRegex(pattern: string): string {
  // escape regex special characters first, EXCEPT * and ?
  const escaped = pattern.replace(/[.+^${}()|[\]\\]/g, '\\$&');
  // convert wildcard tokens: * -> .*   and   ? -> .
  const withWildcards = escaped
    .replace(/\*/g, '.*')
    .replace(/\?/g, '.');
  return `^${withWildcards}$`;
}
```

This:
- Escapes regex metacharacters like `.`, `+`, `(`, `)` etc. so a literal `report.pdf` doesn't accidentally mean "report + any char + pdf"
- Converts `*` → `.*` (matches any sequence) and `?` → `.` (matches any single char) — standard filename-wildcard semantics
- Wraps each in `^...$` so it matches the whole filename, not a substring
- Joins all of them with `|` so the final regex matches any one of the patterns

Example: `report_*.pdf, invoice_?.docx` → `^report_.*\.pdf$|^invoice_.docx$`

**2. HTML — add the icon next to FileNamePattern (or between the two fields)**

```html
<div class="col-sm-3 d-flex flex-column">
  <mat-form-field>
    <mat-label>FileName Pattern</mat-label>
    <input matInput placeholder="FileName Pattern" formControlName="FileNamePattern">
    <i matSuffix
       class="bi bi-arrow-repeat"
       style="cursor:pointer; margin-right: 4px;"
       (click)="generateRegexFromFileNamePattern()"
       title="Generate RegEx from patterns">
    </i>
  </mat-form-field>
</div>
<div class="col-sm-3 d-flex flex-column">
  <mat-form-field>
    <mat-label>FileName Pattern RegEx</mat-label>
    <input matInput placeholder="FileName Pattern RegEx" formControlName="FileNamePatternRegEx">
  </mat-form-field>
</div>
```

**Notes / edge cases to consider:**

- If the user later edits `FileNamePattern` manually after already generating a regex, the two fields go out of sync until they click the icon again — that's expected/intended given your "click to regenerate" flow, but you could optionally disable/mark `FileNamePatternRegEx` as `readonly` so it's only ever set via the icon (prevents drift):
  ```html
  <input matInput ... formControlName="FileNamePatternRegEx" readonly>
  ```
- If a pattern entry has no `*` or `?` at all (i.e. an exact filename like `config.json`), it still works fine — it just becomes an exact-match regex `^config\.json$`.
- If you want case-insensitive matching (e.g. `Report.PDF` should still match `report_*.pdf`), you'd need to either add the `i` flag when actually using the regex at validation/query time, or lowercase both sides before testing — this generator itself doesn't embed flags since JS regex literals here are just strings.