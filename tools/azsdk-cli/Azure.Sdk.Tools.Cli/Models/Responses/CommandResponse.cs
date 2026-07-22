// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Status
{
    Succeeded,
    Failed
}

public abstract class CommandResponse
{
    private int? exitCode = null;
    [JsonIgnore]
    public virtual int ExitCode
    {
        get
        {
            if (null != exitCode) { return exitCode.Value; }
            if (!string.IsNullOrEmpty(ResponseError) || (ResponseErrors?.Count ?? 0) > 0)
            {
                return 1;
            }
            return 0;
        }
        set => exitCode = value;
    }

    [JsonIgnore]
    public virtual bool WriteToStdoutOnFailure => false;

    /// <summary>
    /// ResponseError represents a single error message associated with the response.
    /// </summary>
    [JsonPropertyName("response_error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual string? ResponseError { get; set; }

    /// <summary>
    /// ResponseErrors represents a list of error messages associated with the response.
    /// </summary>
    [JsonPropertyName("response_errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string> ResponseErrors { get; set; }

    /// <summary>
    /// NextSteps provides guidance or recommended actions regarding the response.
    /// </summary>
    [JsonPropertyName("next_steps")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? NextSteps { get; set; }

    /// <summary>
    /// Status shows whether the command operation was successful.
    /// </summary>
    [JsonPropertyName("operation_status")]
    public Status OperationStatus
    {
        get
        {
            return string.IsNullOrEmpty(ResponseError) && (ResponseErrors == null || ResponseErrors.Count == 0) ? Status.Succeeded : Status.Failed;
        }
    }

    public static readonly string SupportChannelMessage =
        "If you need any assistance, please reach out to the AzSDK Agent team via the Teams channel: https://teams.microsoft.com/l/channel/19%3A6d2c19322c254a80bcc521675134da03%40thread.skype/AzSDK%20Tools%20Agent?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47";

    /// <summary>
    /// Support channel details.
    /// </summary>
    [JsonPropertyName("support_channel")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual string? SupportChannel => OperationStatus == Status.Failed ? SupportChannelMessage : null;
    

    protected abstract string Format();

    public override string ToString()
    {
        var value = Format();
        List<string> messages = [];

        if (OperationStatus == Status.Succeeded && !string.IsNullOrWhiteSpace(value))
        {
            messages.Add(value);
        }
        
        if (!string.IsNullOrEmpty(ResponseError))
        {
            messages.Add("[ERROR] " + ResponseError);
        }
        foreach (var error in ResponseErrors ?? [])
        {
            messages.Add("[ERROR] " + error);
        }

        if (NextSteps?.Count > 0)
        {
            messages.Add("[NEXT STEPS]");
            foreach (var step in NextSteps)
            {
                messages.Add(step);
            }
        }

        if (SupportChannel != null)
        {
            messages.Add(SupportChannel);
        }

        if (messages.Count > 0)
        {
            value = string.Join(Environment.NewLine, messages);
        }

        return value;
    }
}
