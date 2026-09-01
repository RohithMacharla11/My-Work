Good news: the Unicode-safe fix worked — no more silent failures, and now you get a proper caught error instead. But it's surfaced the real bug underneath, and it's different from what I guessed:

```
InvalidCharacterError: Failed to execute 'atob' on 'Window': 
The string to be decoded is not correctly encoded.
  at fromBase64 (...ts:270:20)
  at toggleBase64 (...ts:282:32)
```

This is the **decode** path throwing, not encode — `base64Mode['Owners']` thinks it's `'encoded'` (eye icon showing) so the click calls `atob()`, but the box actually contains plain text (`...ou=SRMS,ou=applications,dc=root`), which isn't valid base64. That's exactly why the "Invalid base64 string" validator error is showing too — the value really is plain text right now.

**Root cause:** `base64Mode[field]` only ever gets updated inside `toggleBase64()` itself. Nothing resets it when the input's value changes some other way — you typing directly into the box, pasting a corrected DN, or a picker/autocomplete calling `setValue()`. So the sequence is: click once → encodes fine, mode flips to `'encoded'` → you edit the text directly → mode is now stale but content is plain text again → next click tries to decode plain text → throws. From your side it looks random because the icon's state and the box's actual content have drifted apart.

**Fix** — make the mode flag follow the control's actual value instead of only being set by the toggle:

```typescript
// in ngOnInit, after metadataManagementForm is built
this._base64Fields.forEach(field => {
  this.metadataManagementForm.get(field)?.valueChanges.subscribe(() => {
    this.base64Mode[field] = 'decoded';
  });
});
```

Then make `toggleBase64`'s own `setValue` calls silent so they don't immediately trip that same listener and undo the flag you just set:

```typescript
toggleBase64(field: string) {
  const control = this.metadataManagementForm.get(field);
  const current = control?.value ?? '';
  if (!current) return;
  try {
    if (this.base64Mode[field] === 'encoded') {
      control?.setValue(this.fromBase64(current), { emitEvent: false });
      this.base64Mode[field] = 'decoded';
    } else {
      control?.setValue(this.toBase64(current), { emitEvent: false });
      this.base64Mode[field] = 'encoded';
    }
  } catch (e) {
    console.error(`toggleBase64 failed for ${field}:`, e);
  }
}
```

Do the same `{ emitEvent: false }` in the submit-time normalization loop:

```typescript
this._base64Fields.forEach(field => {
  const control = this.metadataManagementForm.get(field);
  const val = control?.value;
  if (val && this.base64Mode[field] !== 'encoded') {
    control?.setValue(this.toBase64(val), { emitEvent: false });
    this.base64Mode[field] = 'encoded';
  }
});
```

This leaves the explicit `_base64Fields.forEach(f => this.base64Mode[f] = 'encoded')` in your metadata-load block untouched and correct — it runs *after* the `Object.keys(metadata).forEach(...setValue...)` loop, so even though that loop's `setValue` calls will trigger the new listener and set everything to `'decoded'` momentarily, the explicit assignment right after still wins.

Small unrelated thing I noticed while in there: your `base64Mode` initializer has a key `ManualUpload`, but `_base64Fields` (and the `Members` form control it actually maps to) uses `'Members'` instead — so `base64Mode['Members']` starts `undefined`. Harmless today since `undefined !== 'encoded'` still routes correctly to encode, but worth aligning the key names so it doesn't bite you later.

Try that and click the toggle a few times in a row, including editing the box by hand between clicks — that's the sequence that should now stay in sync.