// HealthCheck — POC: Health Check reconciliation, one-time batch run.
//
// For each row in the Requirements Template (template.xlsx), this program:
//   1. Looks inside that row's Source Folder.
//   2. Finds files matching the row's File Pattern (regex).
//   3. For each matching file, reads its size and Last-Write-Time directly
//      from the local filesystem.
//   4. Compares that against the template's expected Frequency Day /
//      Excluding Day, Expected Start/End Time, and Min/Max File Size.
//   5. Writes a pass/fail result per check to health_check_report.xlsx.
//
// Usage:
//   dotnet run --project HealthCheck
//   dotnet run --project HealthCheck -- --template ..\template.xlsx --output ..\health_check_report.xlsx
//
// No NAS paths, no .go file parsing, no database, no email — all local,
// all deferred to later phases. This proves the matching/comparison logic
// before wiring in real infrastructure.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace HealthCheckPOC
{
    internal class TemplateRow
    {
        public string ApplicationName = "";
        public string RecordType = "";
        public string FilePattern = "";
        public string FrequencyType = "";
        public string FrequencyDay = "";
        public string ExcludingDay = "";
        public string ExpectedStartTime = "";
        public string ExpectedEndTime = "";
        public string MinFileSizeKB = "";
        public string MaxFileSizeKB = "";
        public string SourceFolder = "";
        public string DestinationFolder = "";
    }

    internal class ResultRow
    {
        public string ApplicationName = "";
        public string RecordType = "";
        public string FilePattern = "";
        public string FileName = "";
        public string ReceivedDate = "";
        public string ReceivedTime = "";
        public string ReceivedDay = "";
        public string FileSizeKB = "";
        public string DayCheck = "";
        public string TimeCheck = "";
        public string SizeCheck = "";
        public string OverallStatus = "";
    }

    internal static class Program
    {
        private static readonly string[] RequiredColumns =
        {
            "Application Name", "Record Type", "File Pattern (Regex)",
            "Frequency Type", "Frequency Day", "Excluding Day",
            "Expected Start Time", "Expected End Time",
            "Min File Size (KB)", "Max File Size (KB)",
            "Source Folder", "Destination Folder"
        };

        private static int Main(string[] args)
        {
            string templatePath = "template.xlsx";
            string outputPath = "health_check_report.xlsx";

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--template" && i + 1 < args.Length) templatePath = args[++i];
                else if (args[i] == "--output" && i + 1 < args.Length) outputPath = args[++i];
            }

            if (!File.Exists(templatePath))
            {
                Console.Error.WriteLine($"Template not found: {templatePath}");
                return 1;
            }

            List<TemplateRow> rows;
            try
            {
                rows = ReadTemplate(templatePath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to read template: {ex.Message}");
                return 1;
            }

            if (rows.Count == 0)
            {
                Console.WriteLine("Template has no data rows. Nothing to check.");
                return 0;
            }

            var results = new List<ResultRow>();
            foreach (var row in rows)
            {
                results.AddRange(ProcessRow(row));
            }

            WriteReport(results, outputPath);
            Console.WriteLine($"Checked {rows.Count} template row(s). Wrote {results.Count} result row(s) to {outputPath}");
            return 0;
        }

        private static List<TemplateRow> ReadTemplate(string path)
        {
            var rows = new List<TemplateRow>();
            using var workbook = new XLWorkbook(path);

            IXLWorksheet ws = workbook.Worksheets.Contains("Requirements Template")
                ? workbook.Worksheet("Requirements Template")
                : workbook.Worksheets.First();

            var headerRow = ws.Row(1);
            var colIndex = new Dictionary<string, int>();
            int lastCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
            for (int c = 1; c <= lastCol; c++)
            {
                string header = headerRow.Cell(c).GetString().Trim();
                if (!string.IsNullOrEmpty(header)) colIndex[header] = c;
            }

            var missing = RequiredColumns.Where(h => !colIndex.ContainsKey(h)).ToList();
            if (missing.Count > 0)
                throw new Exception("Template is missing required column(s): " + string.Join(", ", missing));

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                var appCell = ws.Cell(r, colIndex["Application Name"]);
                if (appCell.IsEmpty()) continue; // skip blank rows

                var row = new TemplateRow
                {
                    ApplicationName = ws.Cell(r, colIndex["Application Name"]).GetString().Trim(),
                    RecordType = ws.Cell(r, colIndex["Record Type"]).GetString().Trim(),
                    FilePattern = ws.Cell(r, colIndex["File Pattern (Regex)"]).GetString().Trim(),
                    FrequencyType = ws.Cell(r, colIndex["Frequency Type"]).GetString().Trim(),
                    FrequencyDay = ws.Cell(r, colIndex["Frequency Day"]).GetString().Trim(),
                    ExcludingDay = ws.Cell(r, colIndex["Excluding Day"]).GetString().Trim(),
                    ExpectedStartTime = GetTimeString(ws.Cell(r, colIndex["Expected Start Time"])),
                    ExpectedEndTime = GetTimeString(ws.Cell(r, colIndex["Expected End Time"])),
                    MinFileSizeKB = ws.Cell(r, colIndex["Min File Size (KB)"]).GetString().Trim(),
                    MaxFileSizeKB = ws.Cell(r, colIndex["Max File Size (KB)"]).GetString().Trim(),
                    SourceFolder = ws.Cell(r, colIndex["Source Folder"]).GetString().Trim(),
                    DestinationFolder = ws.Cell(r, colIndex["Destination Folder"]).GetString().Trim(),
                };
                rows.Add(row);
            }

            return rows;
        }

        // Excel stores a time-only cell (like typing "1:00" directly) as XLDataType.TimeSpan,
        // a date+time cell as XLDataType.DateTime, and anything typed as plain text as a string.
        // Handle all three so the template is easy to fill in from Excel directly.
        private static string GetTimeString(IXLCell cell)
        {
            if (cell.IsEmpty()) return "";
            if (cell.DataType == XLDataType.DateTime)
            {
                DateTime dt = cell.GetDateTime();
                return dt.ToString("HH:mm", CultureInfo.InvariantCulture);
            }
            if (cell.DataType == XLDataType.TimeSpan)
            {
                TimeSpan ts = cell.GetTimeSpan();
                return ts.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
            }
            return cell.GetString().Trim();
        }

        private static HashSet<string> ParseDayList(string raw)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw)) return set;
            foreach (var part in raw.Split(','))
            {
                string d = part.Trim();
                if (d.Length == 0) continue;
                set.Add(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(d.ToLowerInvariant()));
            }
            return set;
        }

        private static TimeSpan? ParseTime(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            raw = raw.Trim();
            string[] formats = { "HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss", "h:mm tt", "h:mm:ss tt" };
            foreach (var fmt in formats)
            {
                if (DateTime.TryParseExact(raw, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return dt.TimeOfDay;
            }
            // Fallback: accept anything .NET itself considers a valid time span (e.g. "1:00:00").
            if (TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var ts))
                return ts;
            throw new FormatException($"Could not parse time value: '{raw}' (expected HH:mm, 24-hour)");
        }

        private static double? ParseSize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (double.TryParse(raw.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                return v;
            throw new FormatException($"Could not parse size value: '{raw}'");
        }

        private static List<ResultRow> ProcessRow(TemplateRow row)
        {
            var results = new List<ResultRow>();

            var expectedDays = ParseDayList(row.FrequencyDay);
            var excludingDays = ParseDayList(row.ExcludingDay);

            TimeSpan? startTime, endTime;
            double? minKb, maxKb;
            try
            {
                startTime = ParseTime(row.ExpectedStartTime);
                endTime = ParseTime(row.ExpectedEndTime);
                minKb = ParseSize(row.MinFileSizeKB);
                maxKb = ParseSize(row.MaxFileSizeKB);
            }
            catch (FormatException ex)
            {
                results.Add(NewErrorResult(row, $"ERROR ({ex.Message})"));
                return results;
            }

            if (string.IsNullOrWhiteSpace(row.SourceFolder) || !Directory.Exists(row.SourceFolder))
            {
                results.Add(NewErrorResult(row, $"ERROR (Source folder not found: {row.SourceFolder})"));
                return results;
            }

            Regex regex;
            try
            {
                regex = new Regex(row.FilePattern);
            }
            catch (Exception ex)
            {
                results.Add(NewErrorResult(row, $"ERROR (Invalid regex in File Pattern: {ex.Message})"));
                return results;
            }

            var matches = Directory.GetFiles(row.SourceFolder)
                .Where(f => regex.IsMatch(Path.GetFileName(f)))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (matches.Count == 0)
            {
                results.Add(NewErrorResult(row, "NO FILE RECEIVED"));
                return results;
            }

            foreach (var filePath in matches)
                results.Add(EvaluateFile(row, filePath, expectedDays, excludingDays, startTime, endTime, minKb, maxKb));

            return results;
        }

        private static ResultRow NewErrorResult(TemplateRow row, string status) => new ResultRow
        {
            ApplicationName = row.ApplicationName,
            RecordType = row.RecordType,
            FilePattern = row.FilePattern,
            OverallStatus = status,
        };

        private static ResultRow EvaluateFile(TemplateRow row, string filePath, HashSet<string> expectedDays,
            HashSet<string> excludingDays, TimeSpan? startTime, TimeSpan? endTime, double? minKb, double? maxKb)
        {
            DateTime lastWriteTime = File.GetLastWriteTime(filePath);
            string receivedDay = lastWriteTime.DayOfWeek.ToString();
            TimeSpan receivedTime = lastWriteTime.TimeOfDay;
            long sizeBytes = new FileInfo(filePath).Length;
            double sizeKb = sizeBytes / 1024.0;

            string dayCheck;
            if (excludingDays.Contains(receivedDay))
                dayCheck = "FAIL (excluded day)";
            else if (expectedDays.Count > 0 && !expectedDays.Contains(receivedDay))
                dayCheck = $"FAIL (got {receivedDay}, expected {string.Join("/", expectedDays.OrderBy(d => d))})";
            else
                dayCheck = "PASS";

            string timeCheck;
            if (startTime == null || endTime == null)
                timeCheck = "SKIPPED (no expected time set)";
            else if (receivedTime >= startTime.Value && receivedTime <= endTime.Value)
                timeCheck = "PASS";
            else
                timeCheck = $"FAIL (got {receivedTime:hh\\:mm}, expected {startTime.Value:hh\\:mm}-{endTime.Value:hh\\:mm})";

            string sizeCheck;
            if (minKb == null && maxKb == null)
            {
                sizeCheck = "SKIPPED (no size range set)";
            }
            else
            {
                double lo = minKb ?? 0;
                double hi = maxKb ?? double.PositiveInfinity;
                if (sizeKb >= lo && sizeKb <= hi)
                    sizeCheck = "PASS";
                else
                    sizeCheck = $"FAIL (got {sizeKb:F1} KB, expected {lo}-{(double.IsPositiveInfinity(hi) ? "\u221e" : hi.ToString(CultureInfo.InvariantCulture))} KB)";
            }

            bool overallPass = (dayCheck == "PASS" || dayCheck.StartsWith("SKIPPED"))
                && (timeCheck == "PASS" || timeCheck.StartsWith("SKIPPED"))
                && (sizeCheck == "PASS" || sizeCheck.StartsWith("SKIPPED"));

            return new ResultRow
            {
                ApplicationName = row.ApplicationName,
                RecordType = row.RecordType,
                FilePattern = row.FilePattern,
                FileName = Path.GetFileName(filePath),
                ReceivedDate = lastWriteTime.ToString("yyyy-MM-dd"),
                ReceivedTime = lastWriteTime.ToString("HH:mm:ss"),
                ReceivedDay = receivedDay,
                FileSizeKB = Math.Round(sizeKb, 1).ToString(CultureInfo.InvariantCulture),
                DayCheck = dayCheck,
                TimeCheck = timeCheck,
                SizeCheck = sizeCheck,
                OverallStatus = overallPass ? "PASS" : "FAIL",
            };
        }

        private static void WriteReport(List<ResultRow> results, string outputPath)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Health Check Report");

            string[] headers =
            {
                "Application Name", "Record Type", "File Pattern", "File Name",
                "Received Date", "Received Time", "Received Day", "File Size (KB)",
                "Frequency Day Check", "Time Window Check", "File Size Check", "Overall Status"
            };

            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
            }

            int r = 2;
            foreach (var res in results)
            {
                ws.Cell(r, 1).Value = res.ApplicationName;
                ws.Cell(r, 2).Value = res.RecordType;
                ws.Cell(r, 3).Value = res.FilePattern;
                ws.Cell(r, 4).Value = res.FileName;
                ws.Cell(r, 5).Value = res.ReceivedDate;
                ws.Cell(r, 6).Value = res.ReceivedTime;
                ws.Cell(r, 7).Value = res.ReceivedDay;
                ws.Cell(r, 8).Value = res.FileSizeKB;
                ws.Cell(r, 9).Value = res.DayCheck;
                ws.Cell(r, 10).Value = res.TimeCheck;
                ws.Cell(r, 11).Value = res.SizeCheck;

                var statusCell = ws.Cell(r, 12);
                statusCell.Value = res.OverallStatus;

                if (res.OverallStatus == "PASS")
                    statusCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#C6EFCE");
                else if (res.OverallStatus.StartsWith("FAIL"))
                    statusCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFC7CE");
                else if (res.OverallStatus == "NO FILE RECEIVED" || res.OverallStatus.StartsWith("ERROR"))
                    statusCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFEB9C");

                r++;
            }

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);
            if (r > 2)
                ws.RangeUsed().SetAutoFilter();

            workbook.SaveAs(outputPath);
        }
    }
}
