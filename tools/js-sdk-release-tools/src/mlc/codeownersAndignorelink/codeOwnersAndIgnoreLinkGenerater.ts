import { generateCodeOwnersAndIgnoreLinkForPackage } from "../../common/codeownersAndignorelink/generateCodeOwnersAndIgnoreLink.js";
import { logger } from "../../utils/logger.js";
import { getGeneratedPackageDirectory } from "../../common/utils.js";
import path from "path";
import { posix } from "node:path";

export const generateCodeOwnersAndIgnoreLink = async (options: {
    typespecProject?: string;
    typeSpecDirectory: string;
    sdkRepo: string;
    skipGeneration: boolean;
}): Promise<void> => {
    logger.info(
        `Generating CODEOWNERS and ignore link for modular package.`,
    );
    const typeSpecDirectory = path.posix.join(
        options.typeSpecDirectory,
        options.typespecProject!,
    );
    const packageDirectory = await getGeneratedPackageDirectory(
        typeSpecDirectory,
        options.sdkRepo.replaceAll("\\", "/"),
    );
    const relativePackageDirToSdkRoot = posix.relative(
        posix.normalize(options.sdkRepo.replaceAll("\\", "/")),
        posix.normalize(packageDirectory),
    );
    await generateCodeOwnersAndIgnoreLinkForPackage(
        relativePackageDirToSdkRoot,
    );
};
