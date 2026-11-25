import "@azure-tools/typespec-client-generator-core";
import "@typespec/versioning";
using Azure.ClientGenerator.Core;
using TypeSpec.Versioning;
namespace Microsoft.Widget;
@access(Access.public)
model EmployeeClientDetails { value?: string; }
