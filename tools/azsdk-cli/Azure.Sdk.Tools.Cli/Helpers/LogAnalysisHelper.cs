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

    // Built-in error keywords for robust error detection
    private static readonly HashSet<Keyword> defaultErrorKeywords =
    [
        // ANSI color codes (red, etc.)
        "[31m",

        // custom keyword comparers
        new("error", (i) => {
            var falsePositives = new[] { "no error", "0 error", "any errors", "`error`", "error.type" };
            if (falsePositives.Any(fp => i.Contains(fp, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
            return i.Contains("error", StringComparison.OrdinalIgnoreCase);
        }),

        // Common error indicators
        "fail", "failure", "failed", "exception", "aborted",
        "fatal", "critical", "panic", "crash", "crashed", "segfault", "stacktrace",
        // Network/connection errors
        "timeout", "timed out", "unreachable", "refused",
        // Permission/access errors
        "access denied", "permission denied", "unauthorized", "forbidden", "token expired",
        // File/IO errors
        "file not found", "directory not found", "no such file", "permission denied", "disk full", "out of space",
        // Memory/resource errors
        "out of memory", "memory leak", "resource exhausted", "quota exceeded",
        "too many", "limit exceeded", "overflow", "underflow",
        // Process/service errors
        "service unavailable", "service down", "process died", "killed", "terminated", "non-zero exit",
        // HTTP/API errors
        "bad request", "service unavailable"
    ];

    public async Task<List<LogEntry>> AnalyzeLogContent(string filePath, List<string>? keywordOverrides, int? beforeLines, int? afterLines)
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

        var before = new Queue<string>(beforeLines ?? 3);
        var after = new Queue<string>(afterLines ?? 20);

        var errors = new List<LogEntry>();
        using var reader = new StreamReader(filePath);

        var lineNumber = 0;
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            lineNumber++;
            if (before.Count == beforeLines)
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
                    if (matchedKeywords.Count > 0)
                    {
                        logger.LogDebug("Found contiguous error matches at line {lineNumber}: {keywords}", lineNumber, string.Join(", ", matchedKeywords));
                        afterLines++;
                    }
                    after.Enqueue(line);
                }

                var fullContext = before.Concat(after).ToList();
                errors.Add(new LogEntry
                {
                    File = filePath,
                    Line = lineNumber,
                    Message = string.Join(Environment.NewLine, fullContext)
                });
            }
        }

        logger.LogDebug("Found {errorCount} non-contiguous errors in {filePath}", errors.Count, filePath);
        return errors;
    }
}