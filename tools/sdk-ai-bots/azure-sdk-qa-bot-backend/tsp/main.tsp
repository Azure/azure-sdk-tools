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
  title: "Azure SDK QA Assistant Service",
  version: "0.0.1"
})
@server("https://azuresdkbot-azuresdkbot-dev-czhxctdndmfdb5hq.eastasia-01.azurewebsites.net", "Development server")
@server("https://azuresdkbot-dqh7g6btekbfa3hh.eastasia-01.azurewebsites.net", "Production server")
@versioned(Versions)
namespace AzureSDK.QAAssistant;

@doc("The API versions for the Azure SDK QA Assistant Service")
enum Versions {
  @doc("Version 0.0.1 - Initial beta release with basic completion and feedback functionality")
  v0_0_1: "0.0.1",
}
