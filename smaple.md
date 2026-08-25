Yes — much simpler. Skip the subscriptions and snapshots entirely. Just compute what the regex *should* be from the current pattern, and compare it to what's actually in the RegEx field. If they don't match, show the error. No state tracking needed.

**1. Add a simple getter in the component**

```ts
get isFileNamePatternRegexOutOfSync(): boolean {
  const pattern = this.metadataManagementForm.get('FileNamePattern')?.value;
  const regex = this.metadataManagementForm.get('FileNamePatternRegEx')?.value;

  if (!pattern) return false; // nothing to compare against

  const expected = this.buildRegexFromPattern(pattern);
  return regex !== expected;
}

generateRegexFromFileNamePattern() {
  const raw = this.metadataManagementForm.get('FileNamePattern')?.value;
  if (!raw) return;
  this.metadataManagementForm.get('FileNamePatternRegEx')?.setValue(this.buildRegexFromPattern(raw));
}

private buildRegexFromPattern(raw: string): string {
  return raw
    .split(',')
    .map((p: string) => p.trim())
    .filter((p: string) => p.length > 0)
    .map((p: string) => {
      const escaped = p.replace(/[.+^${}()|[\]\\]/g, '\\$&');
      return `^${escaped.replace(/\*/g, '.*').replace(/\?/g, '.')}$`;
    })
    .join('|');
}
```

**2. HTML — just call the getter directly, no subscriptions, no ngOnInit changes needed**

```html
<div class="col-sm-6" *ngIf="isFileNamePatternRegexOutOfSync">
  <div class="error show">
    FileName Pattern and RegEx are out of sync — click the refresh icon to regenerate.
  </div>
</div>
```

That's it. Since it's a getter, Angular re-evaluates it every change detection cycle automatically as either field changes — no `valueChanges` subscriptions, no manual sync flags, and it works correctly for both new records and edit mode (since it's just comparing current values, not tracking history).