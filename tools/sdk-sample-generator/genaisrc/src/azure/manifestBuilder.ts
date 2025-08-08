// manifestBuilder.ts
import { discoverResourcesInFile } from "./extractResources.ts";
import { clientToResource } from "./resourceMap.ts";

export interface ProvisionItem {
    armType: string;
    apiVersion: string;
    parameters: Record<string, any>;
}

export async function buildProvisionManifest(
    sampleFile: string,
): Promise<ProvisionItem[]> {
    const discovered = await discoverResourcesInFile(sampleFile);
    const manifest: ProvisionItem[] = [];

    for (const d of discovered) {
        const mappings = clientToResource[d.clientClass];
        for (const m of mappings) {
            // Build parameters—this is where you extract real names:
            // e.g. if args[0] === "process.env.AZURE_STORAGE_CONNECTION_STRING" you know you'll need
            //    – STORAGE_ACCOUNT_NAME
            //    – STORAGE_CONNECTION_STRING
            // etc. For simplicity, we’ll apply defaults and leave names parametric:
            manifest.push({
                armType: m.type,
                apiVersion: m.apiVersion,
                parameters: {
                    ...(m.defaults || {}),
                    // you could inspect d.args[] here to override defaults
                },
            });
        }
    }

    // Deduplicate identical resources
    return manifest.filter(
        (item, idx, arr) =>
            arr.findIndex(
                (other) =>
                    other.armType === item.armType &&
                    JSON.stringify(other.parameters) ===
                        JSON.stringify(item.parameters),
            ) === idx,
    );
}
