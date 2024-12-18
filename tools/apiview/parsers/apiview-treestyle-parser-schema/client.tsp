import "@azure-tools/typespec-client-generator-core";
import "@typespec/versioning";
import "./main.tsp";

using TypeSpec.Versioning;
using Azure.ClientGenerator.Core;
using Schemas;

namespace ClientCustomizations;

@@visibility(Schemas.CodeFile.LanguageVariant, "none");