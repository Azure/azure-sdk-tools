using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Net.Http.Headers;
using System.Text;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using UtilityLibraries;

namespace ReportHelper;
public class ExcelHelper4Test
{
    private static readonly object LockObj = new object();

    // Initialize the Excel file if it doesn't exist
    public static string Init(string fileName, string sheetName)
    {
        if (sheetName.Length > 29)
        {
            sheetName = sheetName.Substring(0, 29);
        }
        // Define the root directory for the Excel file
        string rootDirectory = ConstData.ReportsDirectory;
        {
            Directory.CreateDirectory(rootDirectory);
        }

        string localFilePath = Path.Combine(rootDirectory, fileName);

        IWorkbook workbook;

        // Check if file exists
        if (File.Exists(localFilePath))
        {
            // Load existing workbook
            using (var fs = new FileStream(localFilePath, FileMode.Open, FileAccess.ReadWrite))
            {
                workbook = WorkbookFactory.Create(fs);
            }
        }
        else
        {
            // Create new workbook
            workbook = new XSSFWorkbook();
        }

        // Check if the sheet exists
        ISheet sheet = workbook.GetSheet(sheetName);
        if (sheet == null)
        {
            // Create new sheet
            sheet = workbook.CreateSheet(sheetName);

            // Create header row with column names
            IRow row = sheet.CreateRow(0);
            row.CreateCell(0).SetCellValue("NO.");
            row.CreateCell(1).SetCellValue("Error Info");
            row.CreateCell(2).SetCellValue("Number of occurrences");
            row.CreateCell(3).SetCellValue("Location of errors");
            row.CreateCell(4).SetCellValue("Error Link");
            row.CreateCell(5).SetCellValue("Test cases");
            row.CreateCell(6).SetCellValue("Notes");
        }

        // Save workbook
        using (var fs = new FileStream(localFilePath, FileMode.Create, FileAccess.Write))
        {
            workbook.Write(fs);
        }

        workbook.Close();

        return localFilePath;
    }
    public static void AddTestResult(ConcurrentQueue<TResult> testResults, string fileName, string sheetName)
    {
        if (sheetName.Length > 29)
        {
            sheetName = sheetName.Substring(0, 29);
        }
        lock (LockObj)
        {
            string localFilePath = Init(fileName, sheetName);
            using (var fs = new FileStream(localFilePath, FileMode.Open, FileAccess.ReadWrite))
            {
                IWorkbook workbook = new XSSFWorkbook(fs);
                ISheet sheet = workbook.GetSheet(sheetName);

                // Create a cell style to enable text wrapping
                ICellStyle cellStyle = workbook.CreateCellStyle();
                cellStyle.WrapText = true;

                // Create a cell style for hyperlinks 
                ICellStyle hlinkStyle = workbook.CreateCellStyle();
                IFont hlinkFont = workbook.CreateFont();
                hlinkFont.Underline = FontUnderlineType.Single;
                hlinkFont.FontName = "Aptos Narrow";
                hlinkFont.Color = IndexedColors.Blue.Index;
                hlinkStyle.SetFont(hlinkFont);

                foreach (var res in testResults)
                {
                    // Create a new row and populate cells with test result data
                    IRow row = sheet.CreateRow(sheet.LastRowNum + 1);

                    row.CreateCell(0).SetCellValue(sheet.LastRowNum);
                    row.CreateCell(1).SetCellValue(res.ErrorInfo);
                    row.CreateCell(2).SetCellValue(res.NumberOfOccurrences);

                    ICell cell3 = row.CreateCell(3);
                    cell3.SetCellValue(string.Join("\n", res.LocationsOfErrors ?? new List<string>()));
                    cell3.CellStyle = cellStyle;

                    ICell cell4 = row.CreateCell(4);
                    cell4.SetCellValue(res.ErrorLink);
                    IHyperlink link = workbook.GetCreationHelper().CreateHyperlink(HyperlinkType.Url);
                    link.Address = res.ErrorLink;
                    cell4.Hyperlink = link;
                    cell4.CellStyle = hlinkStyle;

                    row.CreateCell(5).SetCellValue(res.TestCase);

                    // Add a "python-rules.md" hyperlink to the notes column
                    row.CreateCell(6).SetCellValue("python-rules.md");
                    IHyperlink noteLink = workbook.GetCreationHelper().CreateHyperlink(HyperlinkType.Url);
                    noteLink.Address = "https://github.com/zedy-wj/content-validation/blob/main/docs/rules-introduction/python-rules.md";
                    row.GetCell(6).Hyperlink = noteLink;
                    row.GetCell(6).CellStyle = hlinkStyle;
                }

                // Save the updated workbook back to the file
                using (var outFs = new FileStream(localFilePath, FileMode.Create, FileAccess.Write))
                {
                    workbook.Write(outFs);
                    workbook.Close();
                }
            }
        }
    }


