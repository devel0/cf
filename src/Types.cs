using System.Text.RegularExpressions;

public class RuleRgx
{
    public required ColorizerItem Rule { get; set; }
    public required Regex Rgx { get; set; }
}

public class RuleRgxMatches
{
    public required RuleRgx RuleRgx { get; set; }
    public required List<Match> Matches { get; set; }

    public override string ToString()
    {
        var str = "";

        foreach (var m in Matches)
        {
            str += $"{m.Index}-{m.Index + m.Length} ";
        }

        return str + RuleRgx.Rule.Note;
    }
}

public class BlockNfo
{
    public required int OrigIdx { get; set; }
    public required RuleRgx RuleRgx { get; set; }
    public required Match Match { get; set; }
    public required int MatchIdx { get; set; }
    public required int MatchLen { get; set; }
    public int MatchEndIdx => MatchIdx + MatchLen - 1;

    public override string ToString() => $"{MatchIdx}-{MatchEndIdx} [{RuleRgx.Rule.Note}]";
}

public class CharColor
{
    public string? Background { get; set; } = null;
    public string? Foreground { get; set; } = null;
}

public class BlockExtNfo
{

    public required bool IsOverlapped { get; set; }

    public required BlockNfo BlockNfo { get; set; }

    public override string ToString() => $"[{(IsOverlapped ? 'o' : ' ')}] {BlockNfo}";

}