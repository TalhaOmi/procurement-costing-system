// Step 1 of the learning plan: a console app that reads a CSV of vendor invoices and writes a report.
// Usage: dotnet run -- <input.csv> [output.txt]
using System.Globalization;
using System.Text;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: CsvReport <input.csv> [output.txt]");
    return 2;
}

var path = args[0];
if (!File.Exists(path))
{
    Console.Error.WriteLine($"File not found: {path}");
    return 2;
}

var lines = File.ReadAllLines(path);
var header = lines[0].Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();
int Col(string name) => Array.IndexOf(header, name) is var i and >= 0 ? i : throw new InvalidDataException($"Missing column '{name}'.");
int cNo = Col("invoice_no"), cVendor = Col("vendor"), cProject = Col("project"), cAmount = Col("amount"), cDate = Col("date");

var good = new List<Row>();
var problems = new List<string>();
var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

for (var n = 1; n < lines.Length; n++)
{
    if (string.IsNullOrWhiteSpace(lines[n])) continue;
    var f = SplitCsv(lines[n]);
    var lineNo = n + 1;

    if (f.Count != header.Length) { problems.Add($"line {lineNo}: expected {header.Length} fields, found {f.Count}"); continue; }
    if (!decimal.TryParse(f[cAmount], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)) { problems.Add($"line {lineNo}: amount '{f[cAmount]}' is not a number"); continue; }
    if (amount <= 0) { problems.Add($"line {lineNo}: amount {amount} must be positive"); continue; }
    if (!DateTime.TryParse(f[cDate], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) { problems.Add($"line {lineNo}: date '{f[cDate]}' is not a date"); continue; }
    if (string.IsNullOrWhiteSpace(f[cVendor])) { problems.Add($"line {lineNo}: vendor is blank"); continue; }
    if (!seen.Add($"{f[cVendor].Trim()}|{f[cNo].Trim()}")) { problems.Add($"line {lineNo}: duplicate invoice {f[cNo]} for {f[cVendor]}"); continue; }

    good.Add(new Row(f[cNo].Trim(), f[cVendor].Trim(), f[cProject].Trim(), amount, date));
}

var sb = new StringBuilder();
sb.AppendLine($"Invoice report for {Path.GetFileName(path)}");
sb.AppendLine($"{good.Count} valid invoices totalling {good.Sum(r => r.Amount):N2}; {problems.Count} rejected rows");
sb.AppendLine();
sb.AppendLine("By vendor");
foreach (var g in good.GroupBy(r => r.Vendor).OrderByDescending(g => g.Sum(r => r.Amount)))
    sb.AppendLine($"  {g.Key,-28} {g.Count(),3} invoices {g.Sum(r => r.Amount),14:N2}");
sb.AppendLine();
sb.AppendLine("By project");
foreach (var g in good.GroupBy(r => r.Project).OrderBy(g => g.Key))
    sb.AppendLine($"  {g.Key,-28} {g.Count(),3} invoices {g.Sum(r => r.Amount),14:N2}");
sb.AppendLine();
sb.AppendLine("By month");
foreach (var g in good.GroupBy(r => r.Date.ToString("yyyy-MM")).OrderBy(g => g.Key))
    sb.AppendLine($"  {g.Key,-28} {g.Count(),3} invoices {g.Sum(r => r.Amount),14:N2}");
if (problems.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine("Rejected rows");
    foreach (var p in problems) sb.AppendLine("  " + p);
}

Console.Write(sb.ToString());
if (args.Length > 1) File.WriteAllText(args[1], sb.ToString());
return problems.Count == 0 ? 0 : 1;

// Splits one CSV line, honouring "quoted, fields" and "" escapes.
static List<string> SplitCsv(string line)
{
    var result = new List<string>();
    var cur = new StringBuilder();
    var quoted = false;
    for (var i = 0; i < line.Length; i++)
    {
        var c = line[i];
        if (quoted)
        {
            if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
            else if (c == '"') quoted = false;
            else cur.Append(c);
        }
        else if (c == '"') quoted = true;
        else if (c == ',') { result.Add(cur.ToString()); cur.Clear(); }
        else cur.Append(c);
    }
    result.Add(cur.ToString());
    return result;
}

record Row(string No, string Vendor, string Project, decimal Amount, DateTime Date);
