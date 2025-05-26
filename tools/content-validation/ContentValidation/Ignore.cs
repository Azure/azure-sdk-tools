using System.Text.Json;

public class IgnoreDataObject
{
    public required string Rule { get; set; }
    public List<IgnoreItem> IgnoreList { get; set; }

    public IgnoreDataObject(string rule, List<IgnoreItem> ignoreList)
    {
        Rule = rule;
        IgnoreList = ignoreList;
    }
}


public class IgnoreItem
{
        public required string IgnoreText { get; set; }
        public string? Usage { get; set; }

        public IgnoreItem(string ignoreText, string usage)
        {
            IgnoreText = ignoreText;
            Usage = usage;
        }
}

public class IgnoreData
{
    public static List<IgnoreDataObject> IgnoreDataObjectList { get; set; }

    static IgnoreData()
    {
        string filePath = "../../../../ignore.json";

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found", filePath);
        }

        string jsonContent = File.ReadAllText(filePath);

        IgnoreDataObjectList = JsonSerializer.Deserialize<List<IgnoreDataObject>>(jsonContent) ?? new List<IgnoreDataObject>();

    }

    public static List<IgnoreItem> GetIgnoreList(string rule, string usage = "")
    {
        var ignoreList = IgnoreDataObjectList.Find(x => x.Rule == rule)?.IgnoreList;
        if (ignoreList == null)
        {
            return new List<IgnoreItem>();
        }
        if (usage == null || usage == string.Empty)
        {
            return ignoreList;
        }
        return ignoreList.FindAll(x => x.Usage == usage);
    }
}

