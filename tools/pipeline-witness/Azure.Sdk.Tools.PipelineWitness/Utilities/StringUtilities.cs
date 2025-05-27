using System;
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.PipelineWitness.Utilities;

public partial class StringUtilities
{
    [GeneratedRegex(@"\x1B\[[0-?]*[ -/]*[@-~]")]
    private static partial Regex AnsiiEscapeRegex();

    [GeneratedRegex(@"^(?:(?<timestamp>\d\d\d\d-\d\d-\d\dT\d\d:\d\d:\d\d(?:.\d+)?Z) )?(?<message>.*)$")]
    private static partial Regex TimestampedLogLineRegex();

    public static string StripAnsiiEsacpeSequences(string input)
    {
        return AnsiiEscapeRegex().Replace(input, "");
    }

    public static (DateTimeOffset TimeStamp, string Message) ParseLogLine(string line, DateTimeOffset defaultTimestamp)
    {
        // log lines usually follow the format:
        // 2022-03-30T21:38:38.7007903Z Downloading task: AzureKeyVault (1.200.0)
        // If there's no leading timestamp, we return the entire line as Message.
        Match match = TimestampedLogLineRegex().Match(line);

        if (!match.Success)
        {
            return (defaultTimestamp, StripAnsiiEsacpeSequences(line));
        }

        string timeStampText = match.Groups["timestamp"].Value;

        DateTimeOffset timestamp = !string.IsNullOrEmpty(timeStampText)
            ? DateTimeOffset.Parse(timeStampText).ToUniversalTime()
            : defaultTimestamp;

        string message = StripAnsiiEsacpeSequences(match.Groups["message"].Value);

        return (timestamp, message);
    }
}
