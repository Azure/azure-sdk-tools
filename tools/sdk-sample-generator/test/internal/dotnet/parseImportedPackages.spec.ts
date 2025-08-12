import { describe, it, expect } from "vitest";
import { parseImportedPackages } from "../../../genaisrc/src/dotnet/typecheck.ts";

describe("parseImportedPackages", () => {
    it("should parse Azure SDK using statements", () => {
        const code = `
using System;
using Azure.Storage.Blobs;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            var client = new BlobServiceClient();
        }
    }
}`;

        const result = parseImportedPackages(code, new Set());
        expect(result).toContain("Azure.Storage.Blobs");
        expect(result).toContain("Azure.Identity");
        expect(result).not.toContain("System");
        expect(result).not.toContain("Microsoft.Extensions.Logging");
    });

    it("should parse PackageReference elements", () => {
        const code = `
<PackageReference Include="Azure.AI.OpenAI" Version="1.0.0" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
`;

        const result = parseImportedPackages(code, new Set());
        expect(result).toContain("Azure.AI.OpenAI");
        expect(result).toContain("Newtonsoft.Json");
    });

    it("should exclude packages in excludedPkgs set", () => {
        const code = `
using Azure.Storage.Blobs;
using Azure.Identity;
`;

        const result = parseImportedPackages(code, new Set(["Azure.Identity"]));
        expect(result).toContain("Azure.Storage.Blobs");
        expect(result).not.toContain("Azure.Identity");
    });

    it("should handle complex Azure SDK namespaces", () => {
        const code = `
using Azure.Storage.Files.Shares;
using Azure.Messaging.ServiceBus;
using Azure.Security.KeyVault.Secrets;
`;

        const result = parseImportedPackages(code, new Set());
        expect(result).toContain("Azure.Storage.Files.Shares");
        expect(result).toContain("Azure.Messaging.ServiceBus");
        expect(result).toContain("Azure.Security.KeyVault.Secrets");
    });
});
