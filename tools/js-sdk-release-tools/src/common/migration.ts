import { logger } from "../utils/logger";
import { runCommand, runCommandOptions } from "./utils";
import { load } from "@npmcli/package-json";

// TODO: remove when emitter is ready
export async function migratePackage(packageDirectory: string, rushxScript: string): Promise<void> {
    let packageJson = await load(packageDirectory);
    packageJson.content.scripts![
        "migrate"
    ] = `dev-tool admin migrate-package --package-name=${packageJson.content.name}`;
    packageJson = packageJson.update(packageJson.content);
    packageJson.save();
    await runCommand("node", [rushxScript, "migrate"], {
        ...runCommandOptions,
        cwd: packageDirectory,
    });
    logger.info(`Migrated package '${packageJson.content.name}' successfully`);
}
