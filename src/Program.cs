﻿using System.Drawing;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Console = Colorful.Console;

string DefaultConfigPathfilename()
{
    var folderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "cf");

    if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

    return Path.Combine(folderPath, "default.json");
}

if (!File.Exists(DefaultConfigPathfilename()))
{
    File.WriteAllText(DefaultConfigPathfilename(), JsonSerializer.Serialize(
        new List<ColorizerItem>
        {
            new ColorizerItem{
                Note= "pid",
                Foreground= "317444",
                Background= "",
                Regex = "[a-z0-9]+\\[(\\d*)\\]",
                GroupMatch = true
            },

            new ColorizerItem{
                Note= "procname",
                Foreground= "d8e21d",
                Background= "",
                Regex = "[a-z0-9]+\\s([^\\s]*)\\[\\d*\\]",
                GroupMatch = true
            },
        },
        new JsonSerializerOptions
        {
            // avoid to convert + into escaped \\u002B
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        }));
}

void PrintHelp()
{
    Console.WriteLine($"{AppDomain.CurrentDomain.FriendlyName} [--test=HEXCOLOR] <config-file>");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("    --test=HEXCOLOR     prints out a sample with given hex color.");
    Console.WriteLine();
    Console.WriteLine($"Default config file is {DefaultConfigPathfilename()}");
}

{
    var T = "--test=";
    if (args.Any(r => r.StartsWith(T)))
    {
        var q = args.First(w => w.StartsWith(T));
        q = q.Substring(T.Length, q.Length - T.Length);

        var col = ParseColor(q);
        if (col.HasValue)
            Console.WriteLine("SAMPLE", col.Value);

        Environment.Exit(10);
    }
}

if (args.Any(r => r == "--help"))
{
    PrintHelp();
    Environment.Exit(0);
}

string? configPathfilename = null;

if (args.Length == 0)
{
    if (File.Exists(DefaultConfigPathfilename()))
        configPathfilename = DefaultConfigPathfilename();

    else
    {
        PrintHelp();
        Environment.Exit(1);
    }
}
else
    configPathfilename = args.Where(r => !r.StartsWith("---test=") && !r.StartsWith("--help")).FirstOrDefault();

if (configPathfilename is null) configPathfilename = DefaultConfigPathfilename();

if (!File.Exists(configPathfilename))
{
    Console.WriteLine($"{configPathfilename ?? DefaultConfigPathfilename()} config file not found.");
    PrintHelp();
    Environment.Exit(2);
}

var configRules = JsonSerializer.Deserialize<List<ColorizerItem>>(File.ReadAllText(configPathfilename));
if (configRules is null)
{
    Console.WriteLine($"Unable to deserialize config file {configPathfilename}.");
    Environment.Exit(3);
}

using var stream = Console.OpenStandardInput();
using var sr = new StreamReader(stream);

// using var sr = new StreamReader("/home/devel0/test");

var cts = new CancellationTokenSource();

Console.CancelKeyPress += (a, b) =>
{
    b.Cancel = true;
    cts.Cancel();

    // ReadLineAsync of the console standard input seems ignore token cancellation
    // without an exit here the user should press enter to exit the process
    Environment.Exit(100);
};

Color? ParseColor(string hexColor)
{
    if (string.IsNullOrWhiteSpace(hexColor)) return null;

    if (hexColor.StartsWith("#")) hexColor = hexColor.Substring(1, hexColor.Length - 1);

    if (hexColor.Length == 8) // skip alpha
        hexColor = hexColor.Substring(2, hexColor.Length - 2);

    if (hexColor.Length == 6)
    {
        int r, g, b;

        if (
            int.TryParse(hexColor.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out r) &&
            int.TryParse(hexColor.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out g) &&
            int.TryParse(hexColor.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out b)
        )
            return Color.FromArgb(r, g, b);
    }

    return Color.White;
}

var configRulesWithRgx = configRules
    .Select(rule => new
    {
        rule,
        rgx = new Regex(rule.Regex, rule.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None)
    })
    .ToList();

while (!cts.IsCancellationRequested)
{
    var line = await sr.ReadLineAsync(cts.Token);

    if (line is null) break;

    var qMatches = configRulesWithRgx
        .Select(ruleRgx => new
        {
            ruleRgx,
            matches = ruleRgx.rgx.Matches(line)
                .Where(r => ruleRgx.rule.GroupMatch == false || r.Groups.Count > 1)
                .ToList()
        })
        .ToList();

    if (qMatches.Any(r => r.matches.Count > 0)) // some rules matches
    {
        var qFirstFullRow = qMatches.FirstOrDefault(r => r.ruleRgx.rule.FullRow && r.matches.Count > 0);

        if (qFirstFullRow is not null) // there is at least one fullrow rule matching, take the first one
        {
            var foreground = ParseColor(qFirstFullRow.ruleRgx.rule.Foreground);
            var background = ParseColor(qFirstFullRow.ruleRgx.rule.Background);

            if (background.HasValue)
                Console.BackgroundColor = background.Value;

            if (foreground.HasValue)
                Console.Write(line, foreground.Value);
        }

        else // there is no fullrow rule matching, but some non-rullrow matching rules, print interleaved
        {
            // sort matches by row char index
            var qSorted = qMatches
                .SelectMany(w => w.matches.Select(m => new
                {
                    match = m,
                    w.ruleRgx,
                    eidx = w.ruleRgx.rule.GroupMatch == false ? m.Index : m.Groups[1].Index,
                    elen = w.ruleRgx.rule.GroupMatch == false ? m.Length : m.Groups[1].Length
                }))
                .OrderBy(w => w.eidx)
                .ToList();

            var idx = 0;
            var toRemove = new List<int>();
            for (var si = 0; si < qSorted.Count; ++si)
            {
                var s = qSorted[si];

                if (s.eidx < idx)
                    toRemove.Add(si);

                idx = s.eidx + s.elen;
            }

            if (toRemove.Count > 0) // remove overlapped matches
                qSorted = qSorted
                    .Select((s, i) => new { s, i })
                    .Where(t => !toRemove.Contains(t.i))
                    .Select(x => x.s)
                    .ToList();

            // print out

            idx = 0;

            foreach (var r in qSorted)
            {
                if (r.eidx > idx)
                {
                    Console.ResetColor();
                    Console.Write(line.Substring(idx, r.eidx - idx));
                }

                var foreground = ParseColor(r.ruleRgx.rule.Foreground);
                var background = ParseColor(r.ruleRgx.rule.Background);

                if (background.HasValue)
                    Console.BackgroundColor = background.Value;

                var token = line.Substring(r.eidx, r.elen);
                if (foreground.HasValue)
                    Console.Write(token, foreground.Value);

                else
                    Console.Write(token);

                idx = r.eidx + r.elen;
            }

            if (idx < line.Length)
                Console.Write(line.Substring(idx, line.Length - idx));
        }

        Console.WriteLine();
    }

    else // no rules match, print fullrow without colors
    {
        Console.ResetColor();
        Console.WriteLine(line);
    }
}

Console.ResetColor();