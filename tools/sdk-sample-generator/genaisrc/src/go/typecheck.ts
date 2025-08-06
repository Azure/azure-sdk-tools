import type { TypeCheckParameters, TypeCheckResult } from "../types.ts";
import { getUniqueDirName } from "../utils.ts";
import path from "node:path";

const fileName = "temp.go";
let container: ContainerHost | undefined = undefined;

export async function typecheckGo(
    inputs: TypeCheckParameters,
): Promise<TypeCheckResult> {
    const { code, clientDist } = inputs;
    const projectDir = path.join("tmp", getUniqueDirName());
    const filePath = `${projectDir}/${fileName}`;

    if (!container) {
        container = await host.container({
            image: "golang:1.24.2-alpine",
            networkEnabled: true,
            persistent: true,
        });
    }

    try {
        await container.writeText(filePath, code);

        await container.exec("go", ["mod", "init", "tempmod"], {
            cwd: projectDir,
        });

        if (clientDist) {
            const pkgBaseName = path.basename(clientDist);
            await container.copyTo(
                clientDist,
                path.join(projectDir, pkgBaseName),
            );
            await container.exec(
                "go",
                ["mod", "edit", `-replace=yourmod=./${pkgBaseName}`],
                { cwd: projectDir },
            );
        }

        // Tidy up the Go module
        const tidyResult = await container.exec("go", ["mod", "tidy"], {
            cwd: projectDir,
        });

        // Run go build to check for type errors
        const buildResult = await container.exec("go", ["build", fileName], {
            cwd: projectDir,
        });

        // Run golint for linting (including unused variables)
        await container.exec("go", [
            "install",
            "golang.org/x/lint/golint@latest",
        ]);
        const golintBin = "/go/bin/golint";
        const lintResult = await container.exec(golintBin, [fileName], {
            cwd: projectDir,
        });

        return {
            succeeded:
                tidyResult.exitCode === 0 &&
                !tidyResult.failed &&
                buildResult.exitCode === 0 &&
                !buildResult.failed &&
                lintResult.exitCode === 0 &&
                !lintResult.failed,
            output: [
                `go mod tidy output:\n${(tidyResult.stdout ?? "") + (tidyResult.stderr ?? "")}`,
                `go build output:\n${(buildResult.stdout ?? "") + (buildResult.stderr ?? "")}`,
                `golint output:\n${(lintResult.stdout ?? "") + (lintResult.stderr ?? "")}`,
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
