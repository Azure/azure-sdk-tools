#!/usr/bin/env node

import {generateChangelogAndBumpVersion} from "./common/changelog/automaticGenerateChangeLogAndBumpVersion.js";
import {logger} from "./utils/logger.js";

const changelogToolCli = async (packageFolderPath: string | undefined) => {
    if (!packageFolderPath) {
        logger.error(`Invalid package path '${packageFolderPath}'.`);
    } else {
        await generateChangelogAndBumpVersion(packageFolderPath);
    }
};

const packageFolderPath = process.argv.pop();

changelogToolCli(packageFolderPath);
