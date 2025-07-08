import { vi } from "vitest";

export function getRandomInt(max) {
    return Math.floor(Math.random() * max);
}

export const generateTestNpmView = (
    latestVersion?: string,
    betaVersion?: string,
    latestVersionDate?: string,
    betaVersionDate?: string,
) => {
    const tags: Record<string, string> = {};
    if (latestVersion) tags.latest = latestVersion;
    if (betaVersion) tags.beta = betaVersion;
    const npmView =
        !latestVersion && !betaVersion
            ? undefined
            : {
                  "dist-tags": tags,
                  time: {
                      [latestVersion ?? ""]: latestVersionDate ?? "",
                      [betaVersion ?? ""]: betaVersionDate ?? "",
                  },
              };
    return npmView;
};
