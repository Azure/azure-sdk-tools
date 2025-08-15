import "@azure-tools/typespec-client-generator-core";
import "./main.tsp";

@@Azure.ClientGenerator.Core.clientNamespace(TestProxy, "Microsoft.ClientModel.TestFramework.TestProxy", "csharp");
@@Azure.ClientGenerator.Core.clientName(TestProxy.Admin, "TestProxyAdminClient", "csharp");