using System.Security.Cryptography;
using System.Text;
using Azure.Tools.GeneratorAgent.Models;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Manages TypeSpec file versions and metadata for the tools-based agent approach.
    /// Uses synchronized versioning where all files share the same version number.
    /// Includes file history for rollback capabilities.
    /// </summary>
    internal class TypeSpecFileVersionManager
    {
        private readonly ILogger<TypeSpecFileVersionManager> Logger;
        private readonly Dictionary<string, TypeSpecFileInfo> FileMetadata = new();
        private readonly Dictionary<string, List<FileHistoryEntry>> FileHistory = new();
        private int _currentVersion = 1;
        private const int MaxHistoryPerFile = 20; // Keep last 20 versions per file

        public TypeSpecFileVersionManager(ILogger<TypeSpecFileVersionManager> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            Logger = logger;
        }

        /// <summary>
        /// Gets or creates file metadata without updating versions (for read-only operations)
        /// </summary>
        public TypeSpecFileInfo GetOrCreateFileMetadata(string fileName, string content)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            ArgumentNullException.ThrowIfNull(content);

            var sha256 = ComputeSha256Hash(content);
            var lines = content.Split('\n').Length;

            if (FileMetadata.TryGetValue(fileName, out var existing))
            {
                // File already tracked, just return existing metadata
                Logger.LogDebug("Retrieved existing metadata for {FileName} (hash: {Hash})", 
                    fileName, existing.Sha256[..8]);
                return existing;
            }
            else
            {
                // New file - create metadata without incrementing version
                var newFileInfo = new TypeSpecFileInfo
                {
                    Path = fileName,
                    Lines = lines,
                    Version = _currentVersion,
                    Sha256 = sha256
                };
                FileMetadata[fileName] = newFileInfo;
                Logger.LogDebug("Created new metadata for {FileName} with current version {Version} (hash: {Hash})", 
                    fileName, _currentVersion, sha256[..8]);
                return newFileInfo;
            }
        }

        /// <summary>
        /// Updates file metadata and increments all file versions if any content changed
        /// </summary>
        public TypeSpecFileInfo UpdateFileMetadata(string fileName, string content)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            ArgumentNullException.ThrowIfNull(content);

            var sha256 = ComputeSha256Hash(content);
            var lines = content.Split('\n').Length;
            bool contentChanged = false;

            if (FileMetadata.TryGetValue(fileName, out var existing))
            {
                if (existing.Sha256 != sha256)
                {
                    contentChanged = true;
                    existing.Sha256 = sha256;
                    existing.Lines = lines;
                    Logger.LogDebug("Content changed for {FileName} (hash: {OldHash} → {NewHash})", 
                        fileName, existing.Sha256[..8], sha256[..8]);
                }
                else
                {
                    Logger.LogDebug("No content changes detected for {FileName} (hash: {Hash})", 
                        fileName, sha256[..8]);
                }
            }
            else
            {
                // New file with new content - treat as content change
                contentChanged = true;
                existing = new TypeSpecFileInfo
                {
                    Path = fileName,
                    Lines = lines,
                    Version = _currentVersion,
                    Sha256 = sha256
                };
                FileMetadata[fileName] = existing;
                Logger.LogDebug("Added new file {FileName} (hash: {Hash})", fileName, sha256[..8]);
            }

            // If any content changed, increment version for ALL files (synchronized versioning)
            if (contentChanged)
            {
                _currentVersion++;
                foreach (var fileInfo in FileMetadata.Values)
                {
                    fileInfo.Version = _currentVersion;
                }
                Logger.LogDebug("Incremented ALL file versions to {Version} due to content change in {FileName}", 
                    _currentVersion, fileName);
            }

            return existing;
        }

        /// <summary>
        /// Gets metadata for all tracked files
        /// </summary>
        public List<TypeSpecFileInfo> GetAllFileMetadata()
        {
            return FileMetadata.Values.ToList();
        }

        /// <summary>
        /// Gets metadata for a specific file
        /// </summary>
        public TypeSpecFileInfo? GetFileMetadata(string fileName)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            return FileMetadata.TryGetValue(fileName, out var metadata) ? metadata : null;
        }

        private static string ComputeSha256Hash(string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// Saves a file version to history before making changes
        /// </summary>
        public void SaveToHistory(string fileName, string content, string reason)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            ArgumentNullException.ThrowIfNull(content);
            ArgumentNullException.ThrowIfNull(reason);

            if (!FileHistory.ContainsKey(fileName))
            {
                FileHistory[fileName] = new List<FileHistoryEntry>();
            }

            var history = FileHistory[fileName];
            var existingMetadata = GetFileMetadata(fileName);
            var newHash = ComputeSha256Hash(content);
            
            // Check for duplicate content to avoid storing identical versions
            var existingEntry = history.FirstOrDefault(h => h.Sha256 == newHash);
            if (existingEntry != null)
            {
                Logger.LogDebug("Content identical to version {Version} (hash: {Hash}), skipping duplicate", 
                    existingEntry.Version, newHash[..8]);
                return;
            }
            
            var entry = new FileHistoryEntry
            {
                Version = existingMetadata?.Version ?? _currentVersion,
                Content = content,
                Sha256 = newHash
            };

            history.Add(entry);

            // Keep only the most recent versions
            if (history.Count > MaxHistoryPerFile)
            {
                history.RemoveAt(0);
            }

            Logger.LogDebug("Saved {FileName} to history at version {Version} (hash: {Hash}): {Reason}", 
                fileName, entry.Version, newHash[..8], reason);
        }

        /// <summary>
        /// Gets a specific version from file history
        /// </summary>
        public FileHistoryEntry? GetHistoryVersion(string fileName, int version)
        {
            ArgumentNullException.ThrowIfNull(fileName);

            if (!FileHistory.TryGetValue(fileName, out var history))
                return null;

            return history.FirstOrDefault(h => h.Version == version);
        }

        /// <summary>
        /// Gets complete file history for a specific file
        /// </summary>
        public List<FileHistoryEntry> GetFileHistory(string fileName)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            return FileHistory.TryGetValue(fileName, out var history) ? history.ToList() : new List<FileHistoryEntry>();
        }

        /// <summary>
        /// Gets available version numbers for a file
        /// </summary>
        public List<int> GetAvailableVersions(string fileName)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            
            if (!FileHistory.TryGetValue(fileName, out var history))
                return new List<int>();

            return history.Select(h => h.Version).OrderByDescending(v => v).ToList();
        }

        /// <summary>
        /// Gets the current version number
        /// </summary>
        public int GetCurrentVersion()
        {
            return _currentVersion;
        }

        /// <summary>
        /// Logs the version history for a file with SHA256 hashes for debugging
        /// </summary>
        public void LogFileHistory(string fileName)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            
            if (!FileHistory.TryGetValue(fileName, out var history) || history.Count == 0)
            {
                Logger.LogInformation("No history available for {FileName}", fileName);
                return;
            }

            Logger.LogInformation("Version history for {FileName}:", fileName);
            foreach (var entry in history.OrderByDescending(h => h.Version))
            {
                Logger.LogInformation("  v{Version}: {Hash} ({Lines} lines)", 
                    entry.Version, entry.Sha256[..8], entry.Content.Split('\n').Length);
            }
        }
    }
}
