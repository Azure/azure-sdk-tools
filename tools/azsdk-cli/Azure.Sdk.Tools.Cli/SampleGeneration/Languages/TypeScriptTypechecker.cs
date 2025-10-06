// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

// TODO: how do we better organize these? Ideally a language folder with concrete implementations of each cross-language interface
namespace Azure.Sdk.Tools.Cli.Services.Languages
{
	/// <summary>
	/// Container information for tracking persistent containers.
	/// </summary>
	/// <param name="Name">Container name</param>
	/// <param name="Id">Container ID</param>
	public record ContainerInfo(string Name, string Id);

	/// <summary>
	/// TypeScript/JavaScript type checker using Node.js and TypeScript compiler.
	/// </summary>
	public class TypeScriptTypechecker : ILanguageTypechecker, IAsyncDisposable
	{
		private readonly IDockerService dockerService;
		private readonly ILogger logger;
		private ContainerInfo? persistentContainer; // persistent container reused across typechecks

		private const string DockerImage = "node:alpine";
		private const string DEFAULT_TEMP_FILENAME = "temp.ts";

		// Regex patterns for extracting imports from TypeScript/JavaScript code
		// Note: These patterns may match imports in comments or strings, but this is acceptable
		// for AI-generated samples. False positives (extra packages) are benign; false negatives
		// would cause verification to fail, which we want. For stricter parsing, consider using
		// a TypeScript AST parser like @babel/parser.
		private static readonly Regex ImportRegex = new(@"import\s+(?:[^'""]*\s+from\s+)?['""]([^'""]+)['""]", RegexOptions.Compiled);
		private static readonly Regex RequireRegex = new(@"require\(\s*['""]([^'""]+)['""]\s*\)", RegexOptions.Compiled);
		private static readonly Regex DynamicImportRegex = new(@"import\(\s*['""]([^'""]+)['""]\s*\)", RegexOptions.Compiled);

