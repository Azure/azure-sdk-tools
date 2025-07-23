using Azure.Sdk.Tools.Cli.Services;
using System.Text;
using CsvHelper;

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
            return string.Join(
                "\n",
                csvContent
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(ParseCsvLine)
                    .Append(new List<string> { serviceLabel, "", ServiceLabelColorCode })
                    .OrderBy(entry => entry[0], StringComparer.OrdinalIgnoreCase)
                    .Select(entry => {
                        // Use CsvHelper to properly format each line
                        using var writer = new StringWriter();
                        using var csv = new CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture);
                        foreach (var field in entry)
                            csv.WriteField(field);
                        csv.NextRecord();
                        return writer.ToString().TrimEnd();
                    })
            );
        }

        // This should probably be replaced with a 3rd party CSV parser
        // TODO: This should probably be internal or private. (Is there a way to test it if it's private?)
        public static List<string> ParseCsvLine(string line)
        {
            var columns = new List<string>();

            using var reader = new StringReader(line);
            var config = new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture);
            config.BadDataFound = null; // Ignore bad data
            using var csv = new CsvReader(reader, config);

            if (csv.Read())
            {
                var fieldCount = csv.Parser.Count;
                for (int i = 0; i < fieldCount; i++)
                {
                    columns.Add(csv.GetField(i) ?? string.Empty);
                }
            }

            // Ensure we always have at least 3 columns
            while (columns.Count < 3)
            {
                columns.Add("");
            }

            return columns;
        }
    }
}
