Fair — here are the exact edits, file by file.

## 1. `DateTimeExtractor.cs`

Find the `FindAdjacentTime` method and the `IsValidTime` method right after it (near the bottom of the file). Replace **both** with this:

```csharp
    private static TimeOnly? FindAdjacentTime(string[] segments, int dateSegmentIndex)
    {
        foreach (var idx in new[] { dateSegmentIndex + 1, dateSegmentIndex - 1 })
        {
            if (idx < 0 || idx >= segments.Length) continue;
            var seg = segments[idx];

            var m6 = TimeToken6.Match(seg);
            if (m6.Success)
            {
                var h = int.Parse(m6.Groups[1].Value);
                var mi = int.Parse(m6.Groups[2].Value);
                var se = int.Parse(m6.Groups[3].Value);
                if (IsValidTime(h, mi, se)) return new TimeOnly(h, mi, se);
            }

            var m4 = TimeToken4.Match(seg);
            if (m4.Success)
            {
                var h = int.Parse(m4.Groups[1].Value);
                var mi = int.Parse(m4.Groups[2].Value);
                if (IsValidTime(h, mi, 0)) return new TimeOnly(h, mi);
            }
        }
        return null;
    }

    private static bool IsValidTime(int h, int m, int s) =>
        h is >= 0 and <= 23 && m is >= 0 and <= 59 && s is >= 0 and <= 59;
```

That replaces the old `FindAdjacentTime` + old `IsValidTime(string hh, string mm)` — delete the old versions of both, paste this in their place.

## 2. `HealthCheckEngine.cs`

**Change A** — find this block near the top of `BuildScenarioRow`:

```csharp
        // --- 1+2+3: extract + resolve date/time per row ---
        var extractions = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Name))
            .Select(r => (Record: r, Extraction: _dateExtractor.Extract(r.Name!)))
            .ToList();
```

Replace with:

```csharp
        // --- 1+2+3: extract + resolve date/time per row ---
        var extractions = new List<(ExtractRecord Record, Models.FileNameExtraction Extraction)>();
        int extractionFailures = 0;
        foreach (var r in rows.Where(r => !string.IsNullOrWhiteSpace(r.Name)))
        {
            try
            {
                extractions.Add((r, _dateExtractor.Extract(r.Name!)));
            }
            catch
            {
                extractionFailures++;
                extractions.Add((r, new Models.FileNameExtraction { FileName = r.Name! }));
            }
        }
```

**Change B** — a few lines further down, find:

```csharp
        foreach (var (record, extraction) in extractions)
        {
            var final = _formatResolver.ResolveFinal(extraction, preferredFormat);
            if (final != null)
```

Replace with:

```csharp
        foreach (var (record, extraction) in extractions)
        {
            Models.DateCandidate? final = null;
            try
            {
                final = _formatResolver.ResolveFinal(extraction, preferredFormat);
            }
            catch
            {
                extractionFailures++;
            }

            if (final != null)
```

**Change C** — further down still, find this single line:

```csharp
        if (unparsed > 0)
            row.AddIssue($"{unparsed} of {row.TotalFilesInGroup} file(s) had no usable date (file name or fallback columns) - excluded from cadence/time/excluding-day calculations.");
```

Add one new line right after it:

```csharp
        if (unparsed > 0)
            row.AddIssue($"{unparsed} of {row.TotalFilesInGroup} file(s) had no usable date (file name or fallback columns) - excluded from cadence/time/excluding-day calculations.");
        if (extractionFailures > 0)
            row.AddIssue($"{extractionFailures} file name(s) hit a parsing error (malformed/unexpected token) and were treated as unparsed rather than crashing the run.");
```

That's every change. Save both files, Ctrl+F5.