using System.Text.Json;

public class TestLinkItem
{
    public required string Version { get; set; }
    public List<string> Url { get; set; } = new List<string>();

    public TestLinkItem() { }

    public TestLinkItem(string version, List<string> url)
    {
        Version = version;
        Url = url;
    }
}

public class TestLinkData
{
    private static readonly string DataFolderPath = "../DataSource/DataFolder";

    private static string GetFilePath(string language, string packageName)
    {
        return Path.Combine(DataFolderPath, language, $"{packageName}.json");
    }

    private static List<TestLinkItem> LoadPackageData(string language, string packageName)
    {
        string filePath = GetFilePath(language, packageName);
        
        if (!File.Exists(filePath))
        {
            return new List<TestLinkItem>();
        }

        try
        {
            string jsonContent = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return JsonSerializer.Deserialize<List<TestLinkItem>>(jsonContent, options) ?? new List<TestLinkItem>();
        }
        catch
        {
            Console.WriteLine($"Error loading package data for {language}/{packageName}");
            return new List<TestLinkItem>();
        }
    }

    private static void SavePackageData(string language, string packageName, List<TestLinkItem> testLinkItems)
    {
        string filePath = GetFilePath(language, packageName);
        string? directoryPath = Path.GetDirectoryName(filePath);
        
        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        string jsonContent = JsonSerializer.Serialize(testLinkItems, options);
        File.WriteAllText(filePath, jsonContent);
    }

    public static List<string> GetUrls(string language, string packageName, string version)
    {
        try
        {
            var testLinkItems = LoadPackageData(language, packageName);
            var item = testLinkItems.Find(x => x.Version.Equals(version, StringComparison.OrdinalIgnoreCase));
            return item?.Url ?? new List<string>();
        }
        catch
        {
            Console.WriteLine("There is no testLink.");
            return new List<string>();
        }
    }

    public static void AddUrls(string language, string packageName, string version, List<string> urls)
    {
        var testLinkItems = LoadPackageData(language, packageName);
        
        // Remove existing entry for this version
        testLinkItems.RemoveAll(x => x.Version.Equals(version, StringComparison.OrdinalIgnoreCase));
        
        // Add new entry
        var newItem = new TestLinkItem
        {
            Version = version,
            Url = new List<string>(urls)
        };
        testLinkItems.Add(newItem);

        SavePackageData(language, packageName, testLinkItems);
    }

    public static void ClearUrls(string language, string packageName, string version)
    {
        var testLinkItems = LoadPackageData(language, packageName);
        var item = testLinkItems.Find(x => x.Version.Equals(version, StringComparison.OrdinalIgnoreCase));

        if (item != null)
        {
            item.Url.Clear();
            SavePackageData(language, packageName, testLinkItems);
        }
    }
}
