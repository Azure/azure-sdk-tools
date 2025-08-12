import path from "node:path";
import * as fs from "node:fs/promises";
import { getUniqueDirName } from "../utils.ts";
import type { TypeCheckParameters, TypeCheckResult } from "../types.ts";

const usingRegex = /using\s+([\w\.]+)(?:\s*=.*)?;/g;
const packageRefRegex = /<PackageReference\s+Include\s*=\s*"([^"]+)"/g;
const fileName = "Program.cs";
let container: ContainerHost | undefined = undefined;

/** extract package name from using statements:
 *  - 'Azure.Storage.Blobs' → 'Azure.Storage.Blobs'
 *  - 'System.Threading.Tasks' → 'System.Threading.Tasks' (will be filtered out)
 */
function getPackageName(usingStatement: string): string {
    return usingStatement.trim();
}

export function parseImportedPackages(
    code: string,
    excludedPkgs: Set<string>,
): string[] {
    const pkgs = new Set<string>();
    let match: RegExpExecArray | null;

    // Parse using statements
    while ((match = usingRegex.exec(code)) !== null) {
        const pkg = getPackageName(match[1]);

        // Skip System.* namespaces as they're built-in
        if (
            !pkg.startsWith("System") &&
            !pkg.startsWith("Microsoft.Extensions.Logging") && // Common built-in
            !excludedPkgs.has(pkg)
        ) {
            // Map common Azure SDK namespaces to their package names
            const packageName = mapNamespaceToPackage(pkg);
            if (packageName) {
                pkgs.add(packageName);
            }
        }
    }

    // Also parse any existing PackageReference elements in the code
    while ((match = packageRefRegex.exec(code)) !== null) {
        const pkg = match[1];
        if (!excludedPkgs.has(pkg)) {
            pkgs.add(pkg);
        }
    }

    usingRegex.lastIndex = 0;
    packageRefRegex.lastIndex = 0;

    return Array.from(pkgs);
}

function mapNamespaceToPackage(namespace: string): string | null {
    // Map common Azure SDK namespaces to their NuGet package names
    const namespaceToPackageMap: { [key: string]: string } = {
        "Azure.Storage.Blobs": "Azure.Storage.Blobs",
        "Azure.Storage.Files.Shares": "Azure.Storage.Files.Shares",
        "Azure.Storage.Queues": "Azure.Storage.Queues",
        "Azure.Data.Tables": "Azure.Data.Tables",
        "Azure.Messaging.ServiceBus": "Azure.Messaging.ServiceBus",
        "Azure.Messaging.EventHubs": "Azure.Messaging.EventHubs",
        "Azure.Security.KeyVault.Secrets": "Azure.Security.KeyVault.Secrets",
        "Azure.Security.KeyVault.Keys": "Azure.Security.KeyVault.Keys",
        "Azure.Security.KeyVault.Certificates":
            "Azure.Security.KeyVault.Certificates",
        "Azure.AI.OpenAI": "Azure.AI.OpenAI",
        "Azure.AI.TextAnalytics": "Azure.AI.TextAnalytics",
        "Azure.Search.Documents": "Azure.Search.Documents",
        "Azure.ResourceManager": "Azure.ResourceManager",
        "Azure.Identity": "Azure.Identity",
        "Azure.Core": "Azure.Core",
        "Microsoft.Azure.Cosmos": "Microsoft.Azure.Cosmos",
        "Microsoft.Graph": "Microsoft.Graph",
        "Newtonsoft.Json": "Newtonsoft.Json",
    };

    // Try exact match first
    if (namespaceToPackageMap[namespace]) {
        return namespaceToPackageMap[namespace];
    }

    // Try prefix matching for Azure SDK packages
    for (const [ns, pkg] of Object.entries(namespaceToPackageMap)) {
        if (namespace.startsWith(ns + ".")) {
            return pkg;
        }
    }

    // For other Azure.* namespaces, try to infer the package name
    if (namespace.startsWith("Azure.")) {
        const parts = namespace.split(".");
        if (parts.length >= 3) {
            return parts.slice(0, 3).join(".");
        }
    }

    return null;
}

export async function typecheckDotNet({
    code,
    clientDist,
    pkgName,
}: TypeCheckParameters): Promise<TypeCheckResult> {
    const projectDir = path.join("tmp", getUniqueDirName());
    const filePath = `${projectDir}/${fileName}`;

    const packages = parseImportedPackages(
        code,
        new Set(pkgName ? [pkgName] : []),
    );

    // Create a simple console application project file
    const projectFile = `<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
${packages.map((pkg) => `    <PackageReference Include="${pkg}" Version="*" />`).join("\n")}
  </ItemGroup>

</Project>`;

    if (!container) {
        container = await host.container({
            image: "mcr.microsoft.com/dotnet/sdk",
            networkEnabled: true,
            persistent: true,
        });
    }

    try {
        // Write the C# source file
        await container.writeText(filePath, code);

        // Write the project file
        await container.writeText(`${projectDir}/Project.csproj`, projectFile);

        // Restore packages
        const restoreResult = await container.exec("dotnet", ["restore"], {
            cwd: projectDir,
        });

        // If there's a custom client distribution, copy it
        if (clientDist) {
            try {
                await fs.stat(clientDist);
                await container.copyTo(clientDist, projectDir);
                const distName = path.basename(clientDist);

                // Add reference to the local package if it's a .nupkg file
                if (distName.endsWith(".nupkg")) {
                    await container.exec(
                        "dotnet",
                        [
                            "add",
                            "package",
                            distName.replace(".nupkg", ""),
                            "--source",
                            ".",
                        ],
                        {
                            cwd: projectDir,
                        },
                    );
                }
            } catch (error) {
                console.warn("Failed to handle clientDist:", error);
            }
        }

        // Build the project (this will perform type checking)
        const buildResult = await container.exec(
            "dotnet",
            ["build", "--no-restore", "--verbosity", "normal"],
            {
                cwd: projectDir,
            },
        );

        return {
            succeeded:
                restoreResult.exitCode === 0 &&
                !restoreResult.failed &&
                buildResult.exitCode === 0 &&
                !buildResult.failed,
            output:
                (restoreResult.stdout ?? "") +
                (restoreResult.stderr ?? "") +
                (buildResult.stdout ?? "") +
                (buildResult.stderr ?? ""),
        };
    } finally {
        try {
            await container.exec("rm", ["-rf", projectDir]);
        } catch (cleanupErr) {
            console.warn("Cleanup failed:", cleanupErr);
        }
    }
}
