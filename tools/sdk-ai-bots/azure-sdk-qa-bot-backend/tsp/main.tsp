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
})
@server("https://azuresdkqabot-dev-server-hrcrckaad5gcedcv.westus2-01.azurewebsites.net", "Development server")
@server("https://azuresdkqabot-test-server-cbdbh2gva6h7hrfr.westus2-01.azurewebsites.net", "Testing server")
@server("https://azuresdkqabot-server-c8czabhzhweadwgu.westus2-01.azurewebsites.net", "Production server")
@versioned(Versions)
namespace AzureSDK.QAAssistant;

@doc("The API versions for the Azure SDK QA Assistant Service")
enum Versions {
  @doc("Version 0.0.1 - Initial beta release with basic completion and feedback functionality")
  v0_0_1: "0.0.1",
}
