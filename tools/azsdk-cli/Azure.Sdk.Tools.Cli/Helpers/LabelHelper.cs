using Azure.Sdk.Tools.Cli.Services;
using System.Text;
using CsvHelper;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface ILabelHelper
    {
        public bool CheckServiceLabel(string csvContent, string serviceName);
        public string CreateServiceLabel(string csvContent, string serviceLabel);
    }
    public class LabelHelper(ILogger<LabelHelper> logger) : ILabelHelper
    {
        internal const string ServiceLabelColorCode = "e99695"; // color code for service labels in common-labels.csv

        public bool CheckServiceLabel(string csvContent, string serviceName)
        {
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
                        return true;
                    }
                }
            }
            
            return false;
        }

        public string CreateServiceLabel(string csvContent, string serviceLabel)
        {
            // TODO: Extract this logic, write tests
            // Output should be the resulting CSV string with the new service
            // label added in the right place.
            return "Service1,Description1,e99695\nService2,Description2,e99695...";
        }

        // should be private or internal, but used in tests
        public static List<string> ParseCsvLine(string line)
        {
            var columns = new List<string>();

            using var reader = new StringReader(line);
            using var csv = new CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture);

            // Read & parse the CSV line
            if (csv.Read())
            {
                var fieldCount = csv.Parser.Count; // number of fields in the current record
                for (int i = 0; i < fieldCount; i++)
                {
                    columns.Add(csv.GetField(i) ?? string.Empty);
                }
            }

            return columns;
        }
    }
}
