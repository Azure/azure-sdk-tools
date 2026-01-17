# azsdk typespec retrieve-solution

The `typespec retrieve-solution` command connects to the Azure knowledge base service `/completion` endpoint.

It can be access via the CLI or as an MCP tool.

## Configuration

You can configure azure knowledge base service via environment variables if you want to use your own service.

- AZURE_SDK_KB_ENDPOINT
- AZURE_SDK_KB_CLIENT_ID
- AZURE_SDK_KB_SCOPE

If the Knowledge base service need authentication, you need provide `AZURE_SDK_KB_CLIENT_ID` and `AZURE_SDK_KB_SCOPE`

These are currently settable as environment variables.

## Building

Follow the [project readme](../../../README.md) to learn how to build and run this project.

## Configuring MCP

For vscode, create the `.vscode/mcp.json` file and add the development build of this package.

```json
{
  "servers": {
    "azure-sdk-mcp": {
      "type": "stdio",
      "command": "/path/to/repo/azure-sdk-tools/artifacts/bin/Azure.Sdk.Tools.Cli/Debug/net8.0/azsdk",
      "args": ["start"]
    }
  }
}
```

### Configure Azure Knowledge base service

If you want to use a different Azure Knowledge Base service instead of the default one, set the AZURE_SDK_KB_ENDPOINT environment variable to specify the endpoint.

```json
{
  "servers": {
    "azure-sdk-mcp": {
      "type": "stdio",
      "command": "/path/to/repo/azure-sdk-tools/artifacts/bin/Azure.Sdk.Tools.Cli/Debug/net8.0/azsdk",
      "args": ["start"],
      "env": {
        "AZURE_SDK_KB_ENDPOINT": "https://completion.endpoint"
      }
    }
  }
}
```

If the service is deployed in Azure with built-in Microsoft authentication enabled, you must also set the AZURE_SDK_KB_CLIENT_ID and AZURE_SDK_KB_SCOPE environment variables. These variables should reference the application (client) ID of the service and the authentication scope. You can find both the endpoint and the client ID in the Azure SDK QA backend service configuration blob.

```
{
  "servers": {
    "azure-sdk-mcp": {
      "type": "stdio",
      "command": "/path/to/repo/azure-sdk-tools/artifacts/bin/Azure.Sdk.Tools.Cli/Debug/net8.0/azsdk",
      "args": ["start"],
      "env": {
        "AZURE_SDK_KB_ENDPOINT": "https://azuresdkqabot-endpoint.azurewebsites.net",
        "AZURE_SDK_KB_CLIENT_ID": "azure-web-service-application-id",
        "AZURE_SDK_KB_SCOPE": "azure-web-service-authentication-scope"
      }
    }
  }
}

```
