# The engsys `everything` server

This tool will serve as the primary integration point for all of the `azure-sdk` provided MCP tools. This tool will be built and published out of the `azure-sdk-tools` repo, but consumed primarily through the `.vscode/mcp.json` within each `Azure-sdk-for-X` language repo. Tool installation will be carried out by the eng/common scripts present at `eng/common/mcp/azure-sdk-mcp.ps`.

## Integration

This `everything` or `hub` server starts up and _should_ be able to pass requests for the requested tool. How the server actually runs these classes is very important. Especially when it comes to versioning etc.

These are different proposals for how we can organize this. Examples will be linked within this same folder until we settle on one.

### Specific classes within a server

Users will add classes to a specific folder in the hub server's code. These classes will have to meet an interface, but will be free to implement however they want other than that.

|Pro/Con|Description|
|---|---|
|✅|Very easy for users to add new functionalities.|
|🟥|Version selection will be gated at the server.|
|🟥|Aside from disabling specific `tools` and changing installed version of the `hub` server, users won't be able to customize their server installation|

### Individual MCP servers that are **referenced** from `hub`

Everything server csproj:

```xml
  <ItemGroup>
    <PackageReference Include="Azure.SDK.Tools.MCP.ToolName1" Version="1.0.0" />
    <PackageReference Include="Azure.SDK.Tools.MCP.ToolName2" Version="1.0.0" />
    <PackageReference Include="Azure.SDK.Tools.MCP.ToolName3" Version="1.0.0" />
```

My vision here is that these individual tools should be:
- implemented as standalone servers
- when DI-ed into our `hub` server, provide a `route` that can be provided by the hub server

This way, users can add local `mcp`

|Pro/Con|Description|
|---|---|
|✅|Tools will work standalone for easy development and testing.|
|✅|Much more granular control over what versions of which tools are present in the hub.|
|🟥|Users will have to match a server convention at the very least, and implement an interface that makes their actual workload pluggable into the `hub`.|
|🟥|EngSys will need to add good integration testing of the contract, so that local development -> deployment in `everything server` will never surprise them.|

## Additional features

Concept of `chaining` of various MCP servers. Can this hub support taking the output of one spoke and shoving it to another? We probably should right?


## Some random DOs and DON'Ts of this server

- [x] DO build with the idea that authentication WILL be coming. Right now the sole protection we have is that these tools will be running in context of the "current user." This means access to `DefaultAzureCredential` should be enough to allow it to function. Users will be adding other external servers to their `mcp.json` at their own risk.
  - What does this actually look like in practice?
- [x] DO Provide `--tools` startup parameter:
  - Provide `--tools <name>,<name>,<name>` to _enable_ specific functionalities of the `hub` server?
  - Provide `--tools-exclude <name>,<name>,<name>` to _disable_ specific functionalities of the `hub` server?
