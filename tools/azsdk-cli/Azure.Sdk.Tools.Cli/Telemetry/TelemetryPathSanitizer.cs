// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text;

namespace Azure.Sdk.Tools.Cli.Telemetry;

// Paths in telemetry can contain usernames and other identifying information.
// We want to redact this info but keep the child segments of paths that are
// relevant for correlating per-package usage of this tool, based on known
// directory prefixes or names that correspond to our repositories.
public static class TelemetryPathSanitizer
{
    public const string Redacted = "[PATH REDACTED]";

    private static readonly string[] AllowlistedSegments =
    [
        "azure-rest-api-specs",
        "specification",
    ];

    private const string AzureSdkPrefix = "azure-sdk-";

    private static readonly ConcurrentDictionary<string, byte> KnownRoots =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte> AllowlistedSegmentSet =
        new(StringComparer.OrdinalIgnoreCase);

    static TelemetryPathSanitizer()
    {
        foreach (var segment in AllowlistedSegments)
        {
            KnownRoots.TryAdd(segment, 0);
            AllowlistedSegmentSet.TryAdd(segment, 0);
        }
    }

    public static void AddKnownRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        KnownRoots.TryAdd(root, 0);
    }

    public static void AddAllowlistedSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return;
        }

        AllowlistedSegmentSet.TryAdd(segment, 0);
        if (segment.StartsWith(AzureSdkPrefix, StringComparison.OrdinalIgnoreCase))
        {
            KnownRoots.TryAdd(segment, 0);
        }
    }

    public static string Sanitize(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        if (!MayContainPath(input))
        {
            return input;
        }

        var sb = new StringBuilder(input.Length);
        int index = 0;
        while (index < input.Length)
        {
            if (IsTokenBoundary(input, index))
            {
                int end = FindTokenEnd(input, index);
                if (end > index)
                {
                    var token = input.Substring(index, end - index);
                    var sanitized = SanitizeToken(token);
                    sb.Append(sanitized);
                    index = end;
                    continue;
                }
            }

            sb.Append(input[index]);
            index++;
        }

        return sb.ToString();
    }

    private static bool MayContainPath(string input)
    {
        return ContainsPathSeparator(input);
    }

    private static bool IsTokenBoundary(string input, int index)
    {
        return index == 0 || IsTerminator(input[index - 1]);
    }

    private static int FindTokenEnd(string input, int start)
    {
        int idx = start;
        while (idx < input.Length && !IsTerminator(input[idx]))
        {
            idx++;
        }
        return idx;
    }

    private static bool IsTerminator(char value)
    {
        return char.IsWhiteSpace(value) || value is '"' or '\'' or '<' or '>' or '(' or ')' or '[' or ']' or '{' or '}' or ',' or ';';
    }

    private static string SanitizeToken(string token)
    {
        var coreStart = 0;
        var coreEnd = token.Length;

        while (coreStart < coreEnd && IsWrapper(token[coreStart]))
        {
            coreStart++;
        }

        while (coreEnd > coreStart && IsWrapper(token[coreEnd - 1]))
        {
            coreEnd--;
        }

        var core = token.Substring(coreStart, coreEnd - coreStart);
        if (!LooksLikePath(core))
        {
            return token;
        }

        var sanitizedCore = SanitizePathCore(core);
        if (sanitizedCore == core)
        {
            return token;
        }

        return token.Substring(0, coreStart) + sanitizedCore + token.Substring(coreEnd);
    }

    private static bool IsWrapper(char value)
    {
        return value is '"' or '\'' or '<' or '>' or '(' or ')' or '[' or ']' or '{' or '}' or ',' or '.';
    }

    private static bool LooksLikePath(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        if (token.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        if (ContainsPathSeparator(token))
        {
            return true;
        }

        return IsDriveLetterPath(token) || IsTildePath(token);
    }

    private static bool ContainsPathSeparator(string input)
    {
        return TryFindSeparator(input, out _);
    }

    private static char GetSeparator(string input)
    {
        return TryFindSeparator(input, out var separator) ? separator : '/';
    }

    private static bool TryFindSeparator(string input, out char separator)
    {
        // Treat real path separators as signals while skipping JSON escape sequences like \uXXXX or \".
        var hasBackslash = false;
        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (c == '/')
            {
                separator = '/';
                return true;
            }

            if (c != '\\')
            {
                continue;
            }

            if (i + 1 >= input.Length)
            {
                continue;
            }

            var next = input[i + 1];
            if (next == '\\' || next == '/')
            {
                if (next == '/')
                {
                    separator = '/';
                    return true;
                }

                hasBackslash = true;
                i++;
                continue;
            }

            if (next == 'u' && i + 5 < input.Length)
            {
                if (IsHex(input[i + 2]) && IsHex(input[i + 3]) && IsHex(input[i + 4]) && IsHex(input[i + 5]))
                {
                    i += 5;
                    continue;
                }
            }

            if (next is '"' or '\\' or '/' or 'b' or 'f' or 'n' or 'r' or 't')
            {
                i++;
                continue;
            }

            hasBackslash = true;
        }

        if (hasBackslash)
        {
            separator = '\\';
            return true;
        }

        separator = '/';
        return false;
    }

    private static bool IsHex(char value)
    {
        return (value >= '0' && value <= '9')
            || (value >= 'a' && value <= 'f')
            || (value >= 'A' && value <= 'F');
    }

    private static bool IsDriveLetterPath(string token)
    {
        return token.Length >= 3
            && char.IsLetter(token[0])
            && token[1] == ':'
            && (token[2] == '\\' || token[2] == '/');
    }

    private static bool IsTildePath(string token)
    {
        return token.Length >= 2 && token[0] == '~' && (token[1] == '\\' || token[1] == '/');
    }

    private static string SanitizePathCore(string token)
    {
        foreach (var root in KnownRoots.Keys)
        {
            if (token.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                var remainder = token.Substring(root.Length);
                if (string.IsNullOrEmpty(remainder))
                {
                    var rootSeparator = GetSeparator(token);
                    return IsAllowlistedSegment(root) ? $"{Redacted}{rootSeparator}{root}" : Redacted;
                }

                var sep = GetSeparator(token);
                if (IsAllowlistedSegment(root) && (remainder[0] == '/' || remainder[0] == '\\'))
                {
                    return $"{Redacted}{sep}{root}{remainder}";
                }

                var trimmedRemainder = remainder.TrimStart('/', '\\');
                if (string.IsNullOrEmpty(trimmedRemainder))
                {
                    return Redacted + sep;
                }

                return $"{Redacted}{sep}{trimmedRemainder}";
            }
        }

        var sepChar = GetSeparator(token);
        var trimmed = token;
        if (token.StartsWith(@"\\", StringComparison.Ordinal))
        {
            trimmed = token[2..];
        }
        else if (IsDriveLetterPath(token))
        {
            trimmed = token[3..];
        }
        else if (IsTildePath(token))
        {
            trimmed = token[2..];
        }
        else if (token.StartsWith("/", StringComparison.Ordinal))
        {
            trimmed = token[1..];
        }

        var segments = trimmed.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        var allowedIndex = FindAllowlistedSegmentIndex(segments);
        if (allowedIndex >= 0)
        {
            var kept = string.Join(sepChar, segments.Skip(allowedIndex));
            return string.IsNullOrEmpty(kept) ? Redacted : $"{Redacted}{sepChar}{kept}";
        }

        return Redacted;
    }

    private static int FindAllowlistedSegmentIndex(string[] segments)
    {
        for (int i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (IsAllowlistedSegment(segment))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsAllowlistedSegment(string segment)
    {
        if (segment.StartsWith(AzureSdkPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return AllowlistedSegmentSet.ContainsKey(segment);
    }
}
