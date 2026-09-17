# Health Check Reconciliation — Logic Specification

This document describes the complete logic of a file health-check
reconciliation tool, independent of any programming language. It has
been implemented once in Python and once in C#/.NET; both follow this
exact spec. Give this document to any AI or developer and they should
be able to reproduce the same behavior in any language.

## 1. Purpose

Applications/teams are expected to deliver files into specific local
folders on a schedule (which day(s) of the week, what time window,
what file size range). This tool performs a **one-time batch check**:
for each expected application/file-type, look in its folder, find any
matching file(s), and report whether what arrived matches what was
expected — without sending emails or doing anything ongoing. It is a
proof-of-concept: no database, no network/NAS paths, no parsing of any
in-file date marker — the only source of truth for "when was this
file received" is the file's own filesystem **last-modified
timestamp**.

## 2. Inputs

### 2.1 The Requirements Template (a spreadsheet, one row = one rule)

One row per **Application Name + Record Type + File Pattern**
combination. Required columns, with exact header text:

| Column | Type | Meaning |
|---|---|---|
| `Application Name` | text | Name of the sending application/team |
| `Record Type` | text | Record type for that application |
| `File Pattern (Regex)` | regex string | Pattern the expected filename(s) must match |
| `Frequency Type` | text | Daily / Weekly / Monthly — informational only, not used in checks |
| `Frequency Day` | comma-separated weekday names, or blank | Day(s) the file is expected on. Blank = skip this check entirely |
| `Excluding Day` | comma-separated weekday names, or blank | Day(s) that must NEVER receive a file, regardless of Frequency Day |
| `Expected Start Time` | `HH:MM` 24-hour, or blank | Earliest acceptable arrival time |
| `Expected End Time` | `HH:MM` 24-hour, or blank | Latest acceptable arrival time |
| `Min File Size (KB)` | number, or blank | Minimum acceptable file size |
| `Max File Size (KB)` | number, or blank | Maximum acceptable file size |
| `Source Folder` | path | Local folder this application's files land in |
| `Destination Folder` | path | Where files get moved after processing — **recorded only, not used in check logic in this POC** |

A row is skipped entirely if `Application Name` is blank (treated as a
blank/filler row).

### 2.2 The filesystem

For each template row's `Source Folder`, the tool looks at whatever
files are physically present there right now. No subfolders are
traversed — only files directly inside that folder.

## 3. Processing algorithm

For each row in the template, in order:

```
1. Parse Frequency Day and Excluding Day into two sets of weekday
   names (case-insensitive, e.g. "Monday", "Tuesday", ...). Empty
   string → empty set.

2. Parse Expected Start Time and Expected End Time into time-of-day
   values. Empty → null (meaning "skip the time check").

3. Parse Min File Size (KB) and Max File Size (KB) into numbers.
   Empty → null (meaning "no limit on that side").
   → If any of steps 1–3 fail to parse (bad format), record ONE
     result row for this template row with:
       Overall Status = "ERROR (<parse error message>)"
     and move to the next template row.

4. Check Source Folder exists on disk.
   → If not, record ONE result row with:
       Overall Status = "ERROR (Source folder not found: <path>)"
     and move to the next template row.

5. Compile File Pattern as a regular expression.
   → If it's invalid regex syntax, record ONE result row with:
       Overall Status = "ERROR (Invalid regex in File Pattern: <error>)"
     and move to the next template row.

6. List every file directly inside Source Folder (not subfolders).
   Keep only files whose FILENAME matches the compiled regex
   (a "search" / "contains" match, not necessarily full-string).

7. If zero files matched:
     Record ONE result row with Overall Status = "NO FILE RECEIVED"
     (all other result fields blank), then move to the next
     template row.

8. If one or more files matched:
     Run step 9 (below) for EACH matching file, producing one result
     row per file. So one template row can produce multiple result
     rows if multiple files match.
```

### Step 9 — evaluating a single matched file

