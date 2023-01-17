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

namespace APIViewWeb.Managers
{

    public class PackageNameManager : IPackageNameManager
    {
        static readonly string PACKAGE_CSV_LOOKUP_URL = "https://raw.githubusercontent.com/Azure/azure-sdk/main/_data/releases/latest/<langauge>-packages.csv";
        static readonly HttpClient _httpClient = new();

        static Dictionary<string, PackageModel> _packageNameMap = new();
        static TelemetryClient _telemetryClient = new(TelemetryConfiguration.CreateDefault());

        public async Task<PackageModel> GetPackageDetails(string packageName)
        {
            if (_packageNameMap.Count == 0)
            {
                await LoadPackageDisplayName("python");
                await LoadPackageDisplayName("java");
                await LoadPackageDisplayName("dotnet");
                await LoadPackageDisplayName("js");
                await LoadPackageDisplayName("go");
                await LoadPackageDisplayName("cpp");
                await LoadPackageDisplayName("c");
                await LoadPackageDisplayName("ios");
            }

            if (packageName != null)
            {
                if (_packageNameMap.ContainsKey(packageName))
                {
                    return _packageNameMap[packageName];
                }
            }
            return null;
        }

        private async Task LoadPackageDisplayName(string language)
        {
            var url = PACKAGE_CSV_LOOKUP_URL.Replace("<langauge>", language.ToLower());
            try
            {
                var resp = await _httpClient.GetAsync(url);
                resp.EnsureSuccessStatusCode();
                var contentStream = await resp.Content.ReadAsStreamAsync();
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
