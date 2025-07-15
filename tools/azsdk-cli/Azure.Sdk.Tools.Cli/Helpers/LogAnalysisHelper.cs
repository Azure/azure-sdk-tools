using System.Security.Policy;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Helpers;

public interface ILogAnalysisHelper
{
    Task<List<LogEntry>> AnalyzeLogContent(string filePath, List<string>? keywords, int? beforeLines, int? afterLines);
}

public class Keyword
{
    public string Value { get; }
    public Func<string, bool> MatchFunc { get; }

    public Keyword(string keyword)
    {
        Value = keyword;
        MatchFunc = input => input.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    public Keyword(string keyword, Func<string, bool> matchFunc)
    {
        this.Value = keyword;
        this.MatchFunc = matchFunc;
    }

    public bool Matches(string input) => MatchFunc(input);

    public override string ToString()
    {
        return Value;
    }

    public static implicit operator Keyword(string keyword) => new(keyword);
    public static implicit operator string(Keyword keyword) => keyword.Value;
}

public class LogAnalysisHelper(ILogger<LogAnalysisHelper> logger) : ILogAnalysisHelper
{
    private readonly ILogger<LogAnalysisHelper> logger = logger;

    public const int DEFAULT_BEFORE_LINES = 20;
    public const int DEFAULT_AFTER_LINES = 20;

    // Built-in error keywords for robust error detection
    private static readonly HashSet<Keyword> defaultErrorKeywords =
    [
        // ANSI color codes (red, etc.)
        "[31m",

        // custom keyword comparers
        new("error", (i) => {
            var falsePositives = new[] { "no error", "0 error", "any errors", "`error`", "error.type" };
            var hasFalsePositives = falsePositives.Any(fp => i.Contains(fp, StringComparison.OrdinalIgnoreCase));
            return hasFalsePositives ? false : i.Contains("error", StringComparison.OrdinalIgnoreCase);
        }),

        new("fail", (i) => {
            var falsePositives = new[] { "no fail", "0 fail", "any fail" };
            var hasFalsePositives = falsePositives.Any(fp => i.Contains(fp, StringComparison.OrdinalIgnoreCase));
            return hasFalsePositives ? false : i.Contains("fail", StringComparison.OrdinalIgnoreCase);
        }),

        // Common error indicators
        "exception", "aborted", "fatal", "critical", "panic", "crash", "crashed", "segfault", "stacktrace",
        // Network/connection errors
        "timeout", "timed out", "unreachable", "refused",
        // Permission/access errors
        "access denied", "permission denied", "unauthorized", "forbidden", "token expired",
        // File/IO errors
        "file not found", "directory not found", "no such file", "permission denied", "disk full", "out of space",
        // Memory/resource errors
        "out of memory", "memory leak", "resource exhausted", "quota exceeded", "too many", "limit exceeded", "overflow", "underflow",
        // Process/service errors
        "service unavailable", "service down", "process died", "killed", "terminated", "non-zero exit",
        // HTTP/API errors
        "bad request"
    ];

    public async Task<List<LogEntry>> AnalyzeLogContent(string filePath, List<string>? keywordOverrides, int? beforeLines, int? afterLines)
    {
        using var stream = new StreamReader(filePath);
        return await AnalyzeLogContent(stream, keywordOverrides, beforeLines, afterLines, filePath: filePath);
    }

    public async Task<List<LogEntry>> AnalyzeLogContent(StreamReader reader, List<string>? keywordOverrides, int? beforeLines, int? afterLines, string url = "", string filePath = "")
    {
        var keywords = defaultErrorKeywords;
        if (keywordOverrides?.Count > 0)
        {
            keywords = [];
            foreach (var keyword in keywordOverrides)
            {
                keywords.Add(keyword);
            }
        }

        beforeLines ??= DEFAULT_BEFORE_LINES;
        afterLines ??= DEFAULT_AFTER_LINES;
        var before = new Queue<string>((int)beforeLines);
        var after = new Queue<string>((int)afterLines);
        var maxAfterLines = afterLines ?? 100;

        var errors = new List<LogEntry>();

        var lineNumber = 0;
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            lineNumber++;
            // check > not >= because an error match will take up an extra slot
            if (before.Count > beforeLines)
            {
                before.Dequeue();
            }
            before.Enqueue(line);
            var matchedKeywords = keywords.Where(k => k.Matches(line)).ToList();

            if (matchedKeywords.Count > 0)
            {
                logger.LogDebug("Found error matches at line {lineNumber}: {keywords}. Line: {line}", lineNumber, string.Join(", ", matchedKeywords), line);
                while (after.Count < afterLines && (line = await reader.ReadLineAsync()) != null)
                {
                    lineNumber++;
                    matchedKeywords = keywords.Where(k => k.Matches(line)).ToList();
                    // Keep seeking if we find new errors while collecting the trailing error context
                    if (matchedKeywords.Count > 0 && afterLines < maxAfterLines)
                    {
                        logger.LogDebug("Found contiguous error matches at line {lineNumber}: {keywords}", lineNumber, string.Join(", ", matchedKeywords));
                        afterLines++;
                    }
                    after.Enqueue(line);
                }

                var fullContext = before.Concat(after).ToList();
                before.Clear();
                after.Clear();
                var entry = new LogEntry
                {
                    Line = lineNumber,
                    Message = string.Join(Environment.NewLine, fullContext)
                };
                if (!string.IsNullOrEmpty(url))
                {
                    entry.Url = url;
                }
                if (!string.IsNullOrEmpty(filePath))
                {
                    entry.File = filePath;
                }
                errors.Add(entry);
            }
        }

        logger.LogDebug("Found {errorCount} non-contiguous errors in {filePath}", errors.Count, filePath);
        return errors;
    }
}