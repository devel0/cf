using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

var escape = (char)27;

void SetForegroundColor(Color color) => SetColor(fg: color, bg: null);
void SetBackgroundColor(Color color) => SetColor(fg: null, bg: color);
void ResetColors() => SetColor(fg: null, bg: null);

void SetColor(Color? fg, Color? bg)
{
    var str = "";

    if (!fg.HasValue && !bg.HasValue)
        str = $"{escape}[0m";

    else if (fg.HasValue && !bg.HasValue)
        str = $"{escape}[38;2;{fg.Value.R};{fg.Value.G};{fg.Value.B}m";

    else if (!fg.HasValue && bg.HasValue)
        str = $"{escape}[48;2;{bg.Value.R};{bg.Value.G};{bg.Value.B}m";

    else if (fg.HasValue && bg.HasValue)
        str = $"{escape}[38;2;{fg.Value.R};{fg.Value.G};{fg.Value.B}m{escape}[48;2;{bg.Value.R};{bg.Value.G};{bg.Value.B}m";

    Console.Write(str);
}

string? DefaultConfigPathfilename()
{
    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    if (string.IsNullOrWhiteSpace(appData)) return null;

    var folderPath = Path.Combine(appData, "cf");

    if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

    return Path.Combine(folderPath, "default.json");
}

