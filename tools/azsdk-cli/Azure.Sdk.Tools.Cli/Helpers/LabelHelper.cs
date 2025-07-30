using Azure.Sdk.Tools.Cli.Services;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface ILabelHelper
    {
        public LabelHelper.ResultType CheckServiceLabel(string csvContent, string serviceName);
        public string CreateServiceLabel(string csvContent, string serviceLabel);
        public string NormalizeLabel(string label);
    }

    public class LabelHelper(ILogger<LabelHelper> logger) : ILabelHelper
    {
        internal const string ServiceLabelColorCode = "e99695"; // color code for service labels in common-labels.csv

        private List<LabelData> getLabelsFromCsv(string csvContent)
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

        public enum ResultType
        {
            Exists,
            DoesNotExist,
            NotAServiceLabel,
            InReview
        }

        public ResultType CheckServiceLabel(string csvContent, string serviceName)
        {
            var records = getLabelsFromCsv(csvContent);

            foreach (var record in records)
            {
                if (record.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
                {
                    if (record.Color.Equals(ServiceLabelColorCode, StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogInformation($"Service label '{serviceName}' exists in common-labels.csv.");
                        return ResultType.Exists;
                    }
                    else
                    {
                        logger.LogWarning($"Label '{serviceName}' exists but is not a service label.");
                        return ResultType.NotAServiceLabel;
                    }
                }

            }

            return ResultType.DoesNotExist;
        }

        public string CreateServiceLabel(string csvContent, string serviceLabel)
        {
            List<LabelData> records;

            records = getLabelsFromCsv(csvContent);

            var newRecords = records
                .Append(new LabelData { Name = serviceLabel, Description = "", Color = ServiceLabelColorCode })
                .OrderBy(label => label.Name, StringComparer.Ordinal);

            var writer = new StringWriter();
            var csvWriter = new CsvWriter(writer, config);
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
