using System;
using System.IO;
using PipelineGenerator.CommandParserOptions;

namespace PipelineGenerator
{
    public static class PreparedSdkDirectory
    {
        public static void Validate(PreparePostMergeOptions options, DescriptorRequest[] descriptors)
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.SdkDirectory));
            if (!Directory.Exists(root))
            {
                throw new InvalidOperationException("The SDK directory must already be prepared by the trusted pipeline checkout step.");
            }
            RejectLink(root);
            foreach (var descriptor in descriptors)
            {
                var path = GetContainedPath(root, descriptor.Path);
                for (var directory = Directory.GetParent(path); directory != null && directory.FullName != root; directory = directory.Parent)
                {
                    RejectLink(directory.FullName);
                }
                RejectLink(path);
                if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.Directory) != 0)
                {
                    throw new InvalidOperationException($"Prepared descriptor '{descriptor.Path}' is missing or is not a regular file.");
                }
                if (descriptor.PreviousPath != null)
                {
                    var previousPath = GetContainedPath(root, descriptor.PreviousPath);
                    for (var directory = Directory.GetParent(previousPath); directory != null && directory.FullName != root; directory = directory.Parent)
                    {
                        if (Directory.Exists(directory.FullName))
                        {
                            RejectLink(directory.FullName);
                        }
                    }
                    if (Path.Exists(previousPath) || new FileInfo(previousPath).LinkTarget != null)
                    {
                        throw new InvalidOperationException($"Previous descriptor '{descriptor.PreviousPath}' still exists in the prepared SDK directory.");
                    }
                }
            }
            // Generation scans recurse. Reject linked entries in every selected scan scope before it can follow them.
            foreach (var directory in PostMergeRequest.GetDirectories(descriptors))
            {
                ValidateTree(GetContainedPath(root, directory));
            }
        }

        private static string GetContainedPath(string root, string relative)
        {
            var path = Path.GetFullPath(Path.Combine(root, relative));
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, comparison))
            {
                throw new InvalidOperationException($"Descriptor '{relative}' escapes the prepared SDK directory.");
            }
            return path;
        }

        private static void RejectLink(string path)
        {
            if (!Path.Exists(path) && new FileInfo(path).LinkTarget == null)
            {
                throw new InvalidOperationException($"Prepared SDK path is missing: '{path}'.");
            }
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException($"Prepared SDK paths must not contain symbolic links or reparse points: '{path}'.");
            }
        }

        private static void ValidateTree(string directory)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                RejectLink(entry);
                if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0)
                {
                    ValidateTree(entry);
                }
            }
        }
    }
}
