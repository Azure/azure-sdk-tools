using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;
using Azure.Sdk.Tools.Cli.Benchmarks.Interaction;
using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Scenarios.TypeSpec
{
    /// <summary>
    /// Represents a TypeSpec authoring scenario that can be loaded dynamically from JSON test case files.
    /// These scenarios test TypeSpec authoring operations in the azure-rest-api-specs repository,
    /// such as adding resources, modifying models, or updating operations.
    /// Scenarios are loaded from TestData/TypeSpec/*.json files during discovery.
    /// </summary>
    public class AuthoringScenario : BenchmarkScenario
    {
        private const string DefaultTspProjectPath = "specification/widget/resource-manager/Microsoft.Widget/Widget";

        /// <inheritdoc />
        public override string Name { get; }

        /// <inheritdoc />
        public override string Description { get; }

        /// <inheritdoc />
        public override string Prompt { get; }

        /// <inheritdoc />
        public override RepoConfig Repo { get; }

        /// <inheritdoc />
        public override IReadOnlyList<QuestionAndAnswer>? QuestionAndAnswers { get; }

        /// <inheritdoc />
        public override IEnumerable<IValidator> Validators { get; }

        public string TspProjectPath { get; }

        public IReadOnlyList<string> ToolsToCall { get; set; } = new List<string>() { "azure-typespec-author", "azsdk-azsdk_typespec_generate_authoring_plan" };

        /// <summary>
        /// Gets or sets the verification plan for validating the scenario results.
        /// Contains the strategy or criteria used to verify that the agent completed the task correctly.
        /// </summary>
        public IReadOnlyList<string> VerifyPlan { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the list of TypeSpec test files relevant to this scenario.
        /// These files may be used for validation or reference during scenario execution.
        /// </summary>
        public List<string> TestTspFiles { get; set; } = new List<string>();

        public string? AuthoringSpecRepo { get; private set; }
        public string? AuthoringSkillPath { get; set; }

        /// <inheritdoc />
        public override string[] Tags => ["typespec", "authoring"];

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthoringScenario"/> class.
        /// </summary>
        /// <param name="name">The unique name of the scenario.</param>
        /// <param name="description">The description of what the scenario tests.</param>
        /// <param name="prompt">The prompt to send to the agent.</param>
        /// <param name="tspProjectPath">Optional TypeSpec project path. If null or empty, uses the default path.</param>
        /// <param name="testTspFiles">Optional list of TypeSpec files relevant to this scenario.</param>
        /// <param name="toolsToCall">Optional list of tools to call during the scenario.</param>
        /// <param name="verifyPlan">Optional verification plan for validating scenario results.</param>
        /// <param name="questionAndAnswers">Optional list of questions and answers for agent interaction.</param>
        /// <param name="authoringSpecRepo">Optional repository for authoring specs.</param>
        /// <param name="authoringSkillPath">Optional path for authoring skills.</param>
        public AuthoringScenario(string name, string description, string prompt, string? tspProjectPath, List<string>? testTspFiles = null, List<string>? toolsToCall = null, List<string>? verifyPlan = null, List<QuestionAndAnswer>? questionAndAnswers = null)
        {
            Name = name;
            Description = description ?? string.Empty;
            Prompt = prompt ?? string.Empty;
            TspProjectPath = string.IsNullOrWhiteSpace(tspProjectPath) ? DefaultTspProjectPath : tspProjectPath;
            if (toolsToCall != null && toolsToCall.Any())
            {
                ToolsToCall = toolsToCall;
            }
            VerifyPlan = verifyPlan ?? new List<string> { "compile the project." };
            TestTspFiles = testTspFiles ?? new List<string>();
            QuestionAndAnswers = questionAndAnswers;
            Repo = new RepoConfig()
            {
                Owner = "Azure",
                Name = "azure-rest-api-specs",
                Ref = "main",
                SparseCheckoutPaths = [TspProjectPath, ".vscode", "eng/common"]
            };
            Validators =
            [
                new ToolAndSkillTriggerValidator("Expected tools and skills were triggered", ToolsToCall),
                new VerifyResultWithAIValidator("Verify results with AI", string.Join("\n", VerifyPlan.ToArray()))
            ];
            if (QuestionAndAnswers != null && QuestionAndAnswers.Any())
            {
                Validators = Validators.Concat([new InteractionValidator("Verify interaction")]).ToArray();
            }
        }

        /// <inheritdoc />
        public override TimeSpan Timeout => TimeSpan.FromMinutes(5);

        /// <inheritdoc />
        /// <summary>
        /// Sets up the TypeSpec workspace environment.
        /// This includes installing npm dependencies for TypeSpec compilation and setting up the Azure SDK MCP server.
        /// </summary>
        public override async Task SetupAsync(Workspace workspace)
        {
            // set up test tsp files - copy them to the tspProjectPath, replacing if they exist
            if (TestTspFiles.Any())
            {
                Console.WriteLine($"Setting up test TypeSpec files for scenario '{Name}'...");
                // Look for TestData in the source directory, not the build output
                var baseDir = AppContext.BaseDirectory;
                var testDataPath = Path.Combine(baseDir, "TestData", "TypeSpec");

                // If not found in build output, try to find it relative to the source
                if (!Directory.Exists(testDataPath))
                {
                    // Navigate up from bin/Debug/net8.0 to project root
                    var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
                    testDataPath = Path.Combine(projectRoot, "TestData", "TypeSpec");
                }
                if (!Directory.Exists(testDataPath))
                {
                    Console.Error.WriteLine($"Warning: TestData directory not found at '{testDataPath}'");
                }
                else
                {
                    foreach (var tspFile in TestTspFiles)
                    {
                        var sourcePath = Path.Combine(testDataPath, tspFile);

                        // Exclude the top-level folder (case-name folder) from the destination path
                        // e.g., "version-add-preview-after-preview/employee.tsp" -> "employee.tsp"
                        var relativePath = tspFile.Contains(Path.DirectorySeparatorChar) || tspFile.Contains('/')
                            ? string.Join(Path.DirectorySeparatorChar, tspFile.Split(new[] { Path.DirectorySeparatorChar, '/' }, StringSplitOptions.RemoveEmptyEntries).Skip(1))
                            : tspFile;

                        var destinationPath = Path.Combine(workspace.RepoPath, TspProjectPath, relativePath);
                        await workspace.CopyToWorkspaceAsync(sourcePath, Path.Combine(TspProjectPath, relativePath));
                    }
                }

                // cache the setup changes
                Console.WriteLine($"Caching setup changes for scenario '{Name}'...");
                await workspace.RunCommandAsync("git", "add", ".");
                Console.WriteLine($"Committing setup changes for scenario '{Name}'...");
                await workspace.RunCommandAsync("git", "commit", "-m", $"Setup for scenario '{Name}'");
            }
            if (!string.IsNullOrWhiteSpace(AuthoringSkillPath))
            {
                // Copy authoring skill to workspace if AuthoringSkillPath is provided
                if (!Directory.Exists(AuthoringSkillPath))
                {
                    Console.Error.WriteLine($"Warning: Authoring skill file not found at '{AuthoringSkillPath}'");
                }
                else
                {
                    var destRelativePath = Path.Combine(".github", "skills", "azure-typespec-author");
                    await workspace.RemoveFromWorkspace(destRelativePath);
                    await workspace.CopyToWorkspaceAsync(AuthoringSkillPath, destRelativePath);
                }
            }
            // Install npm dependencies required for TypeSpec compilation
            Console.WriteLine($"Installing npm dependencies for scenario '{Name}'...");
            await workspace.RunCommandAsync("npm", "ci");

            // Download and install the Azure SDK MCP CLI tool using the common setup script
            Console.WriteLine($"Installing Azure SDK MCP CLI tool for scenario '{Name}'...");
            await workspace.RunCommandAsync("pwsh", "./eng/common/mcp/azure-sdk-mcp.ps1", "-InstallDirectory", workspace.RootPath);

            // Configure the path to the installed MCP executable for this scenario
            // Check if running on Windows
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            AzsdkMcpPath = Path.Combine(workspace.RootPath, isWindows ? "azsdk.exe" : "azsdk");


            // TODO: Download and Start Azure Knowledge Base locally. Currently we need to manually start the Azure Knowledge Base server locally.
            //await workspace.RunCommandAsync("")
        }
    }
}
