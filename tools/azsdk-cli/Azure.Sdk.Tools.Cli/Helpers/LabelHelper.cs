using Azure.Sdk.Tools.Cli.Services;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface ILabelHelper
    {
        public LabelHelper.ServiceLabelStatus CheckServiceLabel(string csvContent, string serviceName);
        public string CreateServiceLabel(string csvContent, string serviceLabel);
        public string NormalizeLabel(string label);
        public bool CheckServiceLabelInReview(IReadOnlyList<Octokit.PullRequest?> pullRequests, string serviceLabel);
    }

    public class LabelHelper(ILogger<LabelHelper> logger) : ILabelHelper
    {
        internal const string ServiceLabelColorCode = "e99695"; // color code for service labels in common-labels.csv

        public static IList<LabelData> GetLabelsFromCsv(string csvContent)
        {
            using var reader = new StringReader(csvContent);
            using var csvReader = new CsvReader(reader, config);
            return csvReader.GetRecords<LabelData>().ToList();
        }

        private static CsvConfiguration config = new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            NewLine = "\n",
        };

        public enum ServiceLabelStatus
        {
            Exists,
            DoesNotExist,
            NotAServiceLabel,
            InReview
        }

        public ServiceLabelStatus CheckServiceLabel(string csvContent, string serviceName)
        {
            var records = GetLabelsFromCsv(csvContent);

            foreach (var record in records)
            {
                if (record.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
                {
                    if (record.Color.Equals(ServiceLabelColorCode, StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogInformation($"Service label '{serviceName}' exists in common-labels.csv.");
                        return ServiceLabelStatus.Exists;
                    }
                    else
                    {
                        logger.LogWarning($"Label '{serviceName}' exists but is not a service label.");
                        return ServiceLabelStatus.NotAServiceLabel;
                    }
                }

            }

            return ServiceLabelStatus.DoesNotExist;
        }

        public bool CheckServiceLabelInReview(IReadOnlyList<Octokit.PullRequest?> pullRequests, string serviceLabel)
        {
            try
            {
                if (pullRequests == null || !pullRequests.Any())
                {
                    logger.LogInformation("No pull request found for service labels");
                    return false;
                }

                foreach (var pr in pullRequests.Where(p => p != null))
                {
                    if (pr != null && pr.Title.Contains(serviceLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                logger.LogInformation($"No pull requests found for {serviceLabel}.");
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error retrieving pull requests: {ex.Message}");
                return false;
            }
        }

        public string CreateServiceLabel(string csvContent, string serviceLabel)
        {
            IList<LabelData> records;

            records = GetLabelsFromCsv(csvContent);

            var newRecords = records
                .Append(new LabelData { Name = serviceLabel, Description = "", Color = ServiceLabelColorCode })
                .OrderBy(label => label.Name, StringComparer.Ordinal);

            using var writer = new StringWriter();
            using var csvWriter = new CsvWriter(writer, config);
            csvWriter.WriteRecords(newRecords);
            return writer.ToString();
        }

        public string NormalizeLabel(string label)
        {
            var normalizedLabel = label
                    .Replace(" - ", "-")
                    .Replace(" ", "-")
                    .Replace("/", "-")
                    .Replace("_", "-")
                    .Trim('-')
                    .ToLowerInvariant();
            return normalizedLabel;
        }
    }

    public class LabelData
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Color { get; set; }
    }
}
