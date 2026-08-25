Here's the approach: track a per-field "mode" (encoded vs decoded), let the icon toggle between them, and always force-encode to base64 right before submit — so no matter what the user typed or what state the field is displaying in, only base64 with padding ever reaches the backend.

**1. Add a state map in your component (`metadata-management.component.ts`)**

```ts
// fields that need base64 conversion
_base64Fields = ['Owners', 'Visitors', 'LimitedVisitors', 'ManualUpload'];

// tracks whether the currently displayed text is 'encoded' or 'decoded' per field
base64Mode: { [key: string]: 'encoded' | 'decoded' } = {
  Owners: 'decoded',
  Visitors: 'decoded',
  LimitedVisitors: 'decoded',
  ManualUpload: 'decoded'
};
```

**2. Toggle handler**

```ts
toggleBase64(field: string) {
  const control = this.metadataManagementForm.get(field);
  const current = control?.value ?? '';
  if (!current) return;

  if (this.base64Mode[field] === 'encoded') {
    // currently showing base64 -> decode it to view plain text
    try {
      const decoded = atob(current);
      control?.setValue(decoded);
      this.base64Mode[field] = 'decoded';
    } catch {
      // not valid base64, nothing to decode
    }
  } else {
    // currently showing plain text -> encode it to base64
    const encoded = btoa(current);
    control?.setValue(encoded);
    this.base64Mode[field] = 'encoded';
  }
}
```

**3. When loading existing metadata (edit mode)**, the values coming from the backend are already base64 — mark them as `'encoded'` right after you `setValue` them in `ngOnInit`:

```ts
this._base64Fields.forEach(f => this.base64Mode[f] = 'encoded');
```
(add this right after your `Object.keys(metadata).forEach(...)` block that populates the form in edit mode)

**4. Force-encode on submit** — this is the key part that guarantees the backend only ever gets base64, regardless of what's currently displayed:

```ts
onSubmitClick() {
  this.metadataManagementForm.markAllAsTouched();
  this.metadataManagementForm.markAsDirty();

  // ensure every base64 field is actually holding base64 before validation runs
  this._base64Fields.forEach(field => {
    const control = this.metadataManagementForm.get(field);
    const val = control?.value;
    if (val && this.base64Mode[field] !== 'encoded') {
      control?.setValue(btoa(val));
      this.base64Mode[field] = 'encoded';
    }
  });

  if (this.metadataManagementForm.status.toLowerCase() == "valid") {
    // ...rest of your existing submit logic
  }
}
```

**5. HTML — add a clickable icon inside the field** (repeat for Owners, Visitors, LimitedVisitors, ManualUpload):

```html
<div class="col-sm-3 d-flex flex-column">
  <mat-form-field>
    <mat-label>Owners</mat-label>
    <input matInput placeholder="Owners" formControlName="Owners" [readonly]="_metadataID == -1">
    <mat-icon matSuffix style="cursor:pointer"
      (click)="toggleBase64('Owners')"
      [title]="base64Mode['Owners'] === 'encoded' ? 'Show decoded value' : 'Convert to base64'">
      {{ base64Mode['Owners'] === 'encoded' ? 'visibility' : 'lock' }}
    </mat-icon>
  </mat-form-field>
  <div class="error" [class.show]="metadataManagementForm.get('Owners')?.hasError('base64')">
    Invalid base64 string
  </div>
</div>
```

Notes:

- You need `MatIconModule` imported in your module for `<mat-icon>` to work.
- Since your `base64Validator()` runs `atob()` on whatever is currently in the control, the field will show "Invalid base64 string" while it's in `'decoded'` (plain text) mode — that's expected; it clears once the user clicks the icon (or hits Submit, which auto-encodes).
- `btoa`/`atob` only handle Latin1/ASCII strings; if any of these values could contain non-ASCII characters (accented names, etc.), you'll need `btoa(unescape(encodeURIComponent(str)))` and the matching `decodeURIComponent(escape(atob(str)))` to avoid `InvalidCharacterError`.
- `[readonly]` on the input won't block the icon click, so users can still toggle view even in readonly mode — if you want to lock that down too, add `*ngIf="!isReadOnly"` around the icon or disable the click when `_metadataID == -1` isn't the condition you want.