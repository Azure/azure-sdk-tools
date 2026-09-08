// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using APIView;
using Microsoft.ApplicationInsights;
using System.Text;

namespace APIViewWeb
{
    public class GoLanguageService : LanguageProcessor
    {
        public override string Name { get; } = "Go";
        public override string [] Extensions { get; } = { ".gosource" };
        public override string ProcessName { get; } = "apiviewgo";
        public override string VersionString { get; } = "0.3";

        // Guardrails to protect against malicious or malformed archives (e.g. zip bombs).
        private const long MaxCompressedArchiveBytes = 64L * 1024 * 1024;
        private const int MaxEntryCount = 100_000;
        private const long MaxEntryExpandedBytes = 512L * 1024 * 1024;
        private const long MaxTotalExpandedBytes = 2L * 1024 * 1024 * 1024;
        private const int CopyBufferSize = 81920;

        public GoLanguageService(TelemetryClient telemetryClient) : base(telemetryClient)
        {
        }

        public override string GetProcessorArguments(string originalName, string tempDirectory, string jsonFilePath)
        {
            return $"\"{originalName}\" \"{jsonFilePath}\"";
        }

        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis, string crossLanguageMetadata = null)
        {
            var tempPath = Path.GetTempPath();
            var randomSegment = Guid.NewGuid().ToString("N");
            var tempDirectory = Path.Combine(tempPath, "ApiView", randomSegment);
            Directory.CreateDirectory(tempDirectory);
            var originalFilePath = Path.Combine(tempDirectory, originalName);
            var jsonFilePath = Path.ChangeExtension(originalFilePath, ".json");

            try
            {
                using var zipStream = await CopyToBoundedMemoryStreamAsync(stream, MaxCompressedArchiveBytes);
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
                ExtractArchiveWithLimits(archive, tempDirectory);
            }
            catch
            {
                Directory.Delete(tempDirectory, true);
                throw;
            }

            var packageRootDirectory = originalFilePath.Replace(Extensions[0], "");

            var arguments = GetProcessorArguments(packageRootDirectory, tempDirectory, tempDirectory);
            return await RunParserProcess(packageRootDirectory, tempDirectory, jsonFilePath, arguments);
        }

        // Streams the compressed archive into memory while enforcing an upper bound on the
        // compressed input size so an oversized upload cannot be fully buffered.
        private static async Task<MemoryStream> CopyToBoundedMemoryStreamAsync(Stream source, long maxCompressedBytes)
        {
            var memoryStream = new MemoryStream();
            try
            {
                var buffer = new byte[CopyBufferSize];
                long totalRead = 0;
                int read;
                while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                {
                    totalRead += read;
                    if (totalRead > maxCompressedBytes)
                    {
                        throw new InvalidOperationException(
                            $"Compressed archive exceeds the maximum allowed size of {maxCompressedBytes} bytes.");
                    }
                    memoryStream.Write(buffer, 0, read);
                }
            }
            catch
            {
                memoryStream.Dispose();
                throw;
            }

            memoryStream.Position = 0;
            return memoryStream;
        }

        // Extracts the archive while enforcing entry-count, per-entry expanded-byte, and
        // aggregate expanded-byte limits. Duplicate destination paths (per the deployment
        // filesystem's comparison semantics) and path-traversal entries are rejected before
        // any entry is written to disk.
        private static void ExtractArchiveWithLimits(ZipArchive archive, string destinationDirectory)
        {
            if (archive.Entries.Count > MaxEntryCount)
            {
                throw new InvalidOperationException(
                    $"Archive exceeds the maximum allowed entry count of {MaxEntryCount}.");
            }

            var destinationRoot = Path.GetFullPath(destinationDirectory);
            if (!destinationRoot.EndsWith(Path.DirectorySeparatorChar))
            {
                destinationRoot += Path.DirectorySeparatorChar;
            }

            // Validate every entry (path traversal + duplicate destinations) before writing anything.
            var seenPaths = new HashSet<string>(GetDeploymentPathComparer());
            foreach (var entry in archive.Entries)
            {
                var destinationPath = ResolveEntryPath(destinationRoot, entry.FullName);

                // Directory entries have an empty Name; they don't compete for a destination file path.
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                if (!seenPaths.Add(destinationPath))
                {
                    throw new InvalidOperationException(
                        $"Archive contains a duplicate destination path for entry '{entry.FullName}'.");
                }
            }

            long totalExpandedBytes = 0;
            foreach (var entry in archive.Entries)
            {
                var destinationPath = ResolveEntryPath(destinationRoot, entry.FullName);

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                var parentDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(parentDirectory))
                {
                    Directory.CreateDirectory(parentDirectory);
                }

                using var entryStream = entry.Open();
                using var outputStream = new FileStream(
                    destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

                var buffer = new byte[CopyBufferSize];
                long entryExpandedBytes = 0;
                int read;
                while ((read = entryStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    entryExpandedBytes += read;
                    if (entryExpandedBytes > MaxEntryExpandedBytes)
                    {
                        throw new InvalidOperationException(
                            $"Archive entry '{entry.FullName}' exceeds the maximum allowed expanded size of {MaxEntryExpandedBytes} bytes.");
                    }

                    totalExpandedBytes += read;
                    if (totalExpandedBytes > MaxTotalExpandedBytes)
                    {
                        throw new InvalidOperationException(
                            $"Archive exceeds the maximum allowed aggregate expanded size of {MaxTotalExpandedBytes} bytes.");
                    }

                    outputStream.Write(buffer, 0, read);
                }
            }
        }

        // Resolves an archive entry to a full path rooted at the destination directory,
        // rejecting entries that would escape the destination (zip-slip).
        private static string ResolveEntryPath(string destinationRoot, string entryFullName)
        {
            var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, entryFullName));
            if (!destinationPath.StartsWith(destinationRoot, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Archive entry '{entryFullName}' resolves to a path outside the destination directory.");
            }

            return destinationPath;
        }

        // Windows and macOS filesystems are case-insensitive; Linux is case-sensitive.
        // Match the deployment host's semantics when detecting duplicate destination paths.
        private static StringComparer GetDeploymentPathComparer()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? StringComparer.Ordinal
                : StringComparer.OrdinalIgnoreCase;
        }
    }
}
