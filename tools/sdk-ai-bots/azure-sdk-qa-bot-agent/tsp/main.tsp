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
  title: "Azure SDK QA Bot Agent Service",
})
@server("http://localhost:8080", "Local development server")
@versioned(Versions)
namespace AzureSDK.QAAgent;

@doc("The API versions for the Azure SDK QA Bot Agent Service")
enum Versions {
  @doc("Version 0.0.1 - Initial release with chat, feedback, and conversation APIs")
  v0_0_1: "0.0.1",
}
