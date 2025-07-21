// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Models;
using System.Text;


namespace Azure.Sdk.Tools.Cli.Tools
{

    [McpServerToolType, Description("Tools for working with GitHub service labels from the Azure SDK common labels CSV")]
    public class GitHubLabelsTool(ILogger<GitHubLabelsTool> logger, IOutputService output, IGitHubService githubService) : MCPTool
    {
        private const string serviceLabelColorCode = "e99695";
        
        private readonly Argument<string> _serviceLabelArg = new Argument<string>(
            name: "service-label",
            description: "The service label to check in the common labels CSV"
        )
        {
            Arity = ArgumentArity.ExactlyOne
        };

        public override Command GetCommand()
        {
            Command command = new("github-labels", "GitHub service labels tools");
            
            var checkServiceLabelCommand = new Command("check-service-label", "Check if a service label exists in the common labels CSV");
            checkServiceLabelCommand.AddArgument(_serviceLabelArg);
            checkServiceLabelCommand.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            
            command.AddCommand(checkServiceLabelCommand);
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            string serviceLabel = ctx.ParseResult.GetValueForArgument(_serviceLabelArg);
            var result = await CheckServiceLabel(serviceLabel);
            ctx.ExitCode = ExitCode;
            output.Output(result);
        }

        [McpServerTool(Name = "CheckServiceLabel"), Description("Checks if a service label exists in the common-labels.csv and returns the color code (e99695) if found")]
        public async Task<ServiceLabelResponse> CheckServiceLabel(string serviceLabel)
        {
            try
            {
                logger.LogInformation("Checking service label: {serviceLabel}", serviceLabel);

                // Download the CSV content
                var contents = await githubService.GetContentsAsync("Azure", "azure-sdk-tools", "tools/github/data/common-labels.csv");
                
                if (contents == null || contents.Count == 0)
                {
                    throw new InvalidOperationException("Could not retrieve common-labels.csv file");
                }

                // Get the first (and should be only) file content
                var csvContent = contents[0].Content;
                
                if (string.IsNullOrEmpty(csvContent))
                {
                    throw new InvalidOperationException("common-labels.csv file is empty");
                }

                // Parse CSV and look for the service label
                var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                // Skip the header line if it exists
                var dataLines = lines.Skip(1);
                
                foreach (var line in dataLines)
                {
                    var columns = ParseCsvLine(line);
                    
                    // CSV format: Label, Description, Color
                    if (columns.Count >= 3)
                    {
                        string labelName = columns[0].Trim();
                        string description = columns[1].Trim();
                        string colorCode = columns[2].Trim();
                        
                        // Only consider labels with the expected color code and check if it contains the service label
                        if (colorCode.Equals(serviceLabelColorCode, StringComparison.OrdinalIgnoreCase) && 
                            labelName.Contains(serviceLabel, StringComparison.OrdinalIgnoreCase))
                        {
                            logger.LogInformation("Found service label match: '{inputLabel}' -> '{actualLabel}'", serviceLabel, labelName);
                            
                            return new ServiceLabelResponse
                            {
                                ServiceLabel = labelName,
                                Found = true,
                                ColorCode = colorCode,
                                Description = string.IsNullOrEmpty(description) ? null : description
                            };
                        }
                    }
                }

                logger.LogInformation("Service label '{serviceLabel}' not found in common labels CSV", serviceLabel);
                
                return new ServiceLabelResponse
                {
                    ServiceLabel = serviceLabel,
                    Found = false,
                    ColorCode = null,
                    Description = null
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while checking service label: {serviceLabel}", serviceLabel);
                SetFailure();
                return new ServiceLabelResponse
                {
                    ServiceLabel = serviceLabel,
                    Found = false,
                    ResponseError = $"Error occurred while checking service label '{serviceLabel}': {ex.Message}"
                };
            }
        }

        private static List<string> ParseCsvLine(string line)
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

    public class ServiceLabelResponse
    {
        public string ServiceLabel { get; set; } = "";
        public bool Found { get; set; }
        public string? ColorCode { get; set; }
        public string? Description { get; set; }
        public string? ResponseError { get; set; }
    }
}
