// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace GitHubClient
{
    public class RepositoryQuery<T>
    {
        public required RepositoryItems Repository { get; init; }

        public class RepositoryItems
        {
            public required T Result { get; init; }
        }
    }

    public class Author
    {
        public required string Login { get; init; }
    }

    public class Issue
    {
        public required ulong Number { get; init; }
        public required string Title { get; init; }
        public required Author Author { get; init; }
        public required string Body { get; init; }
        public required Page<Label> Labels { get; init; }

        public string[] LabelNames => this.Labels.Nodes.Select(label => label.Name).ToArray();
        public string[] CategoryLabelNames => [.. this.Labels.Nodes.Where(IsCategoryLabel).Select(label => label.Name)];    
        public string[] ServiceLabelNames => [.. this.Labels.Nodes.Where(IsServiceLabel).Select(label => label.Name)];
        private static bool IsServiceLabel(Label label) =>
            string.Equals(label.Color, "e99695", StringComparison.InvariantCultureIgnoreCase);

        private static bool IsCategoryLabel(Label label) =>
            string.Equals(label.Color, "ffeb77", StringComparison.InvariantCultureIgnoreCase);
    }

    public class Label
    {
        public required string Name { get; init; }
        public required string Color { get; init; }
    }

    public class PullRequest : Issue
    {
        public FilesContent? Files { get; init; }

        public class FilesContent : Page<FileNode>
        {
        }

        public class FileNode
        {
            public required string Path { get; init; }
        }

        private string[]? _filePaths;
        public string[] FilePaths => _filePaths
            ??= this.Files?.Nodes.Select(file => file.Path).ToArray() ?? Array.Empty<string>();

        private string[]? _fileNames;
        public string[] FileNames => _fileNames
            ??= this.FilePaths.SelectMany(filePath => new string[] { filePath, Path.GetFileName(filePath), Path.GetFileNameWithoutExtension(filePath) }).ToArray();

        private string[]? _folderNames;
        public string[] FolderNames => _folderNames
            ??= this.FilePaths.SelectMany(filePath => {
                string? dirPath = Path.GetDirectoryName(filePath);

                if (string.IsNullOrEmpty(dirPath))
                {
                    return Enumerable.Empty<string>();
                }

                string[] dirParts = dirPath.Split(Path.DirectorySeparatorChar);

                return dirParts.Select((dir, index) => string.Join(Path.DirectorySeparatorChar, dirParts.Take(index + 1)));
            }).ToArray();
    }

    public class Page<TNode>
    {
        public required TNode[] Nodes { get; init; }
        public PageInfo? PageInfo { get; init; }
        public int? TotalCount { get; init; }

        public bool HasNextPage => this.PageInfo?.HasNextPage ?? false;
        public string? EndCursor => this.PageInfo?.EndCursor;
    }

    public class PageInfo
    {
        public required bool HasNextPage { get; init; }
        public string? EndCursor { get; init; }
    }
}
