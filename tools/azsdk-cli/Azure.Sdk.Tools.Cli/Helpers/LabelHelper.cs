using Azure.Sdk.Tools.Cli.Services;
using System.Text;

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
        internal const string ServiceLabelColorCode = "e99695"; // color code for service labels in common-labels.csv

        public enum ServiceLabelStatus
        {
            Exists,
            DoesNotExist,
            NotAServiceLabel
        }

        public ServiceLabelStatus CheckServiceLabel(string csvContent, string serviceName)
        {
            using var reader = new StringReader(csvContent);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var commaIndices = new List<int>();
                for (int i = 0; i < line.Length; i++)
                {
                    if (line[i] == ',')
                        commaIndices.Add(i);
                }

                if (commaIndices.Count < 2)
                    continue; // Skip lines that don't have at least 2 commas

                // Label is everything before the first comma
                var label = line.Substring(0, commaIndices[0]).Trim();
                
                // Color is everything after the last comma
                var color = line.Substring(commaIndices[commaIndices.Count - 1] + 1).Trim();
                
                // Check if this is the service we're looking for
                if (label.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
                {
                    // Check if it's a service label by color code
                    if (color.Equals(ServiceLabelColorCode, StringComparison.OrdinalIgnoreCase))
                    {
                        return ServiceLabelStatus.Exists;
                    }
                    else
                    {
                        return ServiceLabelStatus.NotAServiceLabel;
                    }
                }
            }

            return ServiceLabelStatus.DoesNotExist;
        }

        public string CreateServiceLabel(string csvContent, string serviceLabel)
        {
            List<string> lines = csvContent.Split("\n").ToList();

            var newServiceLabel = $"{serviceLabel},,{ServiceLabelColorCode}";

            bool inserted = false;
            for (int i = 0; i < lines.Count; i++)
            {
                if (string.Compare(newServiceLabel, lines[i], StringComparison.Ordinal) < 0)
                {
                    lines.Insert(i, newServiceLabel);
                    inserted = true;
                    break;
                }
            }

            // If not inserted yet, add at the end
            if (!inserted)
            {
                lines.Add(newServiceLabel);
            }

            return string.Join("\n", lines);
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
