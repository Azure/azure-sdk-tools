using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Services;
using System.Text;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public static class LabelHelper
    {
        public const string SERVICE_LABELS_COLOR_CODE = "e99695";

        public enum ServiceLabelStatus
        {
            Exists,
            DoesNotExist,
            NotAServiceLabel,
            InReview
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

                var columns = line.Split(",");

                if (columns.Length <= 2)
                {
                    continue; // Skip lines that don't have at least 3 columns
                }

                // Label is the first part (before first comma)
                var label = columns[0].Trim();
                
                // Color is the last part (after last comma)
                var color = columns[columns.Length - 1].Trim();
                
                // Check if this is the service we're looking for
                if (label.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
                {
                    // Check if it's a service label by color code
                    if (color.Equals(SERVICE_LABELS_COLOR_CODE, StringComparison.OrdinalIgnoreCase))
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

        public static bool CheckServiceLabelInReview(IReadOnlyList<Octokit.PullRequest?> pullRequests, string serviceLabel)
        {
            if (pullRequests == null || pullRequests.Count == 0 || string.IsNullOrWhiteSpace(serviceLabel))
            {
                return false;
            }

            foreach (var pr in pullRequests)
            {
                if (pr == null)
                {
                    continue;
                }

                var headLabel = pr.Head?.Label;
                if (string.IsNullOrEmpty(headLabel))
                {
                    continue;
                }

                if (!headLabel.Contains(serviceLabel, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var labels = pr.Labels;
                if (labels == null)
                {
                    continue;
                }

                if (labels.Any(l => string.Equals(l?.Name, "Created by copilot", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        public static string CreateServiceLabel(string csvContent, string serviceLabel)
        {
            // Filter out empty or whitespace-only lines
            List<string> lines = csvContent.Split("\n")
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            var newServiceLabel = $"{serviceLabel},,{SERVICE_LABELS_COLOR_CODE}";

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

            return string.Join("\n", lines) + "\n";
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
}
