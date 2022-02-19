using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using APIViewWeb.Models;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;

namespace APIViewWeb.Repositories
{

    public class PackageNameManager
    {
        static readonly string PACKAGE_CSV_LOOKUP_URL = "https://raw.githubusercontent.com/Azure/azure-sdk/main/_data/releases/latest/<langauge>-packages.csv";
        static readonly HttpClient _httpClient = new();

        static Dictionary<string, PackageModel> _packageNameMap = new();
        static TelemetryClient _telemetryClient = new(TelemetryConfiguration.CreateDefault());

        public PackageNameManager()
        {
            LoadPackageDisplayName("python");
            LoadPackageDisplayName("java");
            LoadPackageDisplayName("dotnet");
            LoadPackageDisplayName("js");
            LoadPackageDisplayName("go");
            LoadPackageDisplayName("cpp");
            LoadPackageDisplayName("c");
            LoadPackageDisplayName("ios");
        }

        public PackageModel GetPackageDetails(string packageName)
        {
            if (packageName != null)
            {
                if (_packageNameMap.ContainsKey(packageName))
                {
                    return _packageNameMap[packageName];
                }
            }
            return null;
        }

        private static void LoadPackageDisplayName(string language)
        {
            var url = PACKAGE_CSV_LOOKUP_URL.Replace("<langauge>", language.ToLower());
            try
            {
                var respTask = _httpClient.GetAsync(url);
                respTask.Wait();
                var resp = respTask.Result;
                resp.EnsureSuccessStatusCode();
                var contentStreamTask = resp.Content.ReadAsStreamAsync();
                contentStreamTask.Wait();
                var contentStream = contentStreamTask.Result;
                var csvReaderConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    MissingFieldFound = null,
                    BadDataFound = null,
                };
                using (var csvReader = new StreamReader(contentStream))
                using (var packageCsv = new CsvReader(csvReader, csvReaderConfig))
                {
                    packageCsv.Read();
                    packageCsv.ReadHeader();
                    while (packageCsv.Read())
                    {
                        try
                        {
                            var p = packageCsv.GetRecord<PackageModel>();
                            var key = p.Name;
                            if (!string.IsNullOrEmpty(p.GroupId))
                            {
                                key = p.GroupId + ":" + p.Name;
                            }
                            if (!_packageNameMap.ContainsKey(key))
                            {
                                if (string.IsNullOrEmpty(p.DisplayName))
                                {
                                    p.DisplayName = "Other";
                                }

                                if (string.IsNullOrEmpty(p.ServiceName))
                                {
                                    p.ServiceName = "Other";
                                }
                                _packageNameMap[key] = p;
                            }
                        }
                        catch (Exception ex)
                        {
                            _telemetryClient.TrackException(ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex);
            }
        }
    }
}
