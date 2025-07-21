import "typespec-mcp";
using MCP;

@mcpServer(#{
  name: "AzureTypeSpecMCP",
  version: "1.0.0",
  instructions: "Use this MCP server to work on TypeSpec projects. It provides tools for onboarding to TypeSpec.",
})
namespace AzureTypeSpecMCP;

/**
 * This interface provides tools for initializing TypeSpec projects and converting existing Azure service swagger definitions to TypeSpec projects.
 * Use this interface to onboard to TypeSpec for new services or convert existing services.
 */
interface Init {
  /**
   * This is the tool to call when you want to initialize a new TypeSpec project.
   * **Call this tool when starting a new TypeSpec project.**
   * Pass in the `template` to use: `azure-core` for data-plane services, or `azure-arm` for resource-manager services.
   * Pass in the `serviceNamespace` to use, which is the namespace of the service you are creating. Should be Pascal case.
   * Pass in the `outputDirectory` where the project should be created. This must be an existing empty directory.
   * Returns the path to the created project.
   */
  @tool quickstart(
    /**
     * The template to use for the TypeSpec project.
     * Valid values are:
     * - `azure-core`: for data-plane services.
     * - `azure-arm`: for resource-manager services.
     */
    template: string,

    /**
     * The namespace of the service you are creating.
     * This should be in Pascal case and represent the service's namespace.
     * For example, "MyService" for a service named "My Service".
     */
    serviceNamespace: string,

    /**
     * The output directory for the generated TypeSpec project.
     * This directory must already exist.
     */
    outputDirectory: string,
  ): string;

  /**
   * Converts an existing Azure service swagger definition to a TypeSpec project.
   * **Call this tool when trying to convert an existing Azure service to TypeSpec.**
   * This command should only be ran once to get started working on a TypeSpec project.
   * Verify whether the source swagger describes an Azure Resource Management (ARM) API
   * or a data plane API if unsure.
   * Pass in the `pathToSwaggerReadme` which is the path or URL to the swagger README file.
   * Pass in the `outputDirectory` where the TypeSpec project should be created. This must be an existing empty directory.
   * Pass in `isAzureResourceManagement` to indicate whether the swagger is for an Azure Resource Management (ARM) API.
   * This should be true if the swagger's path contains `resource-manager`.
   * Pass in `fullyCompatible` to indicate whether the generated TypeSpec project should be fully compatible with the swagger.
   * It is recommended not to set this to `true` so that the converted TypeSpec project
   * leverages TypeSpec built-in libraries with standard patterns and templates.
   * Returns path to the created project.
   */
  @tool convertSwagger(
    /**
     * The path or URL to an Azure swagger README file.
     */
    pathToSwaggerReadme: string,

    /**
     * The output directory for the generated TypeSpec project.
     * This directory must already exist.
     */
    outputDirectory: string,

    /**
     * Whether the generated TypeSpec project is for an Azure Resource Management (ARM) API.
     * This should be true if the swagger's path contains `resource-manager`.
     */
    isAzureResourceManagement?: boolean,

    /**
     * Whether to generate a TypeSpec project that is fully compatible with the swagger.
     * It is recommended not to set this to `true` so that the converted TypeSpec project
     * leverages TypeSpec built-in libraries with standard patterns and templates.
     */
    fullyCompatible?: boolean = false,
  ): string;
}
