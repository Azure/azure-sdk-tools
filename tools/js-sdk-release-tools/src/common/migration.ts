import { logger } from "../utils/logger";
import { getNpmPackageInfo } from "./npmUtils";
import { runCommand, runCommandOptions } from "./utils";

export async function migratePackage(packageDirectory: string): Promise<void> {
    const info = await getNpmPackageInfo(packageDirectory);
    logger.info(`Start to migrate package '${info.name}'`);
    await runCommand(
        "npx",
        `dev-tool admin migrate-package --package-name=${info.name}`.split(" "),
        { ...runCommandOptions, cwd: packageDirectory }
    );
    logger.info(`Migrated package '${info.name}' successfully`);
}
