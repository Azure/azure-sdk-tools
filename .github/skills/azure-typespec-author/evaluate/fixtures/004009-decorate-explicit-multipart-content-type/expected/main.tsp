import "@typespec/http";
import "@typespec/rest";
import "@typespec/versioning";
import "@azure-tools/typespec-azure-core";
import "./employee.tsp";
using TypeSpec.Versioning;
@service(#{ title: "Widget data-plane upload" })
@versioned(Versions)
namespace WidgetUpload;
enum Versions {
  v2021_11_01: "2021-11-01",
  v2024_10_01_preview: "2024-10-01-preview"
}