    public static void AddTestResult(List<TResult4Json> testResults, string fileName, string sheetName)
    {
        if (sheetName.Length > 29)
        {
            sheetName = sheetName.Substring(0, 29);
        }
        lock (LockObj)
        {
            string localFilePath = Init(fileName, sheetName);
            using (var fs = new FileStream(localFilePath, FileMode.Open, FileAccess.ReadWrite))
            {
                IWorkbook workbook = new XSSFWorkbook(fs);
                ISheet sheet = workbook.GetSheet(sheetName);

                // Create a cell style to enable text wrapping
                ICellStyle cellStyle = workbook.CreateCellStyle();
                cellStyle.WrapText = true;

                // Create a cell style for hyperlinks
                ICellStyle hlinkStyle = workbook.CreateCellStyle();
                IFont hlinkFont = workbook.CreateFont();
                hlinkFont.Underline = FontUnderlineType.Single;
                hlinkFont.FontName = "Aptos Narrow";
                hlinkFont.Color = IndexedColors.Blue.Index;
                hlinkStyle.SetFont(hlinkFont);

                foreach (var res in testResults)
                {
                    // Create a new row and populate cells with test result data
                    IRow row = sheet.CreateRow(sheet.LastRowNum + 1);

                    row.CreateCell(0).SetCellValue(sheet.LastRowNum);
                    row.CreateCell(1).SetCellValue(res.ErrorInfo);
                    row.CreateCell(2).SetCellValue(res.NumberOfOccurrences ?? 0);

                    ICell cell3 = row.CreateCell(3);
                    cell3.SetCellValue(string.Join("\n", res.LocationsOfErrors ?? new List<string>()));
                    cell3.CellStyle = cellStyle;

                    ICell cell4 = row.CreateCell(4);
                    cell4.SetCellValue(res.ErrorLink);
                    IHyperlink link = workbook.GetCreationHelper().CreateHyperlink(HyperlinkType.Url);
                    link.Address = res.ErrorLink;
                    cell4.Hyperlink = link;
                    cell4.CellStyle = hlinkStyle;

                    row.CreateCell(5).SetCellValue(res.TestCase);
                    row.CreateCell(6).SetCellValue(res.AdditionalNotes?.ToString());
                }

                // Save the updated workbook back to the file
                using (var outFs = new FileStream(localFilePath, FileMode.Create, FileAccess.Write))
                {
                    workbook.Write(outFs);
                    workbook.Close();
                }
            }
        }
    }



}
public class JsonHelper4Test
{
    private static readonly object LockObj = new object();

    // Initialize the Json file if it doesn't exist
    public static string Init(string filePath)
    {

        // Define the file path for the Excel file
        string rootDirectory = ConstData.ReportsDirectory;
        if (!Directory.Exists(rootDirectory))
        {
            Directory.CreateDirectory(rootDirectory);
        }
        string localFilePath = Path.Combine(rootDirectory, filePath);
        if (!File.Exists(localFilePath))
        {
            var emptyList = new List<TResult4Json>();
            string jsonString = JsonSerializer.Serialize(emptyList, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(localFilePath, jsonString);
        }

        return localFilePath;

    }

    public static void AddTestResult(ConcurrentQueue<TResult> testResults, string fileName)
    {

        lock (LockObj)
        {
            string localFilePath = Init(fileName);
            string jsonString = File.ReadAllText(localFilePath);
            List<TResult4Json> jsonList = JsonSerializer.Deserialize<List<TResult4Json>>(jsonString)!;
            int count = jsonList.Count;

            foreach (var res in testResults)
            {
                TResult4Json result = new TResult4Json
                {
                    Number = ++count,
                    ErrorInfo = res.ErrorInfo,
                    NumberOfOccurrences = res.NumberOfOccurrences,
                    LocationsOfErrors = res.LocationsOfErrors,
                    ErrorLink = res.ErrorLink,
                    TestCase = res.TestCase,
                    AdditionalNotes = res.AdditionalNotes
                };

                jsonList.Add(result);
            }

            var options = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };

            File.WriteAllText(localFilePath, JsonSerializer.Serialize(jsonList, options));
        }
    }

