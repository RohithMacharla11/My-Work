You're right, and that's a real design flaw — my fix assumed "you typing = plain text," but you also need "I'm pasting in a value that's already base64." A flag that just tracks "was this edited" can never tell those two apart. The actual fix is to stop tracking a flag at all, and instead **look at what's actually in the box** every time you click — decide "is this valid base64?" fresh, each time, rather than trusting a stored state that can go stale or guess wrong.

**1. Add this helper — checks if a string is genuinely valid base64 (not just "looks like text"):**

```typescript
isValidBase64(value: string): boolean {
  if (!value) return false;
  if (!/^[A-Za-z0-9+/]*={0,2}$/.test(value)) return false;
  if (value.length % 4 !== 0) return false;
  try {
    return btoa(atob(value)) === value; // round-trip check
  } catch {
    return false;
  }
}
```

**2. Replace `toggleBase64` — decide encode-vs-decode from content, not a flag:**

```typescript
toggleBase64(field: string) {
  const control = this.metadataManagementForm.get(field);
  const current = control?.value ?? '';
  if (!current) return;

  try {
    if (this.isValidBase64(current)) {
      control?.setValue(atob(current));   // it's base64 -> show plain text
    } else {
      control?.setValue(btoa(current));   // it's plain text -> encode it
    }
  } catch (e) {
    console.error(`toggleBase64 failed for ${field}:`, e);
  }
}
```

**3. In the template, swap `base64Mode['Owners']` for a live check** (do this for all 5 fields):

```html
[ngClass]="isValidBase64(metadataManagementForm.get('Owners')?.value) ? 'bi-eye' : 'bi-arrow-repeat'"
[title]="isValidBase64(metadataManagementForm.get('Owners')?.value) ? 'Show decoded value' : 'Convert to base64'"
```

**4. Remove the `(input)="onBase64FieldEdited(...)"` I had you add last time** — not needed anymore, and remove/ignore the `base64Mode` property entirely. There's no more state to go stale, so there's nothing left to get out of sync.

Now: paste an already-encoded value in → icon shows "eye" (it's correctly recognized as base64) → click decodes it. Type a DN or plain text in → icon shows "convert" → click encodes it. Every click just asks "is this base64 right now?" instead of trusting a memory of what happened last time.

One honest limitation: if plain text *happens* to be made only of base64-safe characters and its length is a multiple of 4 (rare for a DN with commas/`=` as separators, but possible for a short alphanumeric name), it'll be treated as already-encoded. That's an inherent ambiguity with any implicit approach — the only way around it entirely would be an explicit "this is base64" checkbox, which is more UI than you probably want.

Give this a shot — it should hold up for both directions now since there's no flag left to drift out of sync.