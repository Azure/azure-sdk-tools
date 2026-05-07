import "@typespec/http";
import "@typespec/rest";
import "@typespec/openapi3";
import "@typespec/versioning";
import "./models.tsp";
import "./routes.tsp";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Versioning;

@service({
  title: "Azure SDK QA Bot Agent Service",
})
@server("http://localhost:8080", "Local development server")
@versioned(Versions)
namespace AzureSdkQaBot;

@doc("The API versions for the Azure SDK QA Bot Agent Service")
enum Versions {
  @doc("Version 0.1.0 - Initial release with chat, feedback, and conversation APIs")
  v0_1_0: "0.1.0",
}