    public static void AddTestResult(List<TResult4Json> testResults, string fileName)
    {
        lock (LockObj)
        {
            string localFilePath = Init(fileName);
            string jsonString = File.ReadAllText(localFilePath);
            List<TResult4Json> jsonList = JsonSerializer.Deserialize<List<TResult4Json>>(jsonString)!;
            int count = jsonList.Count;

            foreach (var res in testResults)
            {
                TResult4Json result = new TResult4Json
                {
                    Number = ++count,
                    ErrorInfo = res.ErrorInfo,
                    NumberOfOccurrences = res.NumberOfOccurrences,
                    LocationsOfErrors = res.LocationsOfErrors,
                    ErrorLink = res.ErrorLink,
                    TestCase = res.TestCase,
                    AdditionalNotes = res.AdditionalNotes
                };

                jsonList.Add(result);
            }

            var options = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };

            File.WriteAllText(localFilePath, JsonSerializer.Serialize(jsonList, options));
        }
    }
}

public class GithubHelper
{
    public static string FormatToMarkdown(List<Dictionary<string, object>> list, bool retry = false)
    {

        string result = $"";

        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];

            result += $"{i + 1}.\n";

            result += $"**ErrorInfo**: {(item.ContainsKey("ErrorInfo") ? item["ErrorInfo"]?.ToString() : "N/A")}\n";
            result += $"**ErrorLink**: {(item.ContainsKey("ErrorLink") ? item["ErrorLink"]?.ToString() : "N/A")}\n";

            if (item.ContainsKey("LocationsOfErrors") && item["LocationsOfErrors"] is JsonElement jsonElement)
            {
                result += $"**ErrorLocation**:\n";
                List<string> locations = new List<string>();
    
                foreach (JsonElement element in jsonElement.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        locations.Add(element.GetString());
                    }
                }

