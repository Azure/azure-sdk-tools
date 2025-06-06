using System.Text.Json;

public class LocalHTMLDataItem
{
    public required string Type { get; set; }
    public required List<HTMLRule> Rules { get; set; }

    public LocalHTMLDataItem(string type, List<HTMLRule> rules)
    {
        Type = type;
        Rules = rules;
    }
}

public class HTMLRule
{
    public required string RuleName { get; set; }
    public required string LocalPath { get; set; }
    public required bool Expected { get; set; }
    public string? FileUri { get; set; }

    public HTMLRule(string ruleName, bool expected, string localPath)
    {
        RuleName = ruleName;
        Expected = expected;
        LocalPath = localPath;
        string htmlFilePath = Path.GetFullPath(localPath);
        FileUri = new Uri(htmlFilePath).AbsoluteUri;
    }
}
public class LocalData
{
    public static List<LocalHTMLDataItem> Items { get; set; }

    static LocalData()
    {
        string filePath = "../../../LocalHtmlData.json";

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found", filePath);
        }

        string jsonContent = File.ReadAllText(filePath);

        Items = JsonSerializer.Deserialize<List<LocalHTMLDataItem>>(jsonContent) ?? new List<LocalHTMLDataItem>();

    }
}