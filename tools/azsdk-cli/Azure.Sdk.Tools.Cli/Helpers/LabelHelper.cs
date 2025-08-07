using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Services;
using System.Text;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public static class LabelHelper
    {

        public enum ServiceLabelStatus
        {
            Exists,
            DoesNotExist,
            NotAServiceLabel
        }

        public static ServiceLabelStatus CheckServiceLabel(string csvContent, string serviceName)
        {
            using var reader = new StringReader(csvContent);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var commaIndices = line.Split(",");

                if (commaIndices.Length < 2)
                {
                    continue; // Skip lines that don't have at least 2 elements
                }

                // Label is the first part (before first comma)
                var label = commaIndices[0].Trim();
                
                // Color is the last part (after last comma)
                var color = commaIndices[commaIndices.Length - 1].Trim();
                
                // Check if this is the service we're looking for
                if (label.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
                {
                    // Check if it's a service label by color code
                    return color.Equals(Constants.SERVICE_LABELS_COLOR_CODE, StringComparison.OrdinalIgnoreCase) ? ServiceLabelStatus.Exists : ServiceLabelStatus.NotAServiceLabel;
                }
            }

            return ServiceLabelStatus.DoesNotExist;
        }

        public static string CreateServiceLabel(string csvContent, string serviceLabel)
        {
            // Filter out empty or whitespace-only lines
            List<string> lines = csvContent.Split("\n")
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            var newServiceLabel = $"{serviceLabel},,{Constants.SERVICE_LABELS_COLOR_CODE}";

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

        public static string NormalizeLabel(string label)
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