                if (locations != null && locations.Count > 0)
                {
                    foreach (var location in locations)
                    {
                        string str = location.Contains(".") ? location.Substring(location.IndexOf(".") + 1) : location;
                        str = " -" + str;
                        result += $"{str}\n";
                    }
                }
            }

            if (item.ContainsKey("Note") && !string.IsNullOrWhiteSpace(item["Note"]?.ToString()))
            {
                result += $"**Note**: {item["Note"]}\n";
            }

            result += $"\n";
        }

        if (retry)
        {
            result += $"**Note**: The issue body is too long, so we have shortened it.\n";
        }
        return result;
    }

    public static async Task CreateOrUpdateGitHubIssue(string owner, string repo, string githubToken, string packageName, string language){
        string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/issues";

        List<string> succeedRules = GetSucceedRules();

        if(succeedRules.Count == 0){
            Console.WriteLine("No succeed rules found.");
            return;
        }

        var allIssues = new List<JsonNode>();
        allIssues = GetAllGitHubIssues(apiUrl, githubToken);

        foreach (var rule in succeedRules){
            string issueTitle = $"{packageName} content validation issues about {rule} for {language} sdk.";
            Console.WriteLine($"Searching issue with title: {issueTitle}");

            var matchingIssue = allIssues.FirstOrDefault(i => i["title"]?.GetValue<string>() == issueTitle);

            if (matchingIssue != null)
            {
                Console.WriteLine($"Html url: {matchingIssue["html_url"]?.GetValue<string>()}");
                Console.WriteLine($"Issue Number: {matchingIssue["number"]?.GetValue<int>()}");
                Console.WriteLine($"Created at: {matchingIssue["created_at"]?.GetValue<string>()}");
                Console.WriteLine($"Updated at: {matchingIssue["updated_at"]?.GetValue<string>()}");
                Console.WriteLine($"Issue: {issueTitle} already exist.");

                // Add a comment to the existing issue with diff issue json.
                string searchPattern = "DiffIssues*.json";
                var githubIssueBodyForJson = GenerateSpecificIssueJson(rule, searchPattern);

                if(githubIssueBodyForJson.Count != 0)
                {
                    string githubBodyOrCommentDiff = FormatToMarkdown(githubIssueBodyForJson);

                    await AddCommentToIssue(owner, repo, matchingIssue["number"]?.GetValue<int>(), githubBodyOrCommentDiff, githubToken);
                }else
                {
                    Console.WriteLine($"There are no new issue about {rule} in this pipeline run.");
                }
            }
            else
            {
                Console.WriteLine($"No issue found with title: {issueTitle}");

                // Open a new issue with total issue json.
                string searchPattern = "TotalIssues*.json";
                var githubIssueBodyForJson = GenerateSpecificIssueJson(rule, searchPattern);

                if(githubIssueBodyForJson.Count != 0)
                {
                    Console.WriteLine($"Opening a new issue with title: {issueTitle}");

                    // Create a new issue in GitHub
                    try
                    {
                        string githubBodyOrCommentTotal = FormatToMarkdown(githubIssueBodyForJson);
                        var res = await CreateNewIssueAsync(apiUrl, issueTitle, githubBodyOrCommentTotal, githubToken);

                        if (!res)
                        {
                            Console.WriteLine("Trying to shorten the issue body and recreate the issue.");
                            // If the issue body is too long, we can try to shorten it
                            // For example, we can limit the number of items to 30
                            int maxCount = 30;
                            githubIssueBodyForJson = GenerateSpecificIssueJson(rule, searchPattern, maxCount);
                            Console.WriteLine($"Re-opening a new issue with title: {issueTitle}");
                            try
                            {
                                bool retry = true;
                                githubBodyOrCommentTotal  = FormatToMarkdown(githubIssueBodyForJson, retry);
                                await CreateNewIssueAsync(apiUrl, issueTitle, githubBodyOrCommentTotal, githubToken);
                            }
                            catch (Exception ex2)
                            {
                                Console.WriteLine($"Error: {ex2.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                    }
                }else
                {
                    Console.WriteLine($"There are no issue about {rule} in this pipeline run.");
                }
            }
        }
    }

    private static async Task AddCommentToIssue(string owner, string repo, int? issueNumber, string body, string githubToken)
    {
        string url = $"https://api.github.com/repos/{owner}/{repo}/issues/{issueNumber}/comments";

        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            client.DefaultRequestHeaders.Add("User-Agent", "MyApp/1.0");
 
            var issueBodyData = new
            {
                body = body
            };
 
            StringContent content = new StringContent(JsonSerializer.Serialize(issueBodyData), Encoding.UTF8, "application/json");
 
            try
            {
                Console.WriteLine("Sending request to create a new issue...");
                HttpResponseMessage response = await client.PostAsync(url, content);
 
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Error: Response status code does not indicate success: {response.StatusCode}");
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error details: {errorContent}");
                    return;
                }
 
                Console.WriteLine("HTTP request successful.");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP request failed: {ex.Message}");
                throw;
            }
        }
    }
    private static async Task<bool> CreateNewIssueAsync(string ApiUrl, string title, string body, string githubToken)
    {
        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            client.DefaultRequestHeaders.Add("User-Agent", "MyApp/1.0");
 
            var issueData = new
            {
                title = title,
                body = body
            };
 
            StringContent content = new StringContent(JsonSerializer.Serialize(issueData), Encoding.UTF8, "application/json");

            try
            {
                Console.WriteLine("Sending request to create a new issue...");
                HttpResponseMessage response = await client.PostAsync(ApiUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Error: Response status code does not indicate success: {response.StatusCode}");
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error details: {errorContent}");
                    return false;
                }
                Console.WriteLine("HTTP request successful.");
                return true;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP request failed: {ex.Message}");
                throw;
            }
        }
    }

    public static List<Dictionary<string, object>> GenerateSpecificIssueJson(string rule, string searchPattern, int maxCount = -1){
        var matchingObjects = new List<Dictionary<string, object>>();
        string rootDirectory = ConstData.ReportsDirectory;

        string outputDirectory = ConstData.EngDirectory;
        string outputFilePath = Path.Combine(outputDirectory, $"{rule}Error.json");

        string[] matchingFiles = Directory.GetFiles(rootDirectory, searchPattern, SearchOption.AllDirectories);

        if (matchingFiles.Length == 0)
        {
            if(searchPattern.Contains("DiffIssues")){
                Console.WriteLine("No diff issue matching files found. There are no new issues to report.");
            }
            else{
                Console.WriteLine("No total issue matching files found. This package have no issue in this pipeline run.");
            }
            return matchingObjects;
        }

        matchingObjects = FindMatchingObjects(rule, matchingFiles[0], maxCount);

        SaveToJson(matchingObjects, outputFilePath, rule);

        return matchingObjects;
    }

    static List<Dictionary<string, object>> FindMatchingObjects(string rule, string jsonFilePath, int maxCount)
    {
        // Define rule mapping: the values ​​in succeedRules correspond to the uppercase version of TestCase in JSON
        var ruleToTestCaseMap = new Dictionary<string, string>
        {
            { "TypeAnnotationValidation", "TestMissingTypeAnnotation" },
            { "MissingContentValidation", "TestTableMissingContent" },
            { "GarbledTextValidation", "TestGarbledText" },
            { "InconsistentTextFormatValidation", "TestInconsistentTextFormat" },
            { "DuplicateServiceValidation", "TestDuplicateService" },
            { "ExtraLabelValidation", "TestExtraLabel" },
            { "UnnecessarySymbolsValidation", "TestUnnecessarySymbols" },
            { "InvalidTagsValidation", "TestInvalidTags" },
            { "CodeFormatValidation", "TestCodeFormat" }
        };
 
        string jsonContent = File.ReadAllText(jsonFilePath);
        var jsonArray = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonContent);
 
        if (jsonArray == null)
        {
            return new List<Dictionary<string, object>>();
        }
 
        var matchingObjects = new List<Dictionary<string, object>>();
 
        if (ruleToTestCaseMap.TryGetValue(rule, out string testCase))
        {
            // Find all TestCase objects whose fields match
            var matchedObjects = jsonArray.Where(obj =>
                obj.ContainsKey("TestCase") && obj["TestCase"]?.ToString().Equals(testCase, StringComparison.OrdinalIgnoreCase) == true);

            if (maxCount > 0)
            {
                matchedObjects = matchedObjects.Take(maxCount);
            }
            matchingObjects.AddRange(matchedObjects.ToList());
        }
 
        return matchingObjects;
    }

    static void SaveToJson(List<Dictionary<string, object>> data, string outputFilePath, string rule)
    {
        if (data == null || data.Count == 0)
        {
            return;
        }

        // Change the No. from 1 to above
        for (int i = 0; i < data.Count; i++)
        {
            if (data[i].ContainsKey("NO."))
            {
                data[i]["NO."] = i + 1; 
            }
        }

        // Generate Error.json 
        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        string jsonString = JsonSerializer.Serialize(data, options);
        File.WriteAllText(outputFilePath, jsonString);
    }

    public static List<JsonNode> GetAllGitHubIssues(string apiUrl, string githubToken){
        List<JsonNode> allIssues = new List<JsonNode>();

        try
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MyGitHubApp", "1.0"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", githubToken);
                string? linkHeader = null;

                while(true)
                {
                    HttpResponseMessage response = client.GetAsync(apiUrl).Result;
                    response.EnsureSuccessStatusCode();

                    string responseBody = response.Content.ReadAsStringAsync().Result;
                    using (JsonDocument jsonDoc = JsonDocument.Parse(responseBody))
                    {
                        JsonElement root = jsonDoc.RootElement;
                        foreach (JsonElement issueElement in root.EnumerateArray())
                        {
                            // Deserialize each issue into a JsonNode (or a custom class if needed)
                            allIssues.Add(JsonNode.Parse(issueElement.GetRawText())!);
                        }
                    }

                    if (response.Headers.TryGetValues("Link", out IEnumerable<string> linkValues))
                    {
                        linkHeader = linkValues.FirstOrDefault();
                        var links = ParseLinkHeader(linkHeader);
                        if (links.TryGetValue("next", out string? nextUrl))
                        {
                            apiUrl = nextUrl.Split(';')[0].TrimStart('[').TrimStart(' ').TrimStart('<').TrimEnd('>');
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred:");
            Console.WriteLine(ex.Message);
            return null;
        }
        return allIssues;
    }

    // Helper method to parse GitHub's Link header
    private static Dictionary<string, string> ParseLinkHeader(string? linkHeader)
    {
        var links = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(linkHeader))
            return links;
 
        foreach (var linkPart in linkHeader.Split(','))
        {
            var parts = linkPart.Split(';');
            if (parts.Length < 2)
                continue;
 
            var url = parts[0].Trim('<', '>');
            var relation = parts[1].Trim().Split('=')[1].Trim('"');
            links[relation] = url;
        }
 
        return links;
    }
    public static List<string> GetSucceedRules(){
        string rootDirectory = ConstData.ReportsDirectory;
        string ruleStatusFilePath = Path.Combine(rootDirectory, "RuleStatus.json");
        

        List<string> succeedRules = new List<string>();

        if (File.Exists(ruleStatusFilePath)){
            string ruleStatusJsonContent = File.ReadAllText(ruleStatusFilePath);

            try
            {
                // Parse JSON Array
                var jsonArray = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(ruleStatusJsonContent);
    
                if (jsonArray != null)
                {
                    foreach (var item in jsonArray)
                    {
                        foreach (var kvp in item)
                        {
                            if (kvp.Value == "succeed")
                            {
                                succeedRules.Add(kvp.Key);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading or parsing JSON: {ex.Message}");
                return succeedRules;
            }
        }
        return succeedRules;
    }

    public static List<TResult4Json> DeduplicateList(List<TResult4Json> differentList)
    {
        var deduplicateList = differentList
            .GroupBy(item => item.ErrorInfo)
            .SelectMany(group =>
            {
                if (group.Count() > 3)
                {
                    var first = group.First();
                    string note = $"{first.ErrorInfo} - this type of issues appears {group.Count()} times, currently only one is shown here. For more details, please click on the excel download link below to view.";
                    first.Note = note;
                    return [first];
                }
                else
                {
                    return group;
                }
            })
            .ToList();

        return deduplicateList;
    }
}

public class pipelineStatusHelper
{
    private static readonly object LockObj = new object();
    public static void SavePipelineFailedStatus(string rule, string status)
    {
        lock (LockObj)
        {
            string engDirectory = ConstData.EngDirectory;
            string rootDirectory = ConstData.ReportsDirectory;
            if (!Directory.Exists(rootDirectory))
            {
                Directory.CreateDirectory(rootDirectory);
            }
            if (!Directory.Exists(engDirectory))
            {
                Directory.CreateDirectory(engDirectory);
            }

            string pipelineStatusfilePath = Path.Combine(engDirectory, "PipelineFailedStatus.txt");
            string filePath = Path.Combine(rootDirectory, "RuleStatus.json");
            List<Dictionary<string, string>> rulesStatusList = new List<Dictionary<string, string>>();

            if (File.Exists(filePath))
            {
                string fileContent = File.ReadAllText(filePath);
                try
                {
                    rulesStatusList = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(fileContent) ?? new List<Dictionary<string, string>>();
                }
                catch (JsonException)
                {
                    rulesStatusList = new List<Dictionary<string, string>>();
                }
            }

            bool ruleExists = false;
            foreach (var ruleStatus in rulesStatusList)
            {
                if (ruleStatus.ContainsKey(rule))
                {
                    ruleExists = true;
                    if(status == "failed")
                    {
                        ruleStatus[rule] = status;
                        File.WriteAllText(pipelineStatusfilePath, "failed, please check the detail of error info.");
                    }
                    break;
                }
            }

            if (!ruleExists)
            {
                rulesStatusList.Add(new Dictionary<string, string> { { rule, status } });
            }
            
            string jsonContent = JsonSerializer.Serialize(rulesStatusList, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, jsonContent);
        }
    }
}