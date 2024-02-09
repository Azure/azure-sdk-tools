using System.Text.RegularExpressions;

namespace Azure.Sdk.PipelineTemplateConverter;

public class Line
{
    public string Value { get; set; } = "";
    public int Instance { get; set; } = 0;
    public Comment? Comment { get; set; }
    public Comment? InlineComment { get; set; }
    public List<string>? BlockChompedLine { get; set; }
    public bool NewLineBefore { get; set; } = false;
    // YamlDotNet serialization removes quotes, but we want to preserve them
    // for line/comment lookup and reducing diff sizes
    public char? Quote { get; set; }
    public string LookupKey { get; set; }

    public Line(string line)
    {
        if (line.Contains('#'))
        {
            line = line[..line.IndexOf("#")];
        }

        var singleQuoted = new Regex(@"(^[ \t]*-?[ ]*\w*:[ ]*)'(.*)'[ ]*");
        var doubleQuoted = new Regex(@"(^[ \t]*-?[ ]*\w*:[ ]*)""(.*)""[ ]*");
        var singleQuotedMatch = singleQuoted.Match(line);
        var doubleQuotedMatch = doubleQuoted.Match(line);

        Value = line.Trim();

        if (singleQuotedMatch.Success)
        {
            Quote = '\'';
            var head = singleQuotedMatch.Groups[1].Value;
            var tail = singleQuotedMatch.Groups[2].Value;
            LookupKey = (head + tail).Trim();
        }
        else if (doubleQuotedMatch.Success)
        {
            Quote = '"';
            var head = doubleQuotedMatch.Groups[1].Value;
            var tail = doubleQuotedMatch.Groups[2].Value;
            LookupKey = (head + tail).Trim();
        }
        else
        {
            LookupKey = Value;
        }
    }
}