if (DefaultConfigPathfilename() is not null && !File.Exists(DefaultConfigPathfilename()))
{
    File.WriteAllText(DefaultConfigPathfilename()!, JsonSerializer.Serialize(
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
    Console.WriteLine($"{AppDomain.CurrentDomain.FriendlyName} [--test=FG[,BG]] [--file=INPUTFILE] <config-file>");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("    --test=[FG[,BG]]     prints out a sample with given hex color.");
    Console.WriteLine("    --demo               print color demo");
    Console.WriteLine("    --file=INPUTFILE     read from given input file instead of stdin");
    Console.WriteLine();
    if (DefaultConfigPathfilename() is not null)
        Console.WriteLine($"Default config file is {DefaultConfigPathfilename()}");
}

if (args.Any(r => r == "--demo"))
{

    for (var h = 0; h < 360; h += 1)
    {
        var color = ColorHelper.ColorConverter.HslToRgb(new ColorHelper.HSL(
            h, // hue deg
            100, // % saturation
            50 // % luminosity                    
            ));

        SetForegroundColor(Color.FromArgb(color.R, color.G, color.B));

        Console.Write('D');
    }

    Console.WriteLine();

    ResetColors();

    Environment.Exit(0);
}

string? inputPathfilename = null;

if (args.Any(r => r.StartsWith("--file=")))
    inputPathfilename = args.First(w => w.StartsWith("--file=")).Substring("--file=".Length);

{
    var T = "--test=";
    if (args.Any(r => r.StartsWith(T)))
    {
        var q = args.First(w => w.StartsWith(T));
        q = q.Substring(T.Length, q.Length - T.Length);

        var ss = q.Split(',');

        var fg = ss[0];
        var bg = "";

        if (ss.Length > 1)
            bg = ss[1];

        var fgColor = ParseColor(fg);
        Color? bgColor = null;

        if (!string.IsNullOrWhiteSpace(bg))
            bgColor = ParseColor(bg);

        SetColor(fgColor, bgColor);

        Console.WriteLine("SAMPLE");

        ResetColors();

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
    if (DefaultConfigPathfilename() is not null && File.Exists(DefaultConfigPathfilename()))
        configPathfilename = DefaultConfigPathfilename();

    else
    {
        PrintHelp();
        Environment.Exit(1);
    }
}
else
    configPathfilename = args.Where(r => !r.StartsWith("--")).FirstOrDefault();

if (configPathfilename is null) configPathfilename = DefaultConfigPathfilename();

if (configPathfilename is null || !File.Exists(configPathfilename))
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

Stream? stream = null;

if (inputPathfilename is not null)
{
    if (!File.Exists(inputPathfilename))
    {
        Console.WriteLine($"Input file {inputPathfilename} not exists");
        Environment.Exit(4);
    }
    stream = File.Open(inputPathfilename, FileMode.Open, FileAccess.Read);
}

else
    stream = Console.OpenStandardInput();

using var sr = new StreamReader(stream);

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
    .Select(rule => new RuleRgx
    {
        Rule = rule,
        Rgx = new Regex(rule.Regex, rule.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None)
    })
    .ToList();

while (!cts.IsCancellationRequested)
{
    var line = await sr.ReadLineAsync(cts.Token);

    if (line is null) break;

    var qMatches = configRulesWithRgx
        .Select(ruleRgx => new RuleRgxMatches
        {
            RuleRgx = ruleRgx,
            Matches = ruleRgx.Rgx.Matches(line)
                .Where(r => ruleRgx.Rule.GroupMatch == false || r.Groups.Count > 1)
                .ToList()
        })
        .ToList();

    if (qMatches.Any(r => r.Matches.Count > 0)) // some rules matches
    {
        var q = qMatches
            .SelectMany((w, origIdx) => w.Matches.Select(m => new BlockNfo
            {
                OrigIdx = origIdx,
                RuleRgx = w.RuleRgx,
                Match = m,

                MatchIdx = w.RuleRgx.Rule.FullRow ?
                    0 :
                    w.RuleRgx.Rule.GroupMatch == false ? m.Index : m.Groups[1].Index,

                MatchLen = w.RuleRgx.Rule.FullRow ?
                    line.Length :
                    w.RuleRgx.Rule.GroupMatch == false ? m.Length : m.Groups[1].Length
            }))
            .Where(r => r.Match.Length > 0)
            .ToList();

        var processed = new List<BlockExtNfo>();
        var seq = new List<BlockExtNfo>();

        var ary = new CharColor[line.Length];

        var nullCharColor = new CharColor();

        for (var i = 0; i < line.Length; ++i) ary[i] = nullCharColor;

        foreach (var x in q)
        {
            var charColor = new CharColor
            {
                Background = x.RuleRgx.Rule.Background?.Length > 0 ? x.RuleRgx.Rule.Background : null,
                Foreground = x.RuleRgx.Rule.Foreground?.Length > 0 ? x.RuleRgx.Rule.Foreground : null,
            };

            var from = 0;
            var to = line.Length - 1;

            if (!x.RuleRgx.Rule.FullRow)
            {
                from = x.MatchIdx;
                to = x.MatchEndIdx;
            }

            var bufCharColor = new List<CharColor>();

            for (var j = from; j <= to; ++j)
            {
                if (ary[j] == nullCharColor)
                {
                    ary[j] = charColor;
                }
                else
                {
                    var fg = charColor.Foreground ?? ary[j].Foreground;
                    var bg = charColor.Background ?? ary[j].Background;

                    var qBuf = bufCharColor.FirstOrDefault(r => r.Foreground == fg && r.Background == bg);
                    if (qBuf is null)
                    {
                        qBuf = new CharColor { Foreground = fg, Background = bg };
                        bufCharColor.Add(qBuf);
                    }

                    ary[j] = qBuf;
                }
            }
        }

        CharColor? prev = null;

        var sb = new StringBuilder();

        for (var i = 0; i < line.Length; ++i)
        {
            var thisCC = ary[i];

            if (prev is null || prev.Foreground != thisCC.Foreground || prev.Background != thisCC.Background)
            {
                Console.Write(sb.ToString());

                sb.Clear();

                ResetColors();

                if (thisCC.Background is not null)
                    SetBackgroundColor(ParseColor(thisCC.Background)!.Value);

                if (thisCC.Foreground is not null)
                    SetForegroundColor(ParseColor(thisCC.Foreground)!.Value);
            }

            sb.Append(line[i]);

            prev = thisCC;
        }

        if (sb.Length > 0) Console.Write(sb.ToString());

        Console.WriteLine();
    }

    else // no rules match, print fullrow without colors
    {
        ResetColors();
        Console.WriteLine(line);
    }
}

ResetColors();