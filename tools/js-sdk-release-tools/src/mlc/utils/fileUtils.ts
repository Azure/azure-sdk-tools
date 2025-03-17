import { basename, resolve } from "path";
import { readdir } from "fs-extra";

export async function getApiLayerApiViewNamePathMap(
    packageRoot: string
): Promise<Map<string, string>> {
    const reviewFolder = resolve(packageRoot, "review");
    const entries = await readdir(reviewFolder);

    const apiViewNamePathMap = new Map<string, string>();
    for (const entry of entries) {
        if (/.+-api-.+\.md$/i.test(entry)) {
            const fileName = basename(entry);
            apiViewNamePathMap.set(fileName, resolve(reviewFolder, entry));
        }
    }
    return apiViewNamePathMap;
}
