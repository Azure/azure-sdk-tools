import "@azure-tools/typespec-client-generator-core";
import "@typespec/versioning";
import "./main.tsp";

using TypeSpec.Versioning;
using Azure.ClientGenerator.Core;
using Schemas;

@TypeSpec.Versioning.useDependency(ServiceVersion.V2023_07_01)
namespace ClientCustomizations;

// model properties 
@@access(ReviewLine.IsHidden, Access.internal);