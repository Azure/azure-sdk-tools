import { access } from "node:fs/promises";
import { resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { execFile, execNpm } from "../../eng/common/scripts/eval/lib/exec.ts";

const packageFiles = new Map<string, string[]>([
    ["azure-sdk-for-js", ["sdk", "widget", "arm-widget", "package.json"]],
    ["azure-sdk-for-net", ["sdk", "widget", "Azure.ResourceManager.Widget", "src", "Azure.ResourceManager.Widget.csproj"]],
    ["azure-sdk-for-python", ["sdk", "widget", "azure-mgmt-widget", "setup.py"]],
    ["azure-sdk-for-java", ["sdk", "widget", "azure-resourcemanager-widget", "pom.xml"]],
    ["azure-sdk-for-go", ["sdk", "resourcemanager", "widget", "armwidget", "go.mod"]],
]);

export async function generateSdk(
    sdkRepoName = "azure-sdk-for-js",
    commands = { execFile, execNpm },
): Promise<void> {
    const workspace = process.cwd();
    const packageFileParts = packageFiles.get(sdkRepoName);
    if (!packageFileParts) {
        throw new Error(`Unsupported SDK repository "${sdkRepoName}". Supported repositories: ${[...packageFiles.keys()].join(", ")}`);
    }

    const root = resolve(workspace);
    const sdkRepo = resolve(root, sdkRepoName);
    const tspConfig = resolve(
        root, "specification", "widget", "resource-manager",
        "Microsoft.Widget", "Widget", "tspconfig.yaml",
    );

    console.log(`Resolved tspConfig path: ${tspConfig}`);
    const azsdk = resolve(root, ".artifacts", "mcp", "cli", process.platform === "win32" ? "azsdk.exe" : "azsdk");

    await access(tspConfig);
    await access(azsdk);

    const options = { cwd: root, logger: { info: console.log, debug: console.log } };
    await commands.execFile("git", [
        "clone", "--depth", "1", "--filter=blob:none", "--sparse",
        `https://github.com/Azure/${sdkRepoName}.git`, sdkRepo,
    ], options);
    await commands.execFile("git", [
        "-C", sdkRepo, "sparse-checkout", "set",
        sdkRepoName === "azure-sdk-for-js" ? "eng/common/tsp-client" : "eng",
    ], options);
    await commands.execNpm(["ci"], {
        ...options,
        prefix: resolve(sdkRepo, "eng", "common", "tsp-client"),
    });
    await commands.execFile(azsdk, [
        "package", "generate",
        "--local-sdk-repo-path", sdkRepo,
        "--tsp-config-path", tspConfig,
        "--output", "json",
    ], options);

    const packageFile = resolve(sdkRepo, ...packageFileParts);
    try {
        await access(packageFile);
    } catch (error) {
        throw new Error(`SDK generation for ${sdkRepoName} did not create an accessible ${packageFile}`, { cause: error });
    }
    console.log(`Generated SDK for ${sdkRepoName}: ${packageFile}`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
    generateSdk(process.argv[2], process.argv[3]).catch((error: unknown) => {
        console.error(error);
        process.exitCode = 1;
    });
}