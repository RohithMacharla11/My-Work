Here are the exact edits — all in `HealthCheckEngine.cs` only.

## Change 1 — start of `Run()` method

Find:
```csharp
    public List<HealthCheckOutputRow> Run(List<ExtractRecord> extract, List<SizePartRecord> sizeParts)
    {
        var output = new List<HealthCheckOutputRow>();

        var appRecordGroups = extract
```

Replace with:
```csharp
    public List<HealthCheckOutputRow> Run(List<ExtractRecord> extract, List<SizePartRecord> sizeParts)
    {
        var output = new List<HealthCheckOutputRow>();

        // Build size lookups ONCE, up front. Doing a fresh .Where() scan over the whole
        // sizeParts list for every one of potentially thousands of scenarios is what was
        // making this run "forever" (scenarios x sizeParts comparisons). These two
        // dictionaries turn that into an O(1) lookup per scenario instead.
        var exactSizeIndex = new Dictionary<(string App, string Record, string FileName), List<double?>>();
        var groupSizeIndex = new Dictionary<(string App, string Record), List<double?>>();
        foreach (var sp in sizeParts)
        {
            // lower-cased keys so lookups stay case-insensitive, matching the original .Where() behaviour
            var app = sp.ApplicationName?.Trim().ToLowerInvariant() ?? "";
            var rec = sp.RecordType?.Trim().ToLowerInvariant() ?? "";
            var groupKey = (app, rec);
            if (!groupSizeIndex.TryGetValue(groupKey, out var groupList))
            {
                groupList = new List<double?>();
                groupSizeIndex[groupKey] = groupList;
            }
            groupList.Add(sp.FileSizeBytes);

            if (!string.IsNullOrWhiteSpace(sp.FileName))
            {
                var exactKey = (app, rec, sp.FileName.Trim().ToLowerInvariant());
                if (!exactSizeIndex.TryGetValue(exactKey, out var exactList))
                {
                    exactList = new List<double?>();
                    exactSizeIndex[exactKey] = exactList;
                }
                exactList.Add(sp.FileSizeBytes);
            }
        }

        var appRecordGroups = extract
```

## Change 2 — inside the pattern-bucket loop

Find:
```csharp
                var rowsInScenario = fileNames.SelectMany(fn => recordsByFileName[fn]).ToList();
                var row = BuildScenarioRow(appRecordGroup.Key.App, appRecordGroup.Key.Record, template, rowsInScenario, sizeParts);
                output.Add(row);
```

Replace with:
```csharp
                var rowsInScenario = fileNames.SelectMany(fn => recordsByFileName[fn]).ToList();
                var row = BuildScenarioRow(appRecordGroup.Key.App, appRecordGroup.Key.Record, template, rowsInScenario, exactSizeIndex, groupSizeIndex);
                output.Add(row);
```

## Change 3 — the "no file name" bucket line

Find:
```csharp
                var row = BuildScenarioRow(appRecordGroup.Key.App, appRecordGroup.Key.Record, "(no file name)", noName, sizeParts);
```

Replace with:
```csharp
                var row = BuildScenarioRow(appRecordGroup.Key.App, appRecordGroup.Key.Record, "(no file name)", noName, exactSizeIndex, groupSizeIndex);
```

## Change 4 — `BuildScenarioRow` method signature

Find:
```csharp
    private HealthCheckOutputRow BuildScenarioRow(string app, string recordType, string pattern,
        List<ExtractRecord> rows, List<SizePartRecord> sizeParts)
    {
```

Replace with:
```csharp
    private HealthCheckOutputRow BuildScenarioRow(string app, string recordType, string pattern,
        List<ExtractRecord> rows,
        Dictionary<(string App, string Record, string FileName), List<double?>> exactSizeIndex,
        Dictionary<(string App, string Record), List<double?>> groupSizeIndex)
    {
```

## Change 5 — inside `BuildScenarioRow`, the size-matching call

Find:
```csharp
        // --- 7: size matching ---
        var matchedSizes = MatchSizes(app, recordType, rows, sizeParts);
```

Replace with:
```csharp
        // --- 7: size matching ---
        var matchedSizes = MatchSizes(app, recordType, rows, exactSizeIndex, groupSizeIndex);
```

## Change 6 — the whole `MatchSizes` method (last method in the file)

Find:
```csharp
    private List<double?> MatchSizes(string app, string recordType, List<ExtractRecord> rows, List<SizePartRecord> sizeParts)
    {
        var fileNames = rows.Where(r => !string.IsNullOrWhiteSpace(r.Name)).Select(r => r.Name!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Primary match: exact ApplicationName + RecordType + FileName.
        var exact = sizeParts.Where(sp =>
            string.Equals(sp.ApplicationName?.Trim(), app, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(sp.RecordType?.Trim(), recordType, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(sp.FileName) &&
            fileNames.Contains(sp.FileName.Trim()))
            .ToList();

        if (exact.Count > 0)
            return exact.Select(sp => sp.FileSizeBytes).ToList();

        // Fallback: same App + RecordType, any file (SIZE_PARTS naming may not exactly match extract naming) -
        // still scoped tightly by App+RecordType so we never borrow sizes from an unrelated scenario.
        return sizeParts.Where(sp =>
            string.Equals(sp.ApplicationName?.Trim(), app, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(sp.RecordType?.Trim(), recordType, StringComparison.OrdinalIgnoreCase))
            .Select(sp => sp.FileSizeBytes)
            .ToList();
    }
}
```

Replace with:
```csharp
    private List<double?> MatchSizes(string app, string recordType, List<ExtractRecord> rows,
        Dictionary<(string App, string Record, string FileName), List<double?>> exactSizeIndex,
        Dictionary<(string App, string Record), List<double?>> groupSizeIndex)
    {
        var appKey = app.Trim().ToLowerInvariant();
        var recKey = recordType.Trim().ToLowerInvariant();
        var fileNames = rows.Where(r => !string.IsNullOrWhiteSpace(r.Name)).Select(r => r.Name!.Trim().ToLowerInvariant()).ToHashSet();

        // Primary match: exact ApplicationName + RecordType + FileName, via O(1) dictionary lookups
        // instead of scanning the whole sizeParts list per scenario (that full re-scan, repeated for
        // every scenario, was why a 650k-row run never finished).
        var exact = new List<double?>();
        foreach (var fn in fileNames)
            if (exactSizeIndex.TryGetValue((appKey, recKey, fn), out var sizes))
                exact.AddRange(sizes);

        if (exact.Count > 0)
            return exact;

        // Fallback: same App + RecordType, any file (SIZE_PARTS naming may not exactly match extract naming) -
        // still scoped tightly by App+RecordType so we never borrow sizes from an unrelated scenario.
        return groupSizeIndex.TryGetValue((appKey, recKey), out var groupSizes)
            ? groupSizes
            : new List<double?>();
    }
}
```

That's every change — all six are in the one file, `HealthCheckAutomation.Core/Engine/HealthCheckEngine.cs`. Save, **Ctrl+F5**. This should finish in well under a minute now instead of hanging. If it still runs long, send me the console output and we'll look at what's making the scenario count so large in the first place.