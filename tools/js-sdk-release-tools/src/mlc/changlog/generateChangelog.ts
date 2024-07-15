import { Changelog } from '../../changelog/changelogGenerator';
import { generateChangelogAndBumpVersion as generateChangelogAndBumpVersionBase } from '../../hlc/utils/automaticGenerateChangeLogAndBumpVersion';
import { logger } from '../../utils/logger';

// TODO: reuse HLC's changelog's generator for now, add api layer changelog generation later
// TODO: consider decouple version bump and changelog generation
// TODO: version bump should reuse version bumper in azure-sdk-for-js
// TODO: when there's no breaking changes and new features, generateChangelogAndBumpVersion return undefined, which causes output json contains empty changelog content. looks like it doesn't impact review flow. keep this logic for now.
export async function generateChangelogAndBumpVersion(packageDirectory: string): Promise<Changelog | undefined> {
    logger.logInfo(`Generating changelog in ${packageDirectory}`);
    const changelog = await generateChangelogAndBumpVersionBase(packageDirectory);
    return changelog;
}
