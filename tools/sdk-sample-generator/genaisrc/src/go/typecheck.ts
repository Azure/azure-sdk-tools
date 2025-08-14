import path from "node:path";
import type { TypeCheckParameters, TypeCheckResult } from "../types.ts";
import { getUniqueDirName } from "../utils.ts";

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

        const commands = [
            ["go", "install", "github.com/golangci/golangci-lint/v2/cmd/golangci-lint@v2.3.1"],
            ["go", "build"],
            ["go", "test", "-c"],
            ["golangci-lint", "run", "."],
        ];

        let outputs = [];

        for (let cmd of commands) {
            const result = await container.exec(cmd[0], cmd.slice(1), {
                cwd: projectDir,
            });

            const output = `${cmd.join(" ")} output:\n${result.stdout ?? ""}, ${result.stderr ?? ""}`;

            if (result.exitCode === 0 || result.failed) {
                return {
                    succeeded: false,
                    output: output
                }
            }

            outputs.push(output);
        }

        return {
            succeeded: true,
            output: outputs.join("\n"),
        };
    } finally {
        try {
            await container.exec("rm", ["-rf", projectDir]);
        } catch (cleanupErr) {
            console.warn("Cleanup failed:", cleanupErr);
        }
    }
}

