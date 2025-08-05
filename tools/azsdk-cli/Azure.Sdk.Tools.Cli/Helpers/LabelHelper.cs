using Azure.Sdk.Tools.Cli.Services;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Azure.Sdk.Tools.Cli.Configuration;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface ILabelHelper
    {
        public LabelHelper.ServiceLabelStatus CheckServiceLabel(string csvContent, string serviceName);
        public string CreateServiceLabel(string csvContent, string serviceLabel);
        public string NormalizeLabel(string label);
    }

    public class LabelHelper(ILogger<LabelHelper> logger) : ILabelHelper
    {

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
            NotAServiceLabel
        }

        public ServiceLabelStatus CheckServiceLabel(string csvContent, string serviceName)
        {
            var records = GetLabelsFromCsv(csvContent);

            foreach (var record in records)
            {
                if (record.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
                {
                    if (record.Color.Equals(Constants.SERVICE_LABELS_COLOR_CODE, StringComparison.OrdinalIgnoreCase))
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

        public string CreateServiceLabel(string csvContent, string serviceLabel)
        {
            IList<LabelData> records;

            records = GetLabelsFromCsv(csvContent);

            var newRecords = records
                .Append(new LabelData { Name = serviceLabel, Description = "", Color = Constants.SERVICE_LABELS_COLOR_CODE })
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
