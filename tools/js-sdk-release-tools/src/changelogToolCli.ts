#!/usr/bin/env node

import {generateChangelogAndBumpVersion} from "./common/changlog/automaticGenerateChangeLogAndBumpVersion";
import {logger} from "./utils/logger";

const changelogToolCli = async (packageFolderPath: string | undefined) => {
    if (!packageFolderPath) {
        logger.error(`Invalid package path '${packageFolderPath}'.`);
    } else {
        await generateChangelogAndBumpVersion(packageFolderPath);
    }
};

const packageFolderPath = process.argv.pop();

changelogToolCli(packageFolderPath);
