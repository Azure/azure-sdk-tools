using Azure.Sdk.Tools.Cli.Services;
using System.Text;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface ILabelHelper
    {
        public string CheckServiceLabel(string csvContent, string serviceName);
        public string CreateServiceLabel(string csvContent, string serviceLabel);
    }
    public class LabelHelper(ILogger<LabelHelper> logger) : ILabelHelper
    {
        internal const string ServiceLabelColorCode = "e99695"; // color code for service labels in common-labels.csv

        public string CheckServiceLabel(string csvContent, string serviceName)
        {
            logger.LogInformation($"Checking service label for: {serviceName}");

            var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var columns = ParseCsvLine(line);

                // CSV format: Label, Description, Color
                if (columns.Count >= 3)
                {
                    var labelName = columns[0].Trim();
                    var colorCode = columns[2].Trim();

                    // Only consider labels with the service label color code and check if it contains the service label
                    if (colorCode.Equals(ServiceLabelColorCode, StringComparison.OrdinalIgnoreCase)
                        && labelName.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        return serviceName;
                    }
                }
            }

            return null;
        }

        public string CreateServiceLabel(string csvContent, string serviceLabel)
        {
            // TODO: Extract this logic, write tests
            // Output should be the resulting CSV string with the new service
            // label added in the right place.
            return "Service1,Description1,e99695\nService2,Description2,e99695...";
        }


        // This should probably be replaced with a 3rd party CSV parser
        // TODO: This should probably be internal or private
        public static List<string> ParseCsvLine(string line)
        {
            var columns = new List<string>();
            var currentColumn = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    columns.Add(currentColumn.ToString());
                    currentColumn.Clear();
                }
                else
                {
                    currentColumn.Append(c);
                }
            }

            // Add the last column
            columns.Add(currentColumn.ToString());

            return columns;
        }
    }
}
