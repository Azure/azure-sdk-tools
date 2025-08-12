import path from "node:path";
import * as fs from "node:fs/promises";
import { getUniqueDirName } from "../utils.ts";
import type { TypeCheckParameters, TypeCheckResult } from "../types.ts";

const importRegex = /import\s+(?:[^'"]*\s+from\s+)?["']([^"']+)["']/g;
const requireRegex = /require\(\s*["']([^"']+)["']\s*\)/g;
const dynamicImportRegex = /import\(\s*["']([^"']+)["']\s*\)/g;
const fileName = "temp.ts";
let container: ContainerHost | undefined = undefined;

/** extract “root” pkg:
 *  - '@scope/pkg/sub/path' → '@scope/pkg'
 *  - 'pkg/sub/path'        → 'pkg'
 */
function getPackageRoot(spec: string): string {
    if (spec.startsWith("@")) {
        const [scope, pkg] = spec.split("/", 3);
        return pkg ? `${scope}/${pkg}` : spec;
    }
    return spec.split("/", 1)[0];
}

export function parseImportedPackages(
    code: string,
    excludedPkgs: Set<string>,
): string[] {
    const pkgs = new Set<string>(["typescript", "@types/node"]);
    let match: RegExpExecArray | null;

    for (const regex of [importRegex, requireRegex, dynamicImportRegex]) {
        while ((match = regex.exec(code)) !== null) {
            const raw = match[1];
            if (raw.startsWith(".") || raw.startsWith("/")) continue;
            const pkg = getPackageRoot(raw);
            if (!excludedPkgs.has(pkg)) {
                pkgs.add(pkg);
            }
        }
        regex.lastIndex = 0;
    }

    return Array.from(pkgs);
}

export async function typecheckTypeScript({
    code,
    clientDist,
    pkgName,
}: TypeCheckParameters): Promise<TypeCheckResult> {
    const projectDir = path.join("tmp", getUniqueDirName());
    const filePath = `${projectDir}/${fileName}`;

    const deps: { [key: string]: string } = {};
    for (const pkg of parseImportedPackages(
        code,
        new Set(pkgName ? [pkgName] : []),
    )) {
        deps[pkg] = "latest";
    }

    const packageJson = {
        name: "temp",
        version: "1.0.0",
        dependencies: deps,
        scripts: {
            typecheck: `tsc --noEmit --skipLibCheck --strict --esModuleInterop --noUnusedLocals --noUnusedParameters --noImplicitReturns ${fileName}`,
        },
    };

    if (!container) {
        container = await host.container({
            image: "node:alpine",
            networkEnabled: true,
            persistent: true,
        });
    }

    try {
        await container.writeText(filePath, code);
        await container.writeText(
            `${projectDir}/package.json`,
            JSON.stringify(packageJson),
        );

        const installResult = await container.exec("npm", ["install"], {
            cwd: projectDir,
        });

        if (clientDist) {
            await fs.stat(clientDist);
            await container.copyTo(clientDist, projectDir);
            const distName = path.basename(clientDist);
            await container.exec("npm", ["install", "--no-save", distName], {
                cwd: projectDir,
            });
        }

        const tscResult = await container.exec("npm", ["run", "typecheck"], {
            cwd: projectDir,
        });
        return {
            succeeded:
                installResult.exitCode === 0 &&
                !installResult.failed &&
                tscResult.exitCode === 0 &&
                !tscResult.failed,
            output:
                (installResult.stdout ?? "") +
                (installResult.stderr ?? "") +
                (tscResult.stdout ?? "") +
                (tscResult.stderr ?? ""),
        };
    } finally {
        try {
            await container.exec("rm", ["-rf", projectDir]);
        } catch (cleanupErr) {
            console.warn("Cleanup failed:", cleanupErr);
        }
    }
}
