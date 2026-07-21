Found it — this is a mapping bug in `candidate.service.ts`, not the panel. The candidate model's `toYesNo()` helper converts `HrRoundOfferAccepted` from SharePoint back into your `Candidate` object, but it only recognizes `'Yes'`/`'No'` — anything else, including your new `'Pending'`, silently collapses to `null`. So the write to SharePoint may well be succeeding, but the moment the local candidate gets reset (via `ngOnChanges()` re-reading `this.candidate.hrRound.offerAccepted`, or on next fetch/refresh), `'Pending'` gets wiped back to `null` — which looks exactly like "I click it, save, and it's gone, have to click again."

## Fix — `candidate.service.ts`

Find the existing `toYesNo()`:
```ts
private toYesNo(v: string | null): YesNo {
  if (v === 'Yes') return 'Yes';
  if (v === 'No') return 'No';
  return null;
}
```

Add a **separate** mapper for offer-accepted specifically (don't touch `toYesNo` itself — it's still correctly used for `offerSent` and possibly elsewhere, and those should stay Yes/No/null only):

```ts
private toOfferAccepted(v: string | null): OfferAcceptedStatus {
  if (v === 'Yes') return 'Yes';
  if (v === 'No') return 'No';
  if (v === 'Pending') return 'Pending';
  return null;
}
```

Then in `mapCandidate()` / wherever `hrRound.offerAccepted` gets built, change:
```ts
offerAccepted: this.toYesNo(item.HrRoundOfferAccepted),
```
to:
```ts
offerAccepted: this.toOfferAccepted(item.HrRoundOfferAccepted),
```

(Import `OfferAcceptedStatus` from `candidate.model` at the top of the file alongside your other model imports.)

## One more thing to verify — the SharePoint column itself

If `HrRoundOfferAccepted` is a SharePoint **Choice** column with only "Yes"/"No" as valid options, the write of `'Pending'` will be **rejected by SharePoint** even after this code fix — that would explain the exact same symptom (looks unsaved) but for a completely different reason (server-side rejection, not client mapping).

Please check: List Settings → that column → confirm "Pending" is listed as one of the choices (or that it's a plain text/free-text field, not a restricted Choice field). If it's Choice-restricted, add "Pending" as a valid option there.

Try the code fix first — if it still doesn't persist after that, it's the SharePoint column restriction, and I'll help you fix the column definition next.