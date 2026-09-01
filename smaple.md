Got it — starting completely from scratch, original code, nothing changed. Here's the whole fix, two small pieces, nothing else touched:

**1. Replace `toggleBase64` with this (adds try/catch to the encode side too, so it never silently freezes on an edge case):**

```typescript
toggleBase64(field: string) {
  const control = this.metadataManagementForm.get(field);
  const current = control?.value ?? '';
  if (!current) return;

  try {
    if (this.base64Mode[field] === 'encoded') {
      control?.setValue(atob(current));
      this.base64Mode[field] = 'decoded';
    } else {
      control?.setValue(btoa(current));
      this.base64Mode[field] = 'encoded';
    }
  } catch (e) {
    console.error(`toggleBase64 failed for ${field}:`, e);
  }
}
```

**2. Add one new tiny method:**

```typescript
onBase64FieldEdited(field: string) {
  this.base64Mode[field] = 'decoded';
}
```

**3. In the template, add `(input)="onBase64FieldEdited('Owners')"` to the Owners `<input>`:**

```html
<input matInput placeholder="Owners" formControlName="Owners" [readonly]="_metadataID == -1"
  (input)="onBase64FieldEdited('Owners')">
```

(Do the same on the other four inputs too — `Visitors`, `LimitedVisitors`, `LegalHoldVisitors`, `Members` — same latent bug lives there, it just hasn't bitten you yet.)

That's the whole thing. **Why this actually fixes it:** the toggle button decides encode-vs-decode based on `base64Mode[field]`, but nothing was ever resetting that flag when you typed or pasted directly into the box. So it went stale — it still said `'encoded'` even after you'd overwritten the box with plain text — and the button then quietly tried to decode plain text and did nothing. Step 3 fixes that at the source: any time you actually type or paste into the field, the flag snaps back to `'decoded'` immediately, so the button always knows what's really in the box.

Nothing here touches `valueChanges`, `emitEvent`, or any other control — just this one method and one template attribute, so it can't ripple into the other fields' behavior.

Give that a try and let me know how it goes.