		public TypeScriptTypechecker(IDockerService dockerService, ILogger logger)
		{
			this.dockerService = dockerService ?? throw new ArgumentNullException(nameof(dockerService));
			this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		public async Task<TypeCheckResult> TypecheckAsync(TypeCheckRequest parameters, CancellationToken ct)
		{
			await EnsureContainerAsync(ct);

			var projectDir = $"/tmp/{Guid.NewGuid():N}";
			var filePath = $"{projectDir}/{DEFAULT_TEMP_FILENAME}";

			logger.LogDebug("Starting TypeScript typecheck in project directory: {projectDir}", projectDir);

			try
			{
				// Write the TypeScript code
				await WriteFileToContainerAsync(filePath, parameters.Code, ct);

				// Create package.json with dependencies
				var packageJson = await CreatePackageJsonAsync(parameters.Code, parameters.PackageName);
				await WriteFileToContainerAsync($"{projectDir}/package.json", packageJson, ct);

				// Install npm dependencies
				var installResult = await dockerService.RunCommandInContainerAsync(
						persistentContainer!.Name,
						["npm", "install"],
						workingDirectory: projectDir,
						ct: ct);

				logger.LogDebug("npm install completed with exit code: {exitCode}", installResult.ExitCode);

				// Install client distribution if provided
				if (!string.IsNullOrEmpty(parameters.ClientDist))
				{
					await InstallClientDistributionAsync(projectDir, parameters.ClientDist, ct);
				}

				// Run TypeScript compiler
				var tscResult = await dockerService.RunCommandInContainerAsync(
						persistentContainer.Name,
						["npm", "run", "typecheck"],
						workingDirectory: projectDir,
						ct: ct);

				logger.LogDebug("TypeScript compilation completed with exit code: {exitCode}", tscResult.ExitCode);

				var succeeded = installResult.ExitCode == 0 && tscResult.ExitCode == 0;
				var output = CombineOutput(installResult, tscResult);

				return new TypeCheckResult(succeeded, output);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error during TypeScript typecheck");
				return new TypeCheckResult(false, $"Error during typecheck: {ex.Message}");
			}
			finally
			{
				// Clean up project directory
				await CleanupProjectDirectoryAsync(projectDir, ct);
			}
		}

		/// <summary>
		/// Extracts package names from TypeScript/JavaScript import statements.
		/// </summary>
		/// <param name="code">The source code to analyze</param>
		/// <param name="excludedPackages">Packages to exclude from the result</param>
		/// <returns>List of package names to install</returns>
		public static List<string> ParseImportedPackages(string code, HashSet<string> excludedPackages)
		{
			var packages = new HashSet<string> { "typescript", "@types/node" };

			// Extract packages from various import patterns
			foreach (var regex in new[] { ImportRegex, RequireRegex, DynamicImportRegex })
			{
				foreach (Match match in regex.Matches(code))
				{
					var importPath = match.Groups[1].Value;

					// Skip relative imports
					if (importPath.StartsWith(".") || importPath.StartsWith("/"))
					{
						continue;
					}

					var packageName = GetPackageRoot(importPath);
					if (!excludedPackages.Contains(packageName))
					{
						packages.Add(packageName);
					}
				}
			}

			return packages.ToList();
		}

		/// <summary>
		/// Extracts the root package name from an import path.
		/// Examples: '@scope/pkg/sub/path' → '@scope/pkg', 'pkg/sub/path' → 'pkg', 'fs' → 'fs'
		/// </summary>
		private static string GetPackageRoot(string importPath)
		{
			if (importPath.StartsWith("@"))
			{
				var parts = importPath.Split('/', 3);
				return parts.Length >= 2 ? $"{parts[0]}/{parts[1]}" : importPath;
			}
			return importPath.Split('/')[0];
		}

		private async Task EnsureContainerAsync(CancellationToken ct)
		{
			if (persistentContainer != null)
			{
				// Check if container is still running
				var isRunning = await dockerService.IsContainerRunningAsync(persistentContainer.Name, ct);
				if (isRunning)
				{
					return;
				}

				// Container exists but is stopped, try to start it
				logger.LogInformation("Restarting stopped TypeScript verification container: {containerName}", persistentContainer.Name);
				var restartResult = await dockerService.StartContainerAsync(persistentContainer.Name, ct);
				if (restartResult.ExitCode == 0)
				{
					return;
				}

				// Failed to start, remove it and create a new one
				logger.LogWarning("Failed to restart container, removing and creating a new one");
				await dockerService.RemoveContainerAsync(persistentContainer.Name, force: true, ct);
				persistentContainer = null;
			}

			logger.LogInformation("Creating TypeScript verification container");

			// Create a new container
			var containerName = $"typescript-typecheck-{Guid.NewGuid():N}";
			var createResult = await dockerService.CreateContainerAsync(
					DockerImage,
					containerName,
					ct: ct);

			if (createResult.ExitCode != 0)
			{
				throw new InvalidOperationException($"Failed to create TypeScript container: {createResult.Output}");
			}

			// Start the container
			var startResult = await dockerService.StartContainerAsync(containerName, ct);
			if (startResult.ExitCode != 0)
			{
				throw new InvalidOperationException($"Failed to start TypeScript container: {startResult.Output}");
			}

			persistentContainer = new ContainerInfo(containerName, createResult.Output.Trim());
			logger.LogInformation("TypeScript verification container created: {containerName}", containerName);
		}

		private async Task WriteFileToContainerAsync(string containerPath, string content, CancellationToken ct)
		{
			// Create a temporary file on the host and copy it to the container
			var tempFile = Path.GetTempFileName();
			try
			{
				await File.WriteAllTextAsync(tempFile, content, ct);

				// Ensure directory exists in container
				var directory = Path.GetDirectoryName(containerPath)?.Replace('\\', '/');
				if (!string.IsNullOrEmpty(directory))
				{
					await dockerService.RunCommandInContainerAsync(
							persistentContainer!.Name,
							["mkdir", "-p", directory],
							ct: ct);
				}

				var copyResult = await dockerService.CopyToContainerAsync(
						persistentContainer!.Name,
						tempFile,
						containerPath,
						ct);

				if (copyResult.ExitCode != 0)
				{
					throw new InvalidOperationException($"Failed to copy file to container: {copyResult.Output}");
				}
			}
			finally
			{
				if (File.Exists(tempFile))
				{
					File.Delete(tempFile);
				}
			}
		}

		private Task<string> CreatePackageJsonAsync(string code, string? excludePackageName)
		{
			var excludedPackages = new HashSet<string>();
			if (!string.IsNullOrEmpty(excludePackageName))
			{
				excludedPackages.Add(excludePackageName);
			}

			var dependencies = ParseImportedPackages(code, excludedPackages);
			var dependencyDict = dependencies.ToDictionary(pkg => pkg, _ => "latest");
			this.logger.LogDebug("Identified dependencies: {dependencies}", string.Join(", ", dependencies));

			var packageJson = new
			{
				name = "temp",
				version = "1.0.0",
				dependencies = dependencyDict,
				scripts = new
				{
					typecheck = $"tsc --noEmit --skipLibCheck --strict --esModuleInterop --noUnusedLocals --noUnusedParameters --noImplicitReturns {DEFAULT_TEMP_FILENAME}"
				}
			};

			var json = JsonSerializer.Serialize(packageJson, new JsonSerializerOptions
			{
				WriteIndented = true
			});

			return Task.FromResult(json);
		}

		private async Task InstallClientDistributionAsync(string projectDir, string clientDist, CancellationToken ct)
		{
			logger.LogDebug("Installing client distribution: {clientDist}", clientDist);

			// Check if the file exists on the host
			if (!File.Exists(clientDist))
			{
				logger.LogWarning("Client distribution file not found: {clientDist}", clientDist);
				return;
			}

			// Copy the distribution file to the container
			var distFileName = Path.GetFileName(clientDist);
			var containerDistPath = $"{projectDir}/{distFileName}";

			var copyResult = await dockerService.CopyToContainerAsync(
					persistentContainer!.Name,
					clientDist,
					containerDistPath,
					ct);

			if (copyResult.ExitCode != 0)
			{
				logger.LogWarning("Failed to copy client distribution to container: {output}", copyResult.Output);
				return;
			}

			// Install the distribution package
			var installResult = await dockerService.RunCommandInContainerAsync(
					persistentContainer.Name,
					["npm", "install", "--no-save", distFileName],
					workingDirectory: projectDir,
					ct: ct);

			if (installResult.ExitCode != 0)
			{
				logger.LogWarning("Failed to install client distribution: {output}", installResult.Output);
			}
			else
			{
				logger.LogDebug("Successfully installed client distribution");
			}
		}

		private static string CombineOutput(ProcessResult installResult, ProcessResult tscResult)
		{
			var parts = new List<string>();

			if (!string.IsNullOrEmpty(installResult.Output))
			{
				parts.Add($"npm install output:\n{installResult.Output}");
			}

			if (!string.IsNullOrEmpty(tscResult.Output))
			{
				parts.Add($"tsc output:\n{tscResult.Output}");
			}

			return string.Join("\n\n", parts);
		}

		private async Task CleanupProjectDirectoryAsync(string projectDir, CancellationToken ct)
		{
			try
			{
				await dockerService.RunCommandInContainerAsync(
						persistentContainer!.Name,
						["rm", "-rf", projectDir],
						ct: ct);

				logger.LogDebug("Cleaned up project directory: {projectDir}", projectDir);
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Failed to clean up project directory: {projectDir}", projectDir);
			}
		}

		/// <summary>
		/// Disposes of resources, including stopping and removing the persistent container.
		/// </summary>
		public async ValueTask DisposeAsync()
		{
			if (persistentContainer != null)
			{
				var containerName = persistentContainer.Name;
				try
				{
					logger.LogInformation("Cleaning up TypeScript verification container: {containerName}", containerName);
					await dockerService.RemoveContainerAsync(containerName, force: true, CancellationToken.None);
					persistentContainer = null;
				}
				catch (Exception ex)
				{
					logger.LogWarning(ex, "Failed to remove TypeScript verification container: {containerName}", containerName);
				}
			}
		}
	}
}