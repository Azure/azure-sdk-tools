using Microsoft.Playwright;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace PendingTestingPackagesThisMonth
{
    public class PendingTestingPackagesThisMonth
    {
        private static readonly string RELEASE_PACKAGES_URL_PREFIX = "https://github.com/Azure/azure-sdk/tree/main/_data/releases/";
        private static readonly string FILTER_PACKAGES_FOR_PYTHON_PATH = "filter-packages-config-python.json";
        private static readonly string FILTER_PACKAGES_FOR_JAVA_PATH = "filter-packages-config-java.json";
        private static readonly string FILTER_PACKAGES_FOR_JAVASCRIPT_PATH = "filter-packages-config-js.json";
        private static readonly string FILTER_PACKAGES_FOR_NET_PATH = "filter-packages-config-dotnet.json";
        private IPlaywright _playwright;

        public PendingTestingPackagesThisMonth(IPlaywright playwright)
        {
            _playwright = playwright ?? throw new ArgumentNullException(nameof(playwright));
        }
        static async Task Main(string[] args)
        {
            // Default Configuration
            using IHost host = Host.CreateApplicationBuilder(args).Build();
            IConfiguration config = host.Services.GetRequiredService<IConfiguration>();
            string? language = config["Language"];
            string packagesListFilePath = "../../../../../tools/content-validation/" + config["PackagesListFilePath"];

            // Initialize Playwright instance.
            if (string.IsNullOrEmpty(language))
            {
                throw new ArgumentException("Language must be specified in the configuration.");
            }
            else if (language.ToLower() == "javascript")
            {
                language = "js";
            }
            IPlaywright playwright = Playwright.CreateAsync().GetAwaiter().GetResult();
            PendingTestingPackagesThisMonth data = new PendingTestingPackagesThisMonth(playwright);

            /*
            ** Retrieve the current system date and pick the corresponding folder from the GitHub repo.
            ** The folder name is expected to be in the format "YYYY-MM".
            ** For example, if the current date is August 11, 2025, the folder name would be "2025-07".
            ** Notes: Test the packages released last month, as the current month's may not have all been released yet.
            */
            var currentDate = DateTime.Now;
            var folderName = currentDate.Month == 1
                ? $"{currentDate.Year - 1}-12"
                : $"{currentDate.Year}-{currentDate.Month - 1:D2}";
            var dataUrl = $"{RELEASE_PACKAGES_URL_PREFIX}{folderName}/{language.ToLower()}.yml";

            var packages = await data.FetchPackages(dataUrl, language, packagesListFilePath);
        }

        public async Task<string> FetchPackages(string testLink, string language, string packagesListFilePath)
        {
            var browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();
            await PlaywrightHelper.GotoageWithRetriesAsync(page, testLink);

            // Fetch content of textarea#read-only-cursor-text-area in {language}.yml file.
            var textAreaContent = await page.EvaluateAsync<string>("() => document.querySelector('textarea#read-only-cursor-text-area')?.value");

            var lines = textAreaContent.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new HashSet<string>();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("- Name:"))
                {
                    var packageName = trimmedLine.Substring("- Name:".Length).Trim();
                    if (!string.IsNullOrEmpty(packageName))
                    {
                        result.Add(packageName.Replace("'", ""));
                    }
                }
            }

            HashSet<string> filteredResult = language.ToLower() switch
            {
                "python" => await PythonFilterPackages(result, packagesListFilePath),
                "java" => await JavaFilterPackages(result, packagesListFilePath),
                "dotnet" => await DotNetFilterPackages(result, packagesListFilePath),
                "js" => await JavaScriptFilterPackages(result, packagesListFilePath),
                _ => result
            };

            // Convert to JSON format
            var jsonResult = JsonSerializer.Serialize(filteredResult.ToList(), new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "result.json");
            await File.WriteAllTextAsync(outputPath, jsonResult);

            return jsonResult == null ? throw new InvalidOperationException("Failed to serialize packages to JSON.") : jsonResult;
        }

        public async Task<HashSet<string>> PythonFilterPackages(HashSet<string> result, string packagesListFilePath)
        {
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), FILTER_PACKAGES_FOR_PYTHON_PATH);

            // Resolve filter packages in filter list.
            if (File.Exists(configPath))
            {
                var configContent = await File.ReadAllTextAsync(configPath);
                var configJson = JsonSerializer.Deserialize<List<Dictionary<string, List<string>>>>(configContent);
                var packages = configJson?.FirstOrDefault()?["packages"] ?? new List<string>();

                result.RemoveWhere(packageName => packageName.StartsWith("azure-mgmt-"));
                result.RemoveWhere(packageName => packageName.StartsWith("azure-cognitiveservices-"));
                foreach (var pkg in packages)
                {
                    result.Remove(pkg);
                }
            }
            else
            {
                Console.WriteLine($"Warning: Configuration file '{FILTER_PACKAGES_FOR_PYTHON_PATH}' not found. No packages will be filtered.");
            }

            var joinedResult = string.Join(",", result);

            var outputPath = Path.Combine(AppContext.BaseDirectory, packagesListFilePath);
            await File.WriteAllTextAsync(outputPath, joinedResult);

            return result;
        }

        public async Task<HashSet<string>> JavaFilterPackages(HashSet<string> result, string packagesListFilePath)
        {
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), FILTER_PACKAGES_FOR_JAVA_PATH);

            // Resolve filter packages in filter list.
            if (File.Exists(configPath))
            {
                var configContent = await File.ReadAllTextAsync(configPath);
                var configJson = JsonSerializer.Deserialize<List<Dictionary<string, List<string>>>>(configContent);
                var packages = configJson?.FirstOrDefault()?["packages"] ?? new List<string>();

                result.RemoveWhere(packageName => packageName.StartsWith("azure-resourcemanager-"));
                result.RemoveWhere(packageName => packageName.StartsWith("spring-cloud-"));
                foreach (var pkg in packages)
                {
                    result.Remove(pkg);
                }
            }
            else
            {
                Console.WriteLine($"Warning: Configuration file '{FILTER_PACKAGES_FOR_JAVA_PATH}' not found. No packages will be filtered.");
            }

            var joinedResult = string.Join(",", result);
            var outputPath = Path.Combine(AppContext.BaseDirectory, packagesListFilePath);
            await File.WriteAllTextAsync(outputPath, joinedResult);

            return result;
        }

        public async Task<HashSet<string>> DotNetFilterPackages(HashSet<string> result, string packagesListFilePath)
        {
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), FILTER_PACKAGES_FOR_NET_PATH);

            // Resolve filter packages in filter list.
            if (File.Exists(configPath))
            {
                var configContent = await File.ReadAllTextAsync(configPath);
                var configJson = JsonSerializer.Deserialize<List<Dictionary<string, List<string>>>>(configContent);
                var packages = configJson?.FirstOrDefault()?["packages"] ?? new List<string>();

                result.RemoveWhere(packageName => packageName.StartsWith("Azure.ResourceManager."));
                foreach (var pkg in packages)
                {
                    result.Remove(pkg);
                }
            }
            else
            {
                Console.WriteLine($"Warning: Configuration file '{FILTER_PACKAGES_FOR_PYTHON_PATH}' not found. No packages will be filtered.");
            }

            // Update package names to lowercase and replace "." with "-"
            var updatedPackages = result.Select(p => p.Replace(".", "-").ToLower()).ToList();

            var joinedResult = string.Join(",", updatedPackages);

            var outputPath = Path.Combine(AppContext.BaseDirectory, packagesListFilePath);
            await File.WriteAllTextAsync(outputPath, joinedResult);

            return result;
        }

        public async Task<HashSet<string>> JavaScriptFilterPackages(HashSet<string> result, string packagesListFilePath)
        {
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), FILTER_PACKAGES_FOR_JAVASCRIPT_PATH);

            // Resolve filter packages in filter list.
            if (File.Exists(configPath))
            {
                var configContent = await File.ReadAllTextAsync(configPath);
                var configJson = JsonSerializer.Deserialize<List<Dictionary<string, List<string>>>>(configContent);
                var packages = configJson?.FirstOrDefault()?["packages"] ?? new List<string>();

                result.RemoveWhere(packageName => packageName.StartsWith("@azure-rest/"));
                result.RemoveWhere(packageName => packageName.StartsWith("@azure/arm-"));
                foreach (var pkg in packages)
                {
                    result.Remove(pkg);
                }
            }
            else
            {
                Console.WriteLine($"Warning: Configuration file '{FILTER_PACKAGES_FOR_JAVASCRIPT_PATH}' not found. No packages will be filtered.");
            }

            // Update package names to lowercase and replace "." with "-"
            var updatedPackages = result.Select(p => p.Replace("@", "").Replace("/", "-").ToLower()).ToList();

            var joinedResult = string.Join(",", updatedPackages);

            var outputPath = Path.Combine(AppContext.BaseDirectory, packagesListFilePath);
            await File.WriteAllTextAsync(outputPath, joinedResult);

            return result;
        }
    }
}
