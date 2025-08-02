import type { TypeCheckParameters, TypeCheckResult } from "../types.ts";
import { getUniqueDirName } from "../utils.ts";
import path from "node:path";

const fileName = "temp.py";
let container: ContainerHost | undefined = undefined;

const importRegex = /^\s*(?:import|from)\s+([a-zA-Z0-9_\.]+)/gm;

function inferRequirementsFromCode(
    code: string,
    excludedPkgs: Set<string>,
): string[] {
    const requirements = new Set<string>();
    let match: RegExpExecArray | null;
    while ((match = importRegex.exec(code)) !== null) {
        const pkg = match[1];
        if (
            ![
                "os",
                "sys",
                "re",
                "math",
                "json",
                "datetime",
                "time",
                "typing",
            ].includes(pkg) &&
            !excludedPkgs.has(pkg)
        ) {
            requirements.add(pkg);
        }
    }
    return Array.from(requirements);
}

export async function typecheckPython(
    inputs: TypeCheckParameters,
): Promise<TypeCheckResult> {
    const { code, clientDist, pkgName } = inputs;
    const projectDir = path.join("tmp", getUniqueDirName());
    const filePath = `${projectDir}/${fileName}`;

    const requirementsList = inferRequirementsFromCode(
        code,
        new Set(!pkgName ? [] : [pkgName]),
    );
    const requirementsContent = requirementsList.join("\n");

    if (!container) {
        container = await host.container({
            image: "python:3.11-alpine",
            networkEnabled: true,
            persistent: true,
        });
        await container.exec("pip", ["install", "mypy", "flake8"]);
    }

    try {
        await container.writeText(filePath, code);

        if (requirementsContent) {
            await container.writeText(
                `${projectDir}/requirements.txt`,
                requirementsContent,
            );
            await container.exec("pip", ["install", "-r", "requirements.txt"], {
                cwd: projectDir,
            });
        }

        if (clientDist) {
            const whlFileName = path.basename(clientDist);
            await container.copyTo(clientDist, `${projectDir}/${whlFileName}`);
            await container.exec("pip", ["install", whlFileName], {
                cwd: projectDir,
            });
        }

        // Run mypy for type checking
        const mypyResult = await container.exec(
            "mypy",
            [fileName, "--ignore-missing-imports"],
            { cwd: projectDir },
        );

        // Run flake8 for unused variable checking
        const flake8Result = await container.exec(
            "flake8",
            [fileName, "--select=F841"],
            { cwd: projectDir },
        );

        return {
            succeeded:
                mypyResult.exitCode === 0 &&
                !mypyResult.failed &&
                flake8Result.exitCode === 0 &&
                !flake8Result.failed,
            output: [
                `mypy output:\n${(mypyResult.stdout ?? "") + (mypyResult.stderr ?? "")}`,
                `flake8 output:\n${(flake8Result.stdout ?? "") + (flake8Result.stderr ?? "")}`,
            ].join("\n"),
        };
    } finally {
        try {
            await container.exec("rm", ["-rf", projectDir]);
        } catch (cleanupErr) {
            console.warn("Cleanup failed:", cleanupErr);
        }
    }
}
