using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using System.Net;
using System.Text.RegularExpressions;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace DataSource
{
    public class GetDataSource
    {
        private static readonly string SDK_API_URL_BASIC = "https://learn.microsoft.com/en-us/";
        private static readonly string SDK_API_REVIEW_URL_BASIC = "https://review.learn.microsoft.com/en-us/";
        private static readonly string PYTHON_DATA_SDK_RELEASES_LATEST_CSV_URL = "https://raw.githubusercontent.com/Azure/azure-sdk/main/_data/releases/latest/python-packages.csv";
        private static readonly string JAVA_DATA_SDK_RELEASES_LATEST_CSV_URL = "https://raw.githubusercontent.com/Azure/azure-sdk/main/_data/releases/latest/java-packages.csv";
        private static readonly string JAVASCRIPT_DATA_SDK_RELEASES_LATEST_CSV_URL = "https://raw.githubusercontent.com/Azure/azure-sdk/main/_data/releases/latest/js-packages.csv";
        private static readonly string DOTNET_DATA_SDK_RELEASES_LATEST_CSV_URL = "https://raw.githubusercontent.com/Azure/azure-sdk/main/_data/releases/latest/dotnet-packages.csv";
        static async Task Main(string[] args)
        {
            // Default Configuration
            using IHost host = Host.CreateApplicationBuilder(args).Build();

            IConfiguration config = host.Services.GetRequiredService<IConfiguration>();

            var appConfig = new AppConfig();
            config.Bind(appConfig);

            string branch = appConfig.Branch;
            string cookieName = appConfig.CookieName;
            string cookieValue = appConfig.CookieValue;

            var languageMap = new Dictionary<string, List<PackageConfig>>
            {
                { "dotnet", appConfig.Languages.DotNet },
                { "java", appConfig.Languages.Java },
                { "javascript", appConfig.Languages.JavaScript },
                { "python", appConfig.Languages.Python }
            };

            // Check if a specific language is passed as an argument
            string? selectedLanguage = args.Length > 0 ? args[0].ToLower() : null;

            foreach (var languageEntry in languageMap)
            {
                string language = languageEntry.Key;
                List<PackageConfig> packages = languageEntry.Value;

                // If a specific language is selected, skip other languages
                if (selectedLanguage != null && language != selectedLanguage)
                {
                    continue;
                }
                
                Console.WriteLine($"Processing language: {language}");

                foreach (var packageConfig in packages)
                {
                    
                    if (string.IsNullOrEmpty(packageConfig.ReadmeName) ||
                        string.IsNullOrEmpty(packageConfig.PackageName))
                    {
                        Console.WriteLine($"Skipping empty configuration for {language}");
                        continue;
                    }

                    Console.WriteLine($"Processing package: {packageConfig.PackageName}");

                    string packageForVersionCheck = language?.ToLower() == "javascript" || language?.ToLower() == "dotnet"
                        ? packageConfig.CsvPackageName
                        : packageConfig.PackageName;

                    string? versionSuffix = ChooseGAOrPreview(language, packageForVersionCheck);

                    if (versionSuffix == null)
                    {
                        Console.WriteLine($"Could not determine version suffix for {packageConfig.PackageName}, skipping...");
                        continue;
                    }

                    string? pageLink = GetPackagePageOverview(language, packageConfig.ReadmeName, versionSuffix, branch);

                    List<string> allPages = new List<string>();

                    await GetAllDataSource(allPages, language, versionSuffix, pageLink, cookieName, cookieValue, branch);

                    if (allPages.Count > 0)
                    {
                        Console.WriteLine($"Saving {allPages.Count} URLs for {packageConfig.PackageName}...");
                        TestLinkData.AddUrls(language, packageConfig.PackageName, versionSuffix, allPages);
                        Console.WriteLine($"Data saved successfully for {packageConfig.PackageName}.");
                    }
                    else
                    {
                        Console.WriteLine($"No pages found for {packageConfig.PackageName}");
                    }
                }
            }

            Console.WriteLine("All packages processed successfully.");
        }

        static string GetPackagePageOverview(string? language, string? readme, string versionSuffix, string branch = "")
        {
            if (string.IsNullOrEmpty(readme) || string.IsNullOrEmpty(versionSuffix))
            {
                throw new ArgumentException("Readme and VersionSuffix cannot be null or empty.");
            }

            language = language?.ToLower();
            
            if (branch != "main")
            {
                return $"{SDK_API_REVIEW_URL_BASIC}{language}/api/overview/azure/{readme}?{versionSuffix}&branch={branch}";
            }
            return $"{SDK_API_URL_BASIC}{language}/api/overview/azure/{readme}?{versionSuffix}&branch={branch}";
        }

        static string? ChooseGAOrPreview(string? language, string? package)
        {
            language = language?.ToLower();
            string url;

            if (language == "python")
            {
                url = PYTHON_DATA_SDK_RELEASES_LATEST_CSV_URL;
                return CompareGAAndPreview(url, package, language).Result == "GA" ? "view=azure-python" : "view=azure-python-preview";
            }
            else if (language == "java")
            {
                url = JAVA_DATA_SDK_RELEASES_LATEST_CSV_URL;
                return CompareGAAndPreview(url, package, language).Result == "GA" ? "view=azure-java-stable" : "view=azure-java-preview";
            }
            else if (language == "javascript" || language == "js")
            {
                url = JAVASCRIPT_DATA_SDK_RELEASES_LATEST_CSV_URL;
                return CompareGAAndPreview(url, package, language).Result == "GA" ? "view=azure-node-latest" : "view=azure-node-preview";
            }
            else if (language == "dotnet")
            {
                url = DOTNET_DATA_SDK_RELEASES_LATEST_CSV_URL;
                return CompareGAAndPreview(url, package, language).Result == "GA" ? "view=azure-node" : "view=azure-dotnet-preview";
            }
            else
            {
                throw new ArgumentException("Unsupported language specified.");
            }
        }

        static async Task<string?> CompareGAAndPreview(string url, string? package, string language)
        {
            var searchPackage = package;
            
            using (var httpClient = new HttpClient())
            {
                try
                {
                    var csvData = await httpClient.GetStringAsync(url);
                    using (var stringReader = new StringReader(csvData))
                    using (var csvReader = new CsvReader(stringReader, new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        HasHeaderRecord = true,
                    }))
                    {
                        csvReader.Context.RegisterClassMap<PythonPackageMap>();
    
                        var records = new List<PackageCSV>();
                        while (await csvReader.ReadAsync())
                        {
                            var record = csvReader.GetRecord<PackageCSV>();
                            records.Add(record);
                        }
    
                        var res = records.FirstOrDefault(p => p.Package.Equals(searchPackage, StringComparison.OrdinalIgnoreCase));

                        if(res != null)
                        {
                            string versionGA = res.VersionGA;
                            string versionPreview = res.VersionPreview;
                            if(String.IsNullOrEmpty(versionGA) && !String.IsNullOrEmpty(versionPreview))
                            {
                                return "Preview";
                            }
                            else if(!String.IsNullOrEmpty(versionGA) && String.IsNullOrEmpty(versionPreview))
                            {
                                return "GA";
                            }
                            else if(!String.IsNullOrEmpty(versionGA) && !String.IsNullOrEmpty(versionPreview))
                            {
                                var versionRes = CompareVersions(versionGA, versionPreview);
                                return versionRes < 0 ? "Preview" : "GA";
                            }
                            else
                            {
                                Console.WriteLine($"Package {package} has not both GA and Preview version in the table.");
                                return "GA";
                            }
                        }
                        else{
                            Console.WriteLine($"Package {package} not found in the CSV.");
                            return "GA";
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error downloading or parsing CSV: {ex.Message}");
                    return "GA";
                }
            }
        }

        static int CompareVersions(string v1, string v2)
        {
            var (version1Parts, _) = ParseVersion(v1);
            var (version2Parts, _) = ParseVersion(v2);
    
            int length = Math.Max(version1Parts.Length, version2Parts.Length);
            for (int i = 0; i < length; i++)
            {
                int part1 = i < version1Parts.Length ? version1Parts[i] : 0;
                int part2 = i < version2Parts.Length ? version2Parts[i] : 0;
    
                if (part1 < part2) return -1;
                if (part1 > part2) return 1;
            }
    
            return 0;
        }
    
        static (int[], string) ParseVersion(string version)
        {
            var match = Regex.Match(version, @"^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?<pre>[-.\w]*)?$");
            if (!match.Success)
                throw new ArgumentException("Invalid version format");
    
            int major = int.Parse(match.Groups["major"].Value);
            int minor = int.Parse(match.Groups["minor"].Value);
            int patch = int.Parse(match.Groups["patch"].Value);
            string preRelease = match.Groups["pre"].Value;
    
            return (new[] { major, minor, patch }, preRelease);
        }

        static async Task GetAllChildPage(List<string> pages, List<string> allPages, string pagelink, string versionSuffix, string branch, string? cookieName, string? cookieVal)
        {

            // If the current page meets the IsTrue condition, call GetAllPages directly.
            if (IsTrue(pagelink, cookieName, cookieVal))
            {
                
                int lastSlashIndex = pagelink.LastIndexOf('/');
                string baseUri = pagelink.Substring(0, lastSlashIndex + 1);
                allPages.Add(pagelink);
                GetAllPages(pagelink, baseUri, allPages, versionSuffix, branch, cookieName, cookieVal);
                return;
            }

            // Launch a browser
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var context = await ConfigureBrowserContextAsync(browser, branch, cookieName, cookieVal);
            var page = await context.NewPageAsync();

            IReadOnlyList<ILocator> links = new List<ILocator>();

            // Retry 5 times to get the child pages if cannot get pagelinks.
            int i = 0;
            while (links.Count == 0)
            {
                // If the page does not contain the specified content, break the loop
                if (i == 5)
                {
                    break;
                }

                try
                {
                    await page.GotoAsync(pagelink, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.NetworkIdle,
                        Timeout = 60000 // Timeout 60000ms
                    });
                    Console.WriteLine("Page loaded successfully");
                    Console.WriteLine($"Current page: {pagelink}");
                }
                catch (TimeoutException)
                {
                    Console.WriteLine("Page load timeout");
                }
                
                // Get all child pages
                links = await page.Locator("li.tree-item.is-expanded ul.tree-group a").AllAsync();


                i++;
            }

            if (links.Count != 0)
            {
                // Get all href attributes of the child pages
                foreach (var link in links)
                {
                    var href = await link.GetAttributeAsync("href");

                    href = href + "&branch=" + branch;

                    pages.Add(href);
                }

                await browser.CloseAsync();

                // Recursively get all pages of the API reference document
                foreach (var pa in pages)
                {
                    int lastSlashIndex = pa.LastIndexOf('/');
                    string baseUri = pa.Substring(0, lastSlashIndex + 1);
                    allPages.Add(pa);
                    GetAllPages(pa, baseUri, allPages, versionSuffix, branch, cookieName, cookieVal);
                }
            }
        }

        static async Task<IBrowserContext> ConfigureBrowserContextAsync(IBrowser browser, string branch, string? cookieName, string? cookieVal)
        {
            var context = await browser.NewContextAsync();

            if (!string.IsNullOrEmpty(cookieName) && !string.IsNullOrEmpty(cookieVal))
            {
                var cookie = new[]
                {
                    new Microsoft.Playwright.Cookie
                    {
                        Name = cookieName,
                        Value = cookieVal,
                        Domain = "review.learn.microsoft.com",
                        Path = "/"
                    }
                };
                await context.AddCookiesAsync(cookie);
            }

            return context;
        }

        static void GetAllPages(string apiRefDocPage, string? baseUri, List<string> links, string versionSuffix, string branch, string? cookieName, string? cookieVal)
        {
            var doc = FetchHtmlContent(apiRefDocPage, cookieName, cookieVal);

            // The recursion terminates when there are no valid sub pages in the page or when all package links have been visited.
            if (IsTrue(apiRefDocPage, cookieName, cookieVal))
            {
                var aNodes = doc.DocumentNode.SelectNodes("//td/a | //td/span/a");

                if (aNodes != null)
                {
                    foreach (var node in aNodes)
                    {
                        string href = node.Attributes["href"].Value;

                        // Check if the href starts with '#'
                        if (href.StartsWith("#"))
                        {
                            // Skip anchor links
                            continue;
                        }

                        href = $"{baseUri}{href}&branch=" + branch;
                        
                        if (!links.Contains(href))
                        {
                            links.Add(href);

                            // Call GetAllPages method recursively for each new link.
                            GetAllPages(href, baseUri, links, versionSuffix, branch, cookieName, cookieVal);
                        }
                    }
                }
            }
        }
        static bool IsTrue(string link, string? cookieName, string? cookieVal)
        {
            var doc = FetchHtmlContent(link, cookieName, cookieVal);

            var checks = new[]
            {
                new { XPath = "//h1", Content = "Package" },
                new { XPath = "//h1", Content = "Module" },
                new { XPath = "//h2[@id='classes']", Content = "Classes" },
                new { XPath = "//h2[@id='interfaces']", Content = "Interfaces" },
                new { XPath = "//h2[@id='structs']", Content = "Structs" },
                new { XPath = "//h2[@id='typeAliases']", Content = "Type Aliases" },
                new { XPath = "//h2[@id='functions']", Content = "Functions" },
                new { XPath = "//h2[@id='enums']", Content = "Enums" },
                new { XPath = "//h2[@id='modules']", Content = "Modules" },
                new { XPath = "//h2[@id='packages']", Content = "Packages" },
                new { XPath = "//h2[@id='constructors']", Content = "Constructors" },
                new { XPath = "//h2[@id='properties']", Content = "Properties" },
                new { XPath = "//h2[@id='methods']", Content = "Methods" },
                new { XPath = "//h2[@id='operators']", Content = "Operators" }
            };

            return checks.Any(check =>
            {
                string? hNode = doc.DocumentNode.SelectSingleNode(check.XPath)?.InnerText?.Trim();
                if (!string.IsNullOrEmpty(hNode) && hNode.Contains(check.Content))
                {
                    // Check for the presence of <table> tags
                    var tableNode = doc.DocumentNode.SelectSingleNode("//table");
                    if (tableNode != null)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                return false;
            });
        }

        static HtmlDocument FetchHtmlContent(string url, string? cookieName, string? cookieVal)
        {
            // If cookieName and cookieVal is "", use HtmlWeb load page.
            if (string.IsNullOrEmpty(cookieName) && string.IsNullOrEmpty(cookieVal))
            {
                var web = new HtmlWeb();
                return web.Load(url);
            }

            // Else, use HttpClient to load page.
            var handler = new HttpClientHandler
            {
                CookieContainer = new CookieContainer()
            };

            var cookie = new System.Net.Cookie(cookieName ?? string.Empty, cookieVal ?? string.Empty)
            {
                Domain = "review.learn.microsoft.com",
            };
            handler.CookieContainer.Add(cookie);

            using var httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36");

            var response = httpClient.GetAsync(url).Result;
            response.EnsureSuccessStatusCode();

            var htmlContent = response.Content.ReadAsStringAsync().Result;

            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            return doc;
        }

        static async Task GetAllDataSource(List<string> allPages, string? language, string versionSuffix, string pageLink, string cookieName, string cookieValue, string branch)
        {
            List<string> pages = new List<string>();
            List<string> childPage = new List<string>();
            language = language?.ToLower();

            if (language == "js" || language == "javascript")
            {
                await GetChildName(pageLink, childPage, pages);
                GetLanguageChildPage(language, versionSuffix, childPage, pages, branch);
                // Recursively get all pages of the API reference document
                foreach (var pa in pages)
                {
                    int lastSlashIndex = pa.LastIndexOf('/');
                    string baseUri = pa.Substring(0, lastSlashIndex + 1);
                    allPages.Add(pa);
                    GetAllPages(pa, baseUri, allPages, versionSuffix, branch, cookieName, cookieValue);
                }
            }
            else if (language == "python" && versionSuffix.Contains("preview"))
            {
                await GetChildName(pageLink, childPage, pages);
                GetLanguageChildPage(language, versionSuffix, childPage, pages, branch);
                // Recursively get all pages of the API reference document
                foreach (var pa in pages)
                {
                    int lastSlashIndex = pa.LastIndexOf('/');
                    int secondLastSlashIndex = pa.LastIndexOf('/', lastSlashIndex - 1);
                    string baseUri = pa.Substring(0, secondLastSlashIndex + 1);
                    allPages.Add(pa);
                    GetAllPages(pa, baseUri, allPages, versionSuffix, branch, cookieName, cookieValue);
                }
            }
            else
            {
                await GetAllChildPage(pages, allPages, pageLink, versionSuffix, branch, cookieName, cookieValue);
            }
        }

        static async Task GetChildName(string link, List<string> childPages, List<string> pages)
        {
            //Console.Write(link);

            pages.Add(link);
            // Launch a browser
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var context = await browser.NewContextAsync();
            var pageInstance = await context.NewPageAsync();

            try
            {
                // Navigate to the provided link
                await pageInstance.GotoAsync(link, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 60000 // Timeout 60000ms
                });
                Console.WriteLine("Page loaded successfully");
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Page load timeout");
                await browser.CloseAsync();
                return;
            }

            // Get all li tags with aria-level="4"
            var level4Lis = await pageInstance.Locator("li.tree-item[aria-level='4']").AllAsync();

            if (level4Lis.Count != 0)
            {
                // Get the content of each li tag with aria-level="4"
                foreach (var level4Li in level4Lis)
                {
                    var content = await level4Li.InnerTextAsync();
                    if (!string.IsNullOrEmpty(content))
                    {
                        childPages.Add(content);
                    }
                }
            }

            await browser.CloseAsync();
        }

        static List<string> GetLanguageChildPage(string? language, string versionSuffix, List<string> childPage, List<string> childLink, string branch = "")
        {
            string? link = null;
            language = language?.ToLower();
            if(language == "python"){
                foreach (var page in childPage)
                {
                    string packageName = page.Replace(".", "-").ToLower();
                    if(packageName.Contains("fileshare") || packageName.Contains("filedatalake")){
                        packageName = packageName.Replace("file","file-").ToLower();
                    }
                    if (branch != "main")
                    {
                        link = $"{SDK_API_REVIEW_URL_BASIC}{language}/api/{packageName}/{page}/?{versionSuffix}&branch={branch}";
                    }
                    else
                    {
                        link = $"{SDK_API_URL_BASIC}{language}/api/{packageName}/{page}/?{versionSuffix}";
                    }
                    childLink.Add(link);
                }
            }
            else
            {
                foreach (var page in childPage)
                {
                    if (branch != "main")
                    {
                        link = $"{SDK_API_REVIEW_URL_BASIC}{language}/api/{page}/?{versionSuffix}&branch={branch}";
                    }
                    else
                    {
                        link = $"{SDK_API_URL_BASIC}{language}/api/{page}/?{versionSuffix}";
                    }
                    childLink.Add(link);
                }
            }
            return childLink;
        }
    }

    public class PackageCSV
    {
        public string Package { get; set; } = string.Empty;
        public string VersionGA { get; set; } = string.Empty;
        public string VersionPreview { get; set; } = string.Empty;
    }

    public class PythonPackageMap : ClassMap<PackageCSV>
    {
        public PythonPackageMap()
        {
            Map(m => m.Package).Name("Package");
            Map(m => m.VersionGA).Name("VersionGA");
            Map(m => m.VersionPreview).Name("VersionPreview");
        }
    }

    public class PackageConfig
    {
        public string ReadmeName { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string CsvPackageName { get; set; } = string.Empty;
    }

    public class LanguageConfig
    {
        public List<PackageConfig> DotNet { get; set; } = new List<PackageConfig>();
        public List<PackageConfig> Java { get; set; } = new List<PackageConfig>();
        public List<PackageConfig> JavaScript { get; set; } = new List<PackageConfig>();
        public List<PackageConfig> Python { get; set; } = new List<PackageConfig>();
    }

    public class AppConfig
    {
        public string Branch { get; set; } = string.Empty;
        public string CookieName { get; set; } = string.Empty;
        public string CookieValue { get; set; } = string.Empty;
        public LanguageConfig Languages { get; set; } = new LanguageConfig();
    }
}