using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace ScholarshipManagementAPI.Helper.Utilities
{
    internal sealed record ParsedAcademicResultRow(
        int RowNumber,
        string? AcademicRegistrationId,
        string? CourseCode,
        string? Result,
        string? SemesterGpa,
        string? InternalNote,
        List<string> Errors);

    internal sealed record ParsedAcademicResultsWorkbook(
        List<ParsedAcademicResultRow> Rows,
        List<string> FileErrors);

    internal static class AcademicResultsWorkbook
    {
        public const int MaxFileBytes = 5 * 1024 * 1024;
        public const int MaxExpandedBytes = 20 * 1024 * 1024;
        public const int MaxDataRows = 500;
        private static readonly string[] RequiredHeaders =
        {
            "AcademicRegistrationId", "CourseCode", "Result", "SemesterGpa", "InternalNote"
        };

        public static ParsedAcademicResultsWorkbook Parse(Stream input)
        {
            var rows = new List<ParsedAcademicResultRow>();
            var errors = new List<string>();
            try
            {
                using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
                if (archive.Entries.Count > 100)
                    return new ParsedAcademicResultsWorkbook(rows, new() { "The workbook contains too many package entries." });

                long expanded = 0;
                foreach (var entry in archive.Entries)
                {
                    expanded += entry.Length;
                    if (expanded > MaxExpandedBytes)
                        return new ParsedAcademicResultsWorkbook(rows, new() { "The expanded workbook exceeds the allowed size." });
                }

                var workbook = ReadXml(archive, "xl/workbook.xml");
                var relationships = ReadXml(archive, "xl/_rels/workbook.xml.rels");
                var sheet = workbook.Descendants().FirstOrDefault(x => x.Name.LocalName == "sheet" &&
                    (string?)x.Attribute("name") == "Results");
                if (sheet == null)
                    return new ParsedAcademicResultsWorkbook(rows, new() { "The workbook must contain a worksheet named Results." });

                var relationshipId = sheet.Attributes().FirstOrDefault(x => x.Name.LocalName == "id")?.Value;
                var relationship = relationships.Descendants().FirstOrDefault(x =>
                    (string?)x.Attribute("Id") == relationshipId);
                var target = (string?)relationship?.Attribute("Target");
                if (string.IsNullOrWhiteSpace(target) || target.Contains("..", StringComparison.Ordinal))
                    return new ParsedAcademicResultsWorkbook(rows, new() { "The Results worksheet relationship is invalid." });
                var worksheetPath = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target.TrimStart('/');
                var worksheet = ReadXml(archive, worksheetPath.Replace('\\', '/'));
                var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
                var sharedStrings = sharedStringsEntry == null
                    ? new List<string>()
                    : ReadXml(archive, "xl/sharedStrings.xml").Descendants()
                        .Where(x => x.Name.LocalName == "si")
                        .Select(x => string.Concat(x.Descendants().Where(t => t.Name.LocalName == "t").Select(t => t.Value)))
                        .ToList();

                var sheetRows = worksheet.Descendants().Where(x => x.Name.LocalName == "row").ToList();
                if (sheetRows.Count == 0)
                    return new ParsedAcademicResultsWorkbook(rows, new() { "The Results worksheet is empty." });
                var headerIndex = -1;
                Dictionary<int, string>? headerValues = null;
                for (var i = 0; i < Math.Min(sheetRows.Count, 20); i++)
                {
                    var values = ReadRow(sheetRows[i], sharedStrings, out var headerFormula);
                    if (headerFormula) continue;
                    if (Enumerable.Range(0, RequiredHeaders.Length)
                        .All(column => string.Equals(GetColumn(values, column), RequiredHeaders[column], StringComparison.Ordinal)))
                    {
                        headerIndex = i;
                        headerValues = values;
                        break;
                    }
                }
                if (headerIndex < 0 || headerValues == null)
                    return new ParsedAcademicResultsWorkbook(rows, new() { "The Results worksheet must include the required five headers in columns A–E within its first 20 rows." });
                if (headerValues.Any(x => x.Key >= RequiredHeaders.Length && !string.IsNullOrWhiteSpace(x.Value)))
                    return new ParsedAcademicResultsWorkbook(rows, new() { "The Results header row must contain only the five documented columns." });
                for (var i = 0; i < RequiredHeaders.Length; i++)
                {
                    if (!string.Equals(GetColumn(headerValues, i), RequiredHeaders[i], StringComparison.Ordinal))
                        errors.Add($"Header column {i + 1} must be '{RequiredHeaders[i]}'.");
                }
                if (errors.Count > 0) return new ParsedAcademicResultsWorkbook(rows, errors);

                var dataSheetRows = sheetRows.Skip(headerIndex + 1).ToList();
                var lastDataRowIndex = dataSheetRows.FindLastIndex(row =>
                {
                    var values = ReadRow(row, sharedStrings, out _);
                    return values.Values.Any(value => !string.IsNullOrWhiteSpace(value));
                });
                if (lastDataRowIndex < 0)
                    return new ParsedAcademicResultsWorkbook(rows, new() { "The workbook contains no result rows." });
                if (lastDataRowIndex + 1 > MaxDataRows)
                    return new ParsedAcademicResultsWorkbook(rows, new() { $"The workbook cannot contain more than {MaxDataRows} result rows." });

                for (var dataIndex = 0; dataIndex <= lastDataRowIndex; dataIndex++)
                {
                    var row = dataSheetRows[dataIndex];
                    var rowNumber = int.TryParse((string?)row.Attribute("r"), out var parsedNumber) ? parsedNumber : rows.Count + 2;
                    var rowErrors = new List<string>();
                    var values = ReadRow(row, sharedStrings, out var hasFormula);
                    if (hasFormula) rowErrors.Add("Formula cells are not allowed.");
                    if (values.Any(x => x.Key >= RequiredHeaders.Length && !string.IsNullOrWhiteSpace(x.Value)))
                        rowErrors.Add("Unexpected data was found outside the five documented columns.");
                    var fields = Enumerable.Range(0, RequiredHeaders.Length).Select(i => GetColumn(values, i)).ToList();
                    if (fields.All(string.IsNullOrWhiteSpace))
                    {
                        rowErrors.Add("Empty rows are not allowed between result rows.");
                        rows.Add(new ParsedAcademicResultRow(rowNumber, null, null, null, null, null, rowErrors));
                        continue;
                    }
                    rows.Add(new ParsedAcademicResultRow(rowNumber, fields[0], fields[1], fields[2], fields[3], fields[4], rowErrors));
                    if (rows.Count > MaxDataRows)
                        return new ParsedAcademicResultsWorkbook(rows, new() { $"The workbook cannot contain more than {MaxDataRows} result rows." });
                }

                if (rows.Count == 0) errors.Add("The workbook contains no result rows.");
            }
            catch (InvalidDataException)
            {
                errors.Add("The selected file is not a valid .xlsx workbook.");
            }
            catch (XmlException)
            {
                errors.Add("The workbook contains malformed XML and could not be read.");
            }
            catch (Exception)
            {
                errors.Add("The workbook structure is invalid or unsupported.");
            }
            return new ParsedAcademicResultsWorkbook(rows, errors);
        }

        public static byte[] CreateTemplate()
        {
            var entries = new Dictionary<string, string>
            {
                ["[Content_Types].xml"] = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>",
                ["_rels/.rels"] = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>",
                ["xl/workbook.xml"] = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Results\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>",
                ["xl/_rels/workbook.xml.rels"] = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>",
                ["xl/worksheets/sheet1.xml"] = BuildWorksheetXml()
            };
            using var output = new MemoryStream();
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var item in entries)
                {
                    var entry = archive.CreateEntry(item.Key, CompressionLevel.NoCompression);
                    using var writer = new StreamWriter(entry.Open(), new System.Text.UTF8Encoding(false));
                    writer.Write(item.Value);
                }
            }
            return output.ToArray();
        }

        private static string BuildWorksheetXml()
        {
            var headers = string.Join("", RequiredHeaders.Select((header, index) =>
                $"<c r=\"{ColumnName(index + 1)}1\" t=\"inlineStr\"><is><t>{XmlEscape(header)}</t></is></c>"));
            var instructions = new[]
            {
                "Instructions: Use one workbook for one AcademicRegistrationId. Repeat that ID on every result row.",
                "Required: CourseCode must identify an enrolled course; Result must be Pass or Fail.",
                "Optional: SemesterGpa must be consistent on every row (0.00–4.00, max 2 decimals).",
                "Optional: InternalNote must be consistent on every row (maximum 1,000 characters).",
                "Do not add credits, student names, formulas, or other data. Existing finalized results cannot be changed."
            };
            var rows = instructions.Select((text, i) => $"<row r=\"{i + 1}\"><c r=\"A{i + 1}\" t=\"inlineStr\"><is><t>{XmlEscape(text)}</t></is></c></row>");
            rows = rows.Append($"<row r=\"7\">{headers}</row>");
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>" + string.Concat(rows) + "</sheetData></worksheet>";
        }

        private static XElement ReadXml(ZipArchive archive, string path)
        {
            var entry = archive.GetEntry(path) ?? throw new InvalidDataException();
            using var stream = entry.Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxExpandedBytes });
            return XElement.Load(reader);
        }

        private static Dictionary<int, string> ReadRow(XElement row, List<string> sharedStrings, out bool hasFormula)
        {
            hasFormula = false;
            var values = new Dictionary<int, string>();
            foreach (var cell in row.Elements().Where(x => x.Name.LocalName == "c"))
            {
                if (cell.Elements().Any(x => x.Name.LocalName == "f")) { hasFormula = true; continue; }
                var reference = (string?)cell.Attribute("r");
                var index = ColumnIndex(reference);
                if (index < 0 || index > 100) continue;
                if (values.ContainsKey(index)) throw new InvalidDataException("Duplicate cell reference in worksheet row.");
                var type = (string?)cell.Attribute("t");
                var value = type == "inlineStr"
                    ? string.Concat(cell.Descendants().Where(x => x.Name.LocalName == "t").Select(x => x.Value))
                    : cell.Elements().FirstOrDefault(x => x.Name.LocalName == "v")?.Value ?? string.Empty;
                if (type == "s")
                {
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var sharedIndex) ||
                        sharedIndex < 0 || sharedIndex >= sharedStrings.Count)
                        value = string.Empty;
                    else value = sharedStrings[sharedIndex];
                }
                values[index] = value;
            }
            return values;
        }

        private static string? GetColumn(Dictionary<int, string> values, int index) => values.TryGetValue(index, out var value) ? value : null;
        private static int ColumnIndex(string? reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) return -1;
            var result = 0;
            foreach (var c in reference.TakeWhile(char.IsLetter)) result = result * 26 + char.ToUpperInvariant(c) - 'A' + 1;
            return result - 1;
        }
        private static string ColumnName(int number)
        {
            var result = string.Empty;
            while (number > 0) { number--; result = (char)('A' + number % 26) + result; number /= 26; }
            return result;
        }
        private static string XmlEscape(string value) => System.Security.SecurityElement.Escape(value) ?? string.Empty;
    }
}
