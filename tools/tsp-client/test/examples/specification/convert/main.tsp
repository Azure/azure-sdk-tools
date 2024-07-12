import "@typespec/rest";
import "@typespec/http";
import "./routes.tsp";

using TypeSpec.Rest;
using TypeSpec.Http;
@service({
  title: "AzureSphereManagementClient",
})
@versioned(Versions)
@server(
  "{$host}",
  "Azure Sphere resource management API.",
  {
    $host: string,
  }
)
@doc("Azure Sphere resource management API.")
namespace AzureSphereManagementClient;

@doc("The available API versions.")
enum Versions {
  v2024_04_01: "2024-04-01",
}
