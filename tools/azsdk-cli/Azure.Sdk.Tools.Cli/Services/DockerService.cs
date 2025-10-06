// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Azure.Sdk.Tools.Cli.Services
{
    /// <summary>
    /// Service for managing Docker containers and operations.
    /// </summary>
    public interface IDockerService
    {
        /// <summary>
        /// Checks if Docker is available and running on the system.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if Docker is available, false otherwise.</returns>
        Task<bool> IsDockerAvailableAsync(CancellationToken ct);

        /// <summary>
        /// Pulls a Docker image from the registry.
        /// </summary>
        /// <param name="image">The Docker image to pull (e.g., "ubuntu:latest").</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Process result of the pull operation.</returns>
        Task<ProcessResult> PullImageAsync(string image, CancellationToken ct);

        /// <summary>
        /// Creates a new Docker container.
        /// </summary>
        /// <param name="image">The Docker image to use for the container.</param>
        /// <param name="containerName">Optional name for the container. If null, Docker will auto-generate one.</param>
        /// <param name="environmentVars">Optional environment variables to set in the container.</param>
        /// <param name="workingDirectory">Optional working directory to set in the container.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Process result containing the container creation output.</returns>
        Task<ProcessResult> CreateContainerAsync(string image, string? containerName = null, IDictionary<string, string>? environmentVars = null, string? workingDirectory = null, CancellationToken ct = default);

        /// <summary>
        /// Checks if a container is currently running.
        /// </summary>
        /// <param name="containerName">The name or ID of the container to check.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the container is running, false otherwise.</returns>
        Task<bool> IsContainerRunningAsync(string containerName, CancellationToken ct);

        /// <summary>
        /// Runs a command inside a Docker container.
        /// </summary>
        /// <param name="containerName">The name or ID of the container.</param>
        /// <param name="command">The command and arguments to run.</param>
        /// <param name="workingDirectory">Optional working directory to run the command in.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Process result of the command execution.</returns>
        Task<ProcessResult> RunCommandInContainerAsync(string containerName, string[] command, string? workingDirectory = null, CancellationToken ct = default);

        /// <summary>
        /// Copies a file or directory from the host to a container.
        /// </summary>
        /// <param name="containerName">The name or ID of the container.</param>
        /// <param name="hostPath">The path on the host system.</param>
        /// <param name="containerPath">The destination path in the container.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Process result of the copy operation.</returns>
        Task<ProcessResult> CopyToContainerAsync(string containerName, string hostPath, string containerPath, CancellationToken ct);

        /// <summary>
        /// Copies a file or directory from a container to the host.
        /// </summary>
        /// <param name="containerName">The name or ID of the container.</param>
        /// <param name="containerPath">The path in the container.</param>
        /// <param name="hostPath">The destination path on the host system.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Process result of the copy operation.</returns>
        Task<ProcessResult> CopyFromContainerAsync(string containerName, string containerPath, string hostPath, CancellationToken ct);

        /// <summary>
        /// Removes a Docker container.
        /// </summary>
        /// <param name="containerName">The name or ID of the container to remove.</param>
        /// <param name="force">If true, forcefully remove the container even if it's running.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Process result of the removal operation.</returns>
        Task<ProcessResult> RemoveContainerAsync(string containerName, bool force = false, CancellationToken ct = default);

        /// <summary>
        /// Starts a Docker container.
        /// </summary>
        /// <param name="containerName">The name or ID of the container to start.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Process result of the start operation.</returns>
        Task<ProcessResult> StartContainerAsync(string containerName, CancellationToken ct);

        /// <summary>
        /// Stops a Docker container.
        /// </summary>
        /// <param name="containerName">The name or ID of the container to stop.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Process result of the stop operation.</returns>
        Task<ProcessResult> StopContainerAsync(string containerName, CancellationToken ct);
    }

    /// <summary>
    /// Service for managing Docker containers and operations using Docker CLI.
    /// </summary>
    public class DockerService : IDockerService
    {
        private readonly IProcessHelper _processHelper;
        private readonly ILogger<DockerService> _logger;
        private static readonly string DockerCommand = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "docker.exe" : "docker";

        public DockerService(IProcessHelper processHelper, ILogger<DockerService> logger)
        {
            _processHelper = processHelper ?? throw new ArgumentNullException(nameof(processHelper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> IsDockerAvailableAsync(CancellationToken ct)
        {
            try
            {
                var options = new ProcessOptions(DockerCommand, ["--version"], logOutputStream: false);
                var result = await _processHelper.Run(options, ct);
                return result.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Docker availability check failed");
                return false;
            }
        }

        public async Task<ProcessResult> PullImageAsync(string image, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(image))
            {
                throw new ArgumentException("Image name cannot be null or empty", nameof(image));
            }

            _logger.LogInformation("Pulling Docker image: {Image}", image);
            var options = new ProcessOptions(DockerCommand, ["pull", image], timeout: TimeSpan.FromMinutes(10));
            return await _processHelper.Run(options, ct);
        }

        public async Task<ProcessResult> CreateContainerAsync(string image, string? containerName = null, IDictionary<string, string>? environmentVars = null, string? workingDirectory = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(image))
            {
                throw new ArgumentException("Image name cannot be null or empty", nameof(image));
            }

            var args = new List<string> { "create" };

            // Add container name if provided
            if (!string.IsNullOrWhiteSpace(containerName))
            {
                args.AddRange(["--name", containerName]);
            }

            // Add environment variables if provided
            if (environmentVars != null)
            {
                foreach (var kvp in environmentVars)
                {
                    args.AddRange(["-e", $"{kvp.Key}={kvp.Value}"]);
                }
            }

            // Add working directory if provided
            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                args.AddRange(["-w", workingDirectory]);
            }

            // Keep container running for exec commands
            args.AddRange(["-t", image, "tail", "-f", "/dev/null"]);

            _logger.LogInformation("Creating Docker container from image: {Image}", image);
            var options = new ProcessOptions(DockerCommand, args.ToArray());
            return await _processHelper.Run(options, ct);
        }

        public async Task<bool> IsContainerRunningAsync(string containerName, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentException("Container name cannot be null or empty", nameof(containerName));
            }

            try
            {
                var options = new ProcessOptions(DockerCommand, ["ps", "--filter", $"name={containerName}", "--format", "{{.Names}}"], logOutputStream: false);
                var result = await _processHelper.Run(options, ct);
                return result.ExitCode == 0 && result.Output.Contains(containerName);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to check if container {ContainerName} is running", containerName);
                return false;
            }
        }

        public async Task<ProcessResult> StartContainerAsync(string containerName, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentException("Container name cannot be null or empty", nameof(containerName));
            }

            _logger.LogInformation("Starting Docker container: {ContainerName}", containerName);
            var options = new ProcessOptions(DockerCommand, ["start", containerName]);
            return await _processHelper.Run(options, ct);
        }

        public async Task<ProcessResult> StopContainerAsync(string containerName, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentException("Container name cannot be null or empty", nameof(containerName));
            }

            _logger.LogInformation("Stopping Docker container: {ContainerName}", containerName);
            var options = new ProcessOptions(DockerCommand, ["stop", containerName]);
            return await _processHelper.Run(options, ct);
        }

        public async Task<ProcessResult> RunCommandInContainerAsync(string containerName, string[] command, string? workingDirectory = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentException("Container name cannot be null or empty", nameof(containerName));
            }
            if (command == null || command.Length == 0)
            {
                throw new ArgumentException("Command cannot be null or empty", nameof(command));
            }

            var args = new List<string> { "exec" };

            // Add working directory if provided
            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                args.AddRange(["-w", workingDirectory]);
            }

            args.Add(containerName);
            args.AddRange(command);

            _logger.LogInformation("Running command in container {ContainerName}: {Command}", containerName, string.Join(" ", command));
            var options = new ProcessOptions(DockerCommand, args.ToArray(), timeout: TimeSpan.FromMinutes(5));
            return await _processHelper.Run(options, ct);
        }

        public async Task<ProcessResult> CopyToContainerAsync(string containerName, string hostPath, string containerPath, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentException("Container name cannot be null or empty", nameof(containerName));
            }
            if (string.IsNullOrWhiteSpace(hostPath))
            {
                throw new ArgumentException("Host path cannot be null or empty", nameof(hostPath));
            }
            if (string.IsNullOrWhiteSpace(containerPath))
            {
                throw new ArgumentException("Container path cannot be null or empty", nameof(containerPath));
            }

            _logger.LogInformation("Copying {HostPath} to container {ContainerName}:{ContainerPath}", hostPath, containerName, containerPath);
            var options = new ProcessOptions(DockerCommand, ["cp", hostPath, $"{containerName}:{containerPath}"]);
            return await _processHelper.Run(options, ct);
        }

        public async Task<ProcessResult> CopyFromContainerAsync(string containerName, string containerPath, string hostPath, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentException("Container name cannot be null or empty", nameof(containerName));
            }
            if (string.IsNullOrWhiteSpace(containerPath))
            {
                throw new ArgumentException("Container path cannot be null or empty", nameof(containerPath));
            }
            if (string.IsNullOrWhiteSpace(hostPath))
            {
                throw new ArgumentException("Host path cannot be null or empty", nameof(hostPath));
            }

            _logger.LogInformation("Copying from container {ContainerName}:{ContainerPath} to {HostPath}", containerName, containerPath, hostPath);
            var options = new ProcessOptions(DockerCommand, ["cp", $"{containerName}:{containerPath}", hostPath]);
            return await _processHelper.Run(options, ct);
        }

        public async Task<ProcessResult> RemoveContainerAsync(string containerName, bool force = false, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentException("Container name cannot be null or empty", nameof(containerName));
            }

            var args = new List<string> { "rm" };
            if (force)
            {
                args.Add("-f");
            }
            args.Add(containerName);

            _logger.LogInformation("Removing Docker container: {ContainerName} (force: {Force})", containerName, force);
            var options = new ProcessOptions(DockerCommand, args.ToArray());
            return await _processHelper.Run(options, ct);
        }
    }
}