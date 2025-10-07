// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Services.Languages
{
	/// <summary>
	/// Container information for tracking persistent containers.
	/// </summary>
	/// <param name="Name">Container name</param>
	/// <param name="Id">Container ID</param>
	public record ContainerInfo(string Name, string Id);

	/// <summary>
	/// TypeScript type checker using monorepo build tooling (pnpm).
	/// Mounts the entire monorepo and uses the package's native build:samples command.
	/// </summary>
	public class TypeScriptTypechecker : ILanguageTypechecker, IAsyncDisposable
	{
		private readonly IDockerService dockerService;
		private readonly ILogger logger;
		private ContainerInfo? persistentContainer; // persistent container reused across typechecks
		private string? currentRepoRoot; // Track which repo root is mounted
		private const string DockerImage = "node:20"; // Use full Node.js image with pnpm support
		private const string RepoMountPoint = "/repo";

		public TypeScriptTypechecker(IDockerService dockerService, ILogger logger)
		{
			this.dockerService = dockerService ?? throw new ArgumentNullException(nameof(dockerService));
			this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		public async Task<TypeCheckResult> TypecheckAsync(TypeCheckRequest parameters, CancellationToken ct)
		{
			await EnsureContainerAsync(parameters.RepoRoot, ct);
			await InstallDependenciesAsync(ct);

			// Get the relative package path within the monorepo
			var relativePackagePath = GetRelativePath(parameters.RepoRoot, parameters.PackagePath);
			await BuildPackageAsync(relativePackagePath, ct);

			var samplesDir = Path.Combine(relativePackagePath, "samples-dev");
			var sampleFilePath = Path.Combine(samplesDir, parameters.FileName);

			logger.LogDebug("Starting TypeScript typecheck for sample: {fileName}", parameters.FileName);
			logger.LogDebug("Package path (relative): {packagePath}", relativePackagePath);
			logger.LogDebug("Sample file path: {filePath}", sampleFilePath);

			try
			{
				// Write the sample file to the container's mounted repo
				await WriteSampleToContainerAsync(sampleFilePath, parameters.Code, ct);

				// Run the package's build:samples command
				var buildResult = await dockerService.RunCommandInContainerAsync(
						persistentContainer!.Name,
						["pnpm", "run", "build:samples"],
						workingDirectory: Path.Combine(RepoMountPoint, relativePackagePath).Replace('\\', '/'),
						ct: ct);

				logger.LogDebug("build:samples completed with exit code: {exitCode}", buildResult.ExitCode);

				var succeeded = buildResult.ExitCode == 0;
				return new TypeCheckResult(succeeded, buildResult.Output);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error during TypeScript typecheck");
				return new TypeCheckResult(false, $"Error during typecheck: {ex.Message}");
			}
		}

		/// <summary>
		/// Gets a relative path from the repo root to the package path.
		/// </summary>
		private static string GetRelativePath(string repoRoot, string packagePath)
		{
			var repoRootUri = new Uri(Path.GetFullPath(repoRoot) + Path.DirectorySeparatorChar);
			var packagePathUri = new Uri(Path.GetFullPath(packagePath));
			var relativeUri = repoRootUri.MakeRelativeUri(packagePathUri);
			return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
		}

		private async Task EnsureContainerAsync(string repoRoot, CancellationToken ct)
		{
			// If container exists but is for a different repo, clean it up
			if (persistentContainer != null && currentRepoRoot != repoRoot)
			{
				logger.LogInformation("Repository root changed, recreating container");
				await dockerService.RemoveContainerAsync(persistentContainer.Name, force: true, ct);
				persistentContainer = null;
				currentRepoRoot = null;
			}

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

			logger.LogInformation("Creating TypeScript verification container with monorepo mount");

			// Create volume mounts for the entire repository
			var volumeMounts = new Dictionary<string, string>
			{
				{ Path.GetFullPath(repoRoot), RepoMountPoint }
			};

			// Set CI environment variable to allow pnpm to work in non-interactive mode
			var environmentVars = new Dictionary<string, string>
			{
				{ "CI", "true" }
			};

			// Create a new container with the repo mounted
			var containerName = $"typescript-typecheck-{Guid.NewGuid():N}";
			var createResult = await dockerService.CreateContainerAsync(
					DockerImage,
					containerName,
					environmentVars: environmentVars,
					volumeMounts: volumeMounts,
					ct: ct);

			if (createResult.ExitCode != 0)
			{
				throw new InvalidOperationException($"Failed to create TypeScript container: {createResult.Output}");
			}

			// Start the container
			var startResult = await dockerService.StartContainerAsync(containerName, ct);
			if (startResult.ExitCode != 0)
			{
				throw new InvalidOperationException($"Failed to start TypeScript container: {createResult.Output}");
			}

			persistentContainer = new ContainerInfo(containerName, createResult.Output.Trim());
			currentRepoRoot = repoRoot;
			logger.LogInformation("TypeScript verification container created: {containerName}", containerName);

			// Install pnpm globally in the container
			logger.LogInformation("Installing pnpm in container");
			var pnpmInstallResult = await dockerService.RunCommandInContainerAsync(
					persistentContainer.Name,
					["npm", "install", "-g", "pnpm"],
					ct: ct);

			if (pnpmInstallResult.ExitCode != 0)
			{
				logger.LogWarning("Failed to install pnpm globally: {output}", pnpmInstallResult.Output);
			}

			// Install turbo globally in the container
			logger.LogInformation("Installing turbo in container");
			var turboInstallResult = await dockerService.RunCommandInContainerAsync(
					persistentContainer.Name,
					["npm", "install", "-g", "turbo"],
					ct: ct);

			if (turboInstallResult.ExitCode != 0)
			{
				logger.LogWarning("Failed to install turbo globally: {output}", turboInstallResult.Output);
			}
		}

		/// <summary>
		/// Installs dependencies in the monorepo using pnpm install.
		/// </summary>
		private async Task InstallDependenciesAsync(CancellationToken ct)
		{
			logger.LogInformation("Installing monorepo dependencies with pnpm install");

			var installResult = await dockerService.RunCommandInContainerAsync(
					persistentContainer!.Name,
					["pnpm", "install"],
					workingDirectory: RepoMountPoint,
					ct: ct);

			if (installResult.ExitCode != 0)
			{
				logger.LogWarning("pnpm install had non-zero exit code: {exitCode}", installResult.ExitCode);
				logger.LogDebug("pnpm install output: {output}", installResult.Output);
			}
			else
			{
				logger.LogInformation("Dependencies installed successfully");
			}
		}

		/// <summary>
		/// Builds a specific package and its dependencies using turbo.
		/// </summary>
		private async Task BuildPackageAsync(string relativePackagePath, CancellationToken ct)
		{
			logger.LogInformation("Building package and dependencies with turbo: {packagePath}", relativePackagePath);

			var buildResult = await dockerService.RunCommandInContainerAsync(
					persistentContainer!.Name,
					["turbo", "run", "build"],
					workingDirectory: Path.Combine(RepoMountPoint, relativePackagePath).Replace('\\', '/'),
					ct: ct);

			if (buildResult.ExitCode != 0)
			{
				logger.LogWarning("Package build had non-zero exit code: {exitCode}", buildResult.ExitCode);
				logger.LogDebug("Package build output: {output}", buildResult.Output);
			}
			else
			{
				logger.LogInformation("Package and dependencies built successfully with turbo");
			}

		}

		private async Task WriteSampleToContainerAsync(string containerPath, string content, CancellationToken ct)
		{
			// The containerPath is relative to the repo root, so prepend the mount point
			var fullContainerPath = Path.Combine(RepoMountPoint, containerPath).Replace('\\', '/');

			// Create a temporary file on the host
			var tempFile = Path.GetTempFileName();
			try
			{
				await File.WriteAllTextAsync(tempFile, content, ct);

				// Ensure directory exists in container
				var directory = Path.GetDirectoryName(fullContainerPath)?.Replace('\\', '/');
				if (!string.IsNullOrEmpty(directory))
				{
					await dockerService.RunCommandInContainerAsync(
							persistentContainer!.Name,
							["mkdir", "-p", directory],
							ct: ct);
				}

				// Copy the file to the container
				var copyResult = await dockerService.CopyToContainerAsync(
						persistentContainer!.Name,
						tempFile,
						fullContainerPath,
						ct);

				if (copyResult.ExitCode != 0)
				{
					throw new InvalidOperationException($"Failed to copy file to container: {copyResult.Output}");
				}

				logger.LogDebug("Wrote sample file to container: {path}", fullContainerPath);
			}
			finally
			{
				if (File.Exists(tempFile))
				{
					File.Delete(tempFile);
				}
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
					currentRepoRoot = null;
				}
				catch (Exception ex)
				{
					logger.LogWarning(ex, "Failed to remove TypeScript verification container: {containerName}", containerName);
				}
			}
		}
	}
}