using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace perf_semantic_kernel
{
    public class RepoAccessPlugin
    {
        private readonly string _repoRoot;

        public RepoAccessPlugin(string repoRoot)
        {
            _repoRoot = repoRoot;
        }

        [KernelFunction("list_files")]
        [Description("Lists all files in the repository.")]
        public List<string> ListFilesAsync()
        {
            return Directory.GetFiles(_repoRoot, "*.*", SearchOption.AllDirectories).ToList();
        }

        [KernelFunction("read_file")]
        [Description("Reads the content of a file in the repository.")]
        public async Task<string?> ReadFileAsync(string relativePath)
        {
            var fullPath = Path.Combine(_repoRoot, relativePath);
            if (!File.Exists(fullPath)) return null;
            return await File.ReadAllTextAsync(fullPath);
        }

        [KernelFunction("search_in_files")]
        [Description("Searches for a pattern in all files and returns matching lines with file names.")]
        public async Task<List<SearchResult>> SearchInFilesAsync(string pattern)
        {
            var results = new List<SearchResult>();
            var files = Directory.GetFiles(_repoRoot, "*.*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var lines = await File.ReadAllLinesAsync(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new SearchResult
                        {
                            File = Path.GetRelativePath(_repoRoot, file),
                            LineNumber = i + 1,
                            Line = lines[i]
                        });
                    }
                }
            }
            return results;
        }
    }

    public class SearchResult
    {
        [JsonPropertyName("file")]
        public string File { get; set; } = "";

        [JsonPropertyName("line_number")]
        public int LineNumber { get; set; }

        [JsonPropertyName("line")]
        public string Line { get; set; } = "";
    }
}
