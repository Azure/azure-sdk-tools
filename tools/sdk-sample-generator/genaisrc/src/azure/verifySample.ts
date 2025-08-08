import { buildProvisionManifest } from "./manifestBuilder.ts";
import * as fs from "node:fs/promises";
import * as path from "node:path";
import { promisify } from "node:util";
import child_process from "node:child_process";
import dotenv from "dotenv";

const exec = promisify(child_process.exec);

export async function verifySample(sampleName: string, sampleFile: string) {
    const manifest = await buildProvisionManifest(sampleFile);

    // Generate a Bicep template file
    const lines = [`param location string = 'eastus'`, ``];
    manifest.forEach((item, i) => {
        const resName = `res${i}`;
        lines.push(`
resource ${resName} '${item.armType}@${item.apiVersion}' = {
  name: '${sampleName}-${i}'
  location: location
  properties: ${JSON.stringify(item.parameters, null, 2)}
}`);
    });
    lines.push(
        `\noutput manifest array = ${JSON.stringify(manifest, null, 2)}`,
    );

    const bicepPath = path.join(process.cwd(), "temp-template.bicep");
    await fs.writeFile(bicepPath, lines.join("\n"));

    // 1) Create RG
    const rg = `rg-${sampleName}-${Date.now()}`;
    await exec(`az group create -n ${rg} -l eastus`);

    // 2) Deploy
    const { stdout, stderr } = await exec(
        `az deployment group create -g ${rg} --template-file ${bicepPath} -o json`,
    );
    if (stderr) {
        console.error("Error during deployment:", stderr);
        throw new Error(`Deployment failed: ${stderr}`);
    }
    const deploy = JSON.parse(stdout);
    const outputs = deploy.properties.outputs.manifest.value as any[];

    // 3) From outputs build `.env` (this is where you map ARM output → env vars)
    const envLines = outputs.map(
        (res, i) =>
            // e.g. STORAGE_ACCOUNT_NAME=res0.name
            `RESOURCE_${i}_TYPE=${res.armType}`,
    );
    await fs.writeFile(".env", envLines.join("\n"));

    // 4) Run the sample
    await exec(`node ${sampleFile}`, {
        env: { ...process.env, ...dotenv.parse(await fs.readFile(".env")) },
    });

    // 5) Tear down
    await exec(`az group delete -n ${rg} --yes --no-wait`);
}