```
received_datetime = file's filesystem last-modified timestamp
received_day       = weekday name of received_datetime (e.g. "Monday")
received_time      = time-of-day part of received_datetime
size_kb             = file size in bytes ÷ 1024

# --- Day check ---
if received_day is in Excluding Day set:
    day_check = "FAIL (excluded day)"
elif Frequency Day set is non-empty AND received_day NOT in it:
    day_check = "FAIL (got <received_day>, expected <sorted Frequency Day list joined by '/'>)"
else:
    day_check = "PASS"
# Note: the excluded-day check runs FIRST and wins even if the day
# also happens to be in Frequency Day — exclusion always takes
# priority.

# --- Time window check ---
if Expected Start Time is null OR Expected End Time is null:
    time_check = "SKIPPED (no expected time set)"
elif Expected Start Time <= received_time <= Expected End Time (inclusive both ends):
    time_check = "PASS"
else:
    time_check = "FAIL (got <received_time HH:MM>, expected <start HH:MM>-<end HH:MM>)"

# --- File size check ---
if Min File Size (KB) is null AND Max File Size (KB) is null:
    size_check = "SKIPPED (no size range set)"
else:
    lo = Min File Size (KB), or 0 if null
    hi = Max File Size (KB), or +infinity if null
    if lo <= size_kb <= hi (inclusive both ends):
        size_check = "PASS"
    else:
        size_check = "FAIL (got <size_kb to 1 decimal> KB, expected <lo>-<hi or '∞'> KB)"

# --- Overall status ---
# PASS requires ALL THREE checks to be either PASS or SKIPPED.
# A single FAIL on any one check fails the whole row, regardless of
# the other two.
overall = "PASS" if (day_check is PASS or SKIPPED)
              and (time_check is PASS or SKIPPED)
              and (size_check is PASS or SKIPPED)
          else "FAIL"
```

## 4. Output (the report)

One spreadsheet, one row per result, with these exact columns in this
order:

```
Application Name | Record Type | File Pattern | File Name |
Received Date | Received Time | Received Day | File Size (KB) |
Frequency Day Check | Time Window Check | File Size Check | Overall Status
```

- `Received Date` = `YYYY-MM-DD`, `Received Time` = `HH:MM:SS` (both
  derived from the file's last-modified timestamp). Blank for
  `NO FILE RECEIVED` / `ERROR` rows (nothing was evaluated).
- `File Size (KB)` rounded to 1 decimal place. Blank for
  `NO FILE RECEIVED` / `ERROR` rows.
- `Overall Status` cell should be visually color-coded when the output
  format supports it: green = PASS, red = FAIL, yellow = NO FILE
  RECEIVED or ERROR. This is cosmetic, not required for correctness.

## 5. Explicit non-goals for this POC (do not implement unless asked)

- No reading from a `.go` file or parsing any date embedded in a
  filename — the filesystem's own last-modified timestamp is the only
  timing source.
- No NAS/network paths — local folder paths only.
- No database — the template is a flat spreadsheet.
- No scheduling/ongoing monitoring — this is a single batch pass that
  runs once when invoked and then exits.
- No email or any other notification — the spreadsheet report is the
  only output.
- `Destination Folder` is read from the template and carried into
  memory but never used in any check or file-move operation.

## 6. Edge cases a correct implementation must handle

- Template rows with a blank `Application Name` are silently skipped
  (not an error).
- Multiple files matching the same row's pattern → multiple result
  rows for that one template row, not just the first/last match.
- A day that is in BOTH `Frequency Day` and `Excluding Day` for the
  same row → treated as excluded (exclusion wins).
- `Frequency Day` blank → day check always passes regardless of which
  day the file arrived (frequency-day check is effectively off), but
  `Excluding Day` (if set) still applies independently.
- Time and size checks are independently skippable (blank = no
  constraint on that dimension) without affecting the other checks.
- Time comparisons are inclusive on both ends (arriving exactly at
  the start or end time passes).
- Size comparisons are inclusive on both ends.
