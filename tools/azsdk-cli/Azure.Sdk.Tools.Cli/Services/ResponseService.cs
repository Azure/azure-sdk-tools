using System.Text.Json;
using Azure.Sdk.Tools.Cli.Commands;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IResponseService
{
    string RespondFromJson<T>(string json) where T : BaseCommandResponse;
    string Respond(BaseCommandResponse response);
}

public class ResponseService(ICommandFormatter formatter) : IResponseService
{
    public string RespondFromJson<T>(string json) where T : BaseCommandResponse
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON input cannot be null or empty.", nameof(json));
        }

        var instance = JsonSerializer.Deserialize<T>(json);

        if (instance == null)
        {
            throw new InvalidOperationException("Deserialization resulted in a null instance.");
        }

        instance.Initialize(formatter);
        return instance.ToString();
    }

    public string Respond(BaseCommandResponse response)
    {
        if (response == null)
        {
            throw new ArgumentNullException(nameof(response), "Response cannot be null.");
        }

        response.Initialize(formatter);

        return response.ToString();
    }
}