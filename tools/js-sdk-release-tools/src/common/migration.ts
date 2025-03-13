import { logger } from "../utils/logger.js";
import { runCommand, runCommandOptions } from "./utils.js";
import { getNpmPackageInfo } from "./npmUtils.js";
import { ensureDir } from "fs-extra";
import { posix } from "path";

// TODO: remove when emitter is ready
export async function migratePackage(packageDirectory: string): Promise<void> {
    const info = await getNpmPackageInfo(packageDirectory);
    // Note: bug in migration tool: failed to create review directory
    await ensureDir(posix.join(packageDirectory, 'review'));
    await runCommand(
        "npm",
        `exec -- dev-tool admin migrate-package --package-name=${info.name}`.split(
            " "
        ),
        {
            ...runCommandOptions,
            cwd: packageDirectory,
        }
    );

    logger.info(`Start to rush update after migration.`);
    await runCommand(`node`, ['common/scripts/install-run-rush.js', 'update'], runCommandOptions, false);
    logger.info(`Rush update successfully.`);

    logger.info(`Migrated package '${info.name}' successfully`);
}
