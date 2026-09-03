# Master Prompt — Health Check Frequency Field Extraction

You are an expert data analyst. You are given two files and must fill in 5 empty
columns in the first file, using evidence from the second file. Accuracy and
completeness matter more than speed — **no row, column, or file may be silently
dropped or guessed without being flagged.**

## Input Files

**File 1 — Requirements Template** (~100+ rows). Each row is a unique
combination of:
- `Application Name`
- `Record Type`
- `File pattern` — a **regex**, not a literal filename (e.g.
  `^AMLUSSAR_.+(_[0-9]+)*`, `BWise_CIT-\d{4}-CIB-Review_Name-RW*`)

Together, `Application Name + Record Type + File pattern` form the unique key.
Five columns are currently empty and must be filled:
`Frequency Type`, `Frequency Day`, `Expected start time`, `Expected end time`,
`Excluding Day`.
(`Minimum file size` / `Maximum file size` are out of scope — do not touch them.)

**File 2 — Extract** (500,000+ rows). Columns include `Applicationname`,
`RecordType`, `Container Key`, `Document Key`, `Name` (the **actual filename**,
not a regex), `Reportdate`, `ReportdateCalc`, `Created Date`, and others.
`Reportdate`/`ReportdateCalc` are **only accurate for some rows** — do not
trust them as the primary source of truth. The filename (`Name`) is the
primary source for date/time.

## Step 1 — Match every extract row to its requirement row

For each Requirements Template row:
1. Filter Extract rows where `Applicationname` = `Application Name` AND
   `RecordType` = `Record Type`.
