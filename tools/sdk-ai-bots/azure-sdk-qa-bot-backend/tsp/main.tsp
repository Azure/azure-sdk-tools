import "@typespec/http";
import "@typespec/rest";
import "@typespec/openapi3";
import "@typespec/versioning";
import "./model.tsp";
import "./route.tsp";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Versioning;

@service({
  title: "Azure SDK QA Bot Backend",
  version: "1.0.0"
})
@server("https://azuresdkbot-azuresdkbot-dev-czhxctdndmfdb5hq.eastasia-01.azurewebsites.net", "Development server")
@server("https://azuresdkbot-dqh7g6btekbfa3hh.eastasia-01.azurewebsites.net", "Production server")
@versioned(Versions)
namespace AzureSDKQABot;

@doc("The API versions for the Azure SDK QA Bot Backend service")
enum Versions {
  @doc("Version 1.0.0 - Initial release with basic completion and feedback functionality")
  v1_0_0: "1.0.0",
}
