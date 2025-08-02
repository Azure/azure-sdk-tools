import { discoverResourcesInCode } from "../../../genaisrc/src/azure/extractResources.js";
import { describe, expect, it } from "vitest";

describe("discoverResourcesInCode", () => {
    it("finds Azure SDK client instantiations with literals", () => {
        const code = `
      import { BlobServiceClient } from "@azure/storage-blob";
      import { DefaultAzureCredential } from "@azure/identity";

      const endpoint = process.env.AZURE_STORAGE_ENDPOINT;
      const credential = new DefaultAzureCredential();
      const client = new BlobServiceClient(endpoint, credential);
      const other = new NotAzureClient();
    `;
        const result = discoverResourcesInCode(code);
        expect(result).toEqual([
            {
                args: [
                    {
                        kind: "Identifier",
                        text: "endpoint",
                        type: "string | undefined",
                        originalExpression:
                            "process.env.AZURE_STORAGE_ENDPOINT",
                        envVarName: "AZURE_STORAGE_ENDPOINT",
                    },
                    {
                        kind: "Identifier",
                        text: "credential",
                        type: "DefaultAzureCredential",
                        originalExpression: "new DefaultAzureCredential()",
                        packageName: "@azure/identity",
                    },
                ],
                clientClass: "BlobServiceClient",
                packageName: "@azure/storage-blob",
            },
        ]);
    });

    it("ignores non-Azure SDK clients", () => {
        const code = `
      const foo = new NotAzureClient("bar");
    `;
        const result = discoverResourcesInCode(code);
        expect(result).toEqual([]);
    });

    it("finds multiple Azure SDK client instantiations", () => {
        const code = `
      import { BlobServiceClient } from "@azure/storage-blob";
      import { DefaultAzureCredential } from "@azure/identity";

      const endpoint = process.env.AZURE_STORAGE_ENDPOINT;
      const client1 = new BlobServiceClient(endpoint, new DefaultAzureCredential());
      const client2 = new BlobServiceClient(endpoint, new DefaultAzureCredential());
      const other = new NotAzureClient();
    `;
        const result = discoverResourcesInCode(code);
        expect(result).toEqual([
            {
                args: [
                    {
                        kind: "Identifier",
                        text: "endpoint",
                        type: "string | undefined",
                        originalExpression:
                            "process.env.AZURE_STORAGE_ENDPOINT",
                        envVarName: "AZURE_STORAGE_ENDPOINT",
                    },
                    {
                        kind: "NewExpression",
                        text: "new DefaultAzureCredential()",
                        type: "DefaultAzureCredential",
                        packageName: "@azure/identity",
                    },
                ],
                clientClass: "BlobServiceClient",
                packageName: "@azure/storage-blob",
            },
            {
                args: [
                    {
                        kind: "Identifier",
                        text: "endpoint",
                        type: "string | undefined",
                        originalExpression:
                            "process.env.AZURE_STORAGE_ENDPOINT",
                        envVarName: "AZURE_STORAGE_ENDPOINT",
                    },
                    {
                        kind: "NewExpression",
                        text: "new DefaultAzureCredential()",
                        type: "DefaultAzureCredential",
                        packageName: "@azure/identity",
                    },
                ],
                clientClass: "BlobServiceClient",
                packageName: "@azure/storage-blob",
            },
        ]);
    });

    it("finds environment variables inside a function", () => {
        const code = `
      import { BlobServiceClient } from "@azure/storage-blob";
      import { DefaultAzureCredential } from "@azure/identity";

      function getEndpoint() {
        return process.env.AZURE_STORAGE_ENDPOINT;
      }
      const credential = new DefaultAzureCredential();
      const client = new BlobServiceClient(getEndpoint(), credential);
      const other = new NotAzureClient();
    `;
        const result = discoverResourcesInCode(code);
        expect(result).toEqual([
            {
                args: [
                    {
                        kind: "Identifier",
                        text: "endpoint",
                        type: "string | undefined",
                        originalExpression:
                            "process.env.AZURE_STORAGE_ENDPOINT",
                        envVarName: "AZURE_STORAGE_ENDPOINT",
                    },
                    {
                        kind: "Identifier",
                        text: "credential",
                        type: "DefaultAzureCredential",
                        originalExpression: "new DefaultAzureCredential()",
                        packageName: "@azure/identity",
                    },
                ],
                clientClass: "BlobServiceClient",
                packageName: "@azure/storage-blob",
            },
        ]);
    });

    it("finds environment variables outside a function", () => {
        const code = `
      import { BlobServiceClient } from "@azure/storage-blob";
      import { DefaultAzureCredential } from "@azure/identity";

      const endpoint = process.env.AZURE_STORAGE_ENDPOINT;
      function getEndpoint() {
        return endpoint;
      }
      const credential = new DefaultAzureCredential();
      const client = new BlobServiceClient(getEndpoint(), credential);
      const other = new NotAzureClient();
    `;
        const result = discoverResourcesInCode(code);
        expect(result).toEqual([
            {
                args: [
                    {
                        kind: "Identifier",
                        text: "endpoint",
                        type: "string | undefined",
                        originalExpression:
                            "process.env.AZURE_STORAGE_ENDPOINT",
                        envVarName: "AZURE_STORAGE_ENDPOINT",
                    },
                    {
                        kind: "Identifier",
                        text: "credential",
                        type: "DefaultAzureCredential",
                        originalExpression: "new DefaultAzureCredential()",
                        packageName: "@azure/identity",
                    },
                ],
                clientClass: "BlobServiceClient",
                packageName: "@azure/storage-blob",
            },
        ]);
    });

    it("handles no instantiations", () => {
        const code = `console.log('no clients here');`;
        const result = discoverResourcesInCode(code);
        expect(result).toEqual([]);
    });
});
