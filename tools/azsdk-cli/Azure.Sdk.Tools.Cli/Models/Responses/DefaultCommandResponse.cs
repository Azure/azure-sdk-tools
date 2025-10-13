// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class DefaultCommandResponse : CommandResponse
{
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = true
    };

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Message { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("duration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long Duration { get; set; }

    public override string ToString()
    {
        var output = new StringBuilder();
        if (!string.IsNullOrEmpty(Message))
        {
            output.AppendLine(Message);
        }
        if (Result != null)
        {
            if (Result is System.Collections.IEnumerable enumerable && Result is not string)
            {
                var outputs = enumerable.Cast<object>().Select(item => item?.ToString());
                foreach (var item in outputs)
                {
                    output.AppendLine(item);
                }
            }
            else
            {
                output.AppendLine(JsonSerializer.Serialize(Result, serializerOptions));
            }
        }
        if (Duration > 0)
        {
            output.AppendLine($"Duration: {Duration}ms");
        }

        return ToString(output);
    }

    public static implicit operator DefaultCommandResponse(string s) => new() { Message = s };
}
