public class ColorizerItem
{
    public string? Note { get; set; } = null;
    public string Foreground { get; set; } = "";
    public string Background { get; set; } = "";
    public string Regex { get; set; } = "";
    public bool GroupMatch { get; set; }
    public bool IgnoreCase { get; set; }
    public bool FullRow { get; set; }
}