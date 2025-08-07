// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class DefaultCommandResponse : Response
{
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Message { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("duration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long Duration { get; set; }

    public override string ToString()
    {
        var output = $"Message: {Message}" + Environment.NewLine +
                     $"Result: {Result?.ToString() ?? "null"}" + Environment.NewLine +
                     $"Duration: {Duration}ms";
        return ToString(output);
    }
}