2. Within that subset, keep only rows where `Name` matches the row's `File
   pattern` regex (use `search`, not a strict `fullmatch`, unless the pattern
   is anchored with `^...$`; compile carefully — some patterns use `\d{n}`,
   `.*`, and bare `*` inconsistently, so validate each compiles before using
   it).

**Mandatory audit trail:**
- Every Requirements row with **zero** matches → flag it (`No matching files
  found`).
- Every Extract row that matches **zero** requirement patterns → flag it as
  unmatched (`Orphaned file — no requirement pattern matched`). Do not discard
  it silently.
- Log the total counts so it's obvious nothing was lost: `(extract rows in)
  == (matched) + (orphaned)`.

## Step 2 — Extract the real date & time from the filename

`Reportdate`/`ReportdateCalc` are backups only. Parse the date/time primarily
from `Name`, because formats vary a lot. Reference examples actually seen in
this data:

| Filename | Notes |
|---|---|
| `AMLUSSAR_EXTERNAL Submission Accepted SX19-00006149_08262019_14001009.msg` | `08262019` = MMDDYYYY (Aug 26 2019); trailing `14001009` = time-like block right before the extension |
| `AMLUSSAR_C2676071 Confirmation_08262019_14001012.pdf` | same App/RecordType, same date, trailing time block varies slightly between files in the same batch (batch effect — see Step 3) |
| `DTSFTP_03312026_03302213.CPAR1.S12343-14237-033026-070030` | **Resolved:** the true ingestion timestamp is the block immediately before `.CPAR1` → `03302213` = `MMDD` (03/30) + `HHMM` (22:13); borrow the year from the preceding block (`03312026` → 2026). Ignore the later `033026-070030` segment after `.CPAR1` — it is not the ingestion timestamp. |
| `CAT_15794_BNPS_20210325_BNEWOPT210325LQDPT_OrderEvents_000008.json_03262021_10005198.bz2` | `20210325` = YYYYMMDD embedded mid-name (likely a report/business date); `03262021_10005198` right before `.bz2` = MMDDYYYY + time — **the one closest to the extension is the ingestion timestamp we care about** |
| `ATS_425_20250805_08062025_13045273.csv.bz2` | same double-date pattern: `20250805` (YYYYMMDD) mid-name, `08062025_13045273` (MMDDYYYY + time) before the extension |
| `CIRO_20241230_R0MUWSFPU8MPRO8K5P83_BNPPARIBAS_DEBT_29062026_031342.csv` | `20241230` = YYYYMMDD mid-name; `29062026` = **DDMMYYYY** (day=29 proves it can't be MMDD) — this source uses a different date convention than the others; `031342` = HHMMSS before `.csv` |

**Parsing rules:**
1. Scan the filename right-to-left. Prioritize date/time tokens found in the
   last 1–3 delimiter-separated segments before the final file extension —
   that block is almost always the ingestion/processing timestamp, which is
   what we need for scheduling.
2. Recognize date tokens of length 6 or 8 digits. Try, in this order:
   `MMDDYYYY` → `YYYYMMDD` → `DDMMYYYY` → `MMDDYY` → `DDMMYY`, and validate
   (month 1–12, day 1–31 for the actual month). If more than one format
   parses validly, **do not silently pick one** — check whether other rows
   from the *same* `Application Name` already resolved unambiguously (e.g. a
   day value >12 proved DDMMYYYY for CIRO); apply that same format
   consistently within the same application/pattern group. If still
   ambiguous, flag the row.
3. Recognize time tokens adjacent to the date: 4 digits (`HHMM`), 6 digits
   (`HHMMSS`), or 8 digits (commonly `HHMM` + a 4-digit sequence/sub-second
   value — validate `HH` 0–23 and `MM` 0–59 on the first 4 digits; the
   trailing digits are not reliable as seconds and can be ignored for
   scheduling purposes).
4. If a filename contains **two** date-like blocks (a "report date" earlier
   in the name and a "processing timestamp" near the extension), use the one
   nearest the extension as the arrival time. Optionally keep the earlier one
   as a secondary `report_date` field for QA, but it is not what drives
   Frequency/Expected time.
5. If no valid date/time can be parsed from the filename at all, fall back to
   `Created Date` or `Reportdate`, and explicitly flag the row as `Date
   inferred from extract column, not filename`.
6. Some filenames don't end in a normal extension but instead have a fixed
   literal anchor segment partway through (e.g. `.CPAR1` in the DTSFTP
   pattern). For those, apply the same right-to-left priority logic anchored
   to that literal segment instead of the file extension: the date/time block
   **immediately preceding the anchor** is the ingestion timestamp. If that
   block lacks a year, borrow the year from the nearest preceding date block
   in the same filename. Ignore any date/time segments that appear *after*
   the anchor.

## Step 3 — Adjust for the ingestion timer job

Files are picked up by a timer job that runs every 30 minutes, so the
timestamp baked into the filename is **rounded up** to the job's run time —
it can be up to ~29 minutes later than the file's true arrival. When you
later compute `Expected start time` / `Expected end time`:
- Treat each observed filename timestamp as an **upper bound** on arrival.
- Round observed times down to the nearest prior :00/:30 to get a
  conservative earliest-possible-arrival estimate, and use the raw observed
  times as the latest-possible bound.
- This is why files in the same batch often share the same `HHMM` prefix
  (e.g. multiple `AMLUSSAR` files all landing around `1400`) even though
  their underlying arrival times differed slightly.

## Step 4 — Derive the frequency fields per requirement group

For each Requirements row, using its matched Extract rows' resolved dates:
1. Sort the distinct dates.
2. Classify cadence from the gaps between consecutive dates:
   - ~1 day apart, consistently → **Daily**
   - ~7 days apart, or always the same weekday → **Weekly** (set
     `Frequency Day` = that weekday)
   - same day-of-month each cycle, or always the last/first business day →
     **Monthly** (set `Frequency Day` = day number, or "Last business day" /
     "First business day")
   - multiple distinct times on the *same* day, repeating → **Multiple times
     daily (Intraday)**
   - no consistent gap → **Ad-hoc/Irregular** — do not force it into a
     cadence it doesn't have
3. `Excluding Day`: within an otherwise-daily pattern, note which weekdays
   never appear (typically Saturday/Sunday → "Weekends"). If specific
   calendar dates are missing across multiple years in a way that lines up
   with public holidays, note that too — but don't guess a holiday calendar
   you can't verify from the data.
4. `Expected start time` / `Expected end time`: the earliest and latest
   adjusted (per Step 3) times observed across the group, as a window. If the
   group is intraday with several clearly separate batch windows, list each
   window rather than collapsing them into one misleadingly wide range.

## Step 5 — Output

1. Fill the 5 columns into the Requirements Template — do not alter any
   other column.
2. Produce a companion **audit sheet** with:
   - Requirements rows with 0 matches
   - Extract rows that matched 0 requirement patterns (orphaned)
   - Every row where date/time was ambiguous, fell back to a non-filename
     source, or required a judgment call
3. Row/column counts before and after must reconcile — state them explicitly.

## Hard rules

- Never drop a row silently. Every unmatched or ambiguous case must appear in
  the audit sheet, not just disappear.
- Never assume a single global date format. Confirm it per
  application/pattern group, using validation (e.g. day > 12 proves DDMMYYYY).
- Prefer the filename over `Reportdate`/`ReportdateCalc` for date/time, only
  falling back when the filename truly has no parseable date.
- Treat filename timestamps as timer-job-rounded, not exact arrival times.

## Resolved decisions

1. **DTSFTP-style pattern**: the true ingestion timestamp is the date/time
   block immediately before the `.CPAR1` anchor, not the first block (see
   table above and Step 2, rule 6).
2. **No master list** exists mapping `Application Name` → date convention.
   Infer the format per application/pattern group purely from the data, as
   described in Step 2, rule 2.
3. **No public holiday checking.** Most patterns are expected to resolve to
   **Weekly** cadence — treat that as the common case, and keep
   `Excluding Day` at the weekday level only (e.g. "Weekends"), never a
   holiday calendar.
