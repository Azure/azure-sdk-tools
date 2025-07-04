using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ReportHelper
{
    public class CompareDate
    {
        public static async Task Main(string[] args)
        {
            using IHost host = Host.CreateApplicationBuilder(args).Build();

            IConfiguration config = host.Services.GetRequiredService<IConfiguration>();

            string HostPackageName = config["PackageName"] ?? "PackageName";
            string language = config["Language"] ?? "Language";
            string owner = config["Owner"] ?? "Owner";
            string repo = config["Repo"] ?? "Repo";
            string githubToken = config["GitHubToken"] ?? "GitHubToken";

            string rootDirectory = ConstData.ReportsDirectory;

            //Results of the last data summary data (maintenance data)
            string allPackagePath = ConstData.LastPipelineAllPackageJsonFilePath!;
            List<TPackage4Json> allPackageList = new List<TPackage4Json>();

            //Data results for the aim package
            List<TResult4Json> oldDataList = new List<TResult4Json>();

            //Results of this time.
            string newDataPath = Path.Combine(rootDirectory, ConstData.TotalIssuesJsonFileName);
            List<TResult4Json> newDataList = new List<TResult4Json>();

            if (File.Exists(newDataPath))
            {
                newDataList = JsonSerializer.Deserialize<List<TResult4Json>>(File.ReadAllText(newDataPath)) ?? new List<TResult4Json>();
            }

            if (allPackagePath != null && File.Exists(allPackagePath))
            {
                allPackageList = JsonSerializer.Deserialize<List<TPackage4Json>>(File.ReadAllText(allPackagePath)) ?? new List<TPackage4Json>();
                // Finding the target package
                foreach (var package in allPackageList)
                {
                    if (package.PackageName == HostPackageName)
                    {
                        oldDataList = package.ResultList ?? new List<TResult4Json>();
                        continue;
                    }
                }
            }

            // Compare the two lists
            List<TResult4Json> differentList = new List<TResult4Json>();
            differentList = CompareLists(oldDataList, newDataList);


            if (differentList.Count != 0)
            {
                // Save the different results to json and excel files
                string differentDataFileName = ConstData.DiffIssuesJsonFileName;
                JsonHelper4Test.AddTestResult(differentList, differentDataFileName);

                string excelFileName = ConstData.DiffIssuesExcelFileName;
                string differentSheetName = "DiffSheet";
                ExcelHelper4Test.AddTestResult(differentList, excelFileName, differentSheetName);

                // Update github issues
                await GithubHelper.CreateOrUpdateGitHubIssue(owner, repo, githubToken, HostPackageName, language);
            }else if (newDataList.Count != 0)
            {
                // Create github issues
                await GithubHelper.CreateOrUpdateGitHubIssue(owner, repo, githubToken, HostPackageName, language);

            }else
            {
                Console.WriteLine($"There are no content validation issue with {HostPackageName} in {language}.");
            }
        }
        public static List<TResult4Json> CompareLists(List<TResult4Json> oldDataList, List<TResult4Json> newDataList)
        {
            List<TResult4Json> differentList = new List<TResult4Json>();

            foreach (var newItem in newDataList)
            {
                var matchedOldItem = oldDataList.FirstOrDefault(oldItem =>
                        oldItem.TestCase == newItem.TestCase &&
                        oldItem.ErrorInfo == newItem.ErrorInfo &&
                        oldItem.ErrorLink == newItem.ErrorLink
                    );
                //  new TResult is diffrent
                if (matchedOldItem == null)
                {
                    differentList.Add(newItem);
                    continue;
                }
                // new TResult is same , but locations of error is diffrent
                List<string> differentLocationsList = CompareOfLocations(matchedOldItem.LocationsOfErrors!, newItem.LocationsOfErrors!);
                if (differentLocationsList.Count > 0)
                {
                    newItem.LocationsOfErrors = differentLocationsList;
                    differentList.Add(newItem);
                    continue;
                }
            }
            return differentList;
        }
        public static List<string> CompareOfLocations(List<string> oldLocationsList, List<string> newLocationsList)
        {
            List<string> differentLocationsList = new List<string>();
            if (newLocationsList == null && oldLocationsList == null) { return differentLocationsList; }

            if (oldLocationsList == null || newLocationsList == null) { return differentLocationsList; }

            var processedOldLocationsList = oldLocationsList
                    .Select(location => location.Contains(".") ? location.Substring(location.IndexOf(".") + 1) : location)
                    .ToList();

            int count = 0;

            foreach (var location in newLocationsList)
            {
                string str = location.Contains(".") ? location.Substring(location.IndexOf(".") + 1) : location;
                // If new location is not in old locations, they are different
                if (!processedOldLocationsList.Contains(str))
                {
                    differentLocationsList.Add($"{++count}.{str}");
                }
            }
            return differentLocationsList;
        }
    }
}