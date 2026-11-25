import "@azure-tools/typespec-client-generator-core";
import "@typespec/versioning";
using Azure.ClientGenerator.Core;
using TypeSpec.Versioning;
namespace Microsoft.Widget;
@removed(Versions.v2024_10_01_preview)
@renamedFrom(Versions.v2024_10_01_preview, "EmployeeClientDetails")
@access(Access.internal)
model EmployeeClientDetailsBeforePreview { value?: string; }
@added(Versions.v2024_10_01_preview)
@access(Access.public)
model EmployeeClientDetails { value?: string; }
