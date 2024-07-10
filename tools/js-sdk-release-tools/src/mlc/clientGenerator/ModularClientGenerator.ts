import { readFile } from "node:fs/promises";
import { parse } from "yaml";
import { join } from "path";
import { execSync, ExecSyncOptions } from "child_process";
import {
    GeneratedPackageInfo,
    GenerationOutputInfo,
    ModularClientGenerationOptions,
    ChangelogInfo,
} from "../../common/types";
import { Changelog } from "../../changelog/changelogGenerator";

// TODO
// TODO: do we need entire options: ModularClientGenerationOptions?
function generateOutputInfo(changelog: Changelog, options: ModularClientGenerationOptions): GenerationOutputInfo {
    const changelogInfo: ChangelogInfo = {
        content: changelog.displayChangeLog(),
        hasBreakingChange: changelog.hasBreakingChange,
    };
    const packageInfo: GeneratedPackageInfo = {
        packageName: ,
        version: ,
        path: [],
        changelog: changelogInfo,
        artifacts: [],
        result: "failed",
    };
    const outputInfo: GenerationOutputInfo = { packages: [packageInfo] };
    return outputInfo;
}

async function generateClientFromTypeSpec(
    options: ModularClientGenerationOptions
) {
    const configPath = join(options.tspProjectPath, "tspconfig.yaml");
    const content = await readFile(configPath);
    validateTspConfig(content.toString());
    const command = `pwsh ./eng/common/scripts/TypeSpec-Project-Process.ps1 ${options.tspProjectPath} ${options.gitCommitId} ${options.swaggerRepoUrl}`;
    const execOptions: ExecSyncOptions = { stdio: "inherit" };
    execSync(command, execOptions);
}

// TODO: consider modifying tspconfig like rlc?
function validateTspConfig(configContent: string) {
    const config = parse(configContent);
    if (!Array.isArray(config.emit) || config.emit.length !== 1) {
        throw new Error(`Invalid emit config: ${config.emit}`);
    }
    const emitter = config.emit[0];
    if (emitter !== "@azure-tools/typespec-ts") {
        throw new Error(`Unsupported emitter for Modular client: ${emitter}`);
    }

    if (!config.options || !config.options["@azure-tools/typespec-ts"]) {
        throw new Error(
            `Failed to find options for "@azure-tools/typespec-ts"`
        );
    }
    const emitterOptions = config.options["@azure-tools/typespec-ts"];
    const isModularLibrary =
        emitterOptions.isModularLibrary === true ? true : false;
    // TODO
}

// TODO
function generateChangelog(): Changelog {

}

// TODO: add log
export async function generate(options: ModularClientGenerationOptions) {
    if (!options.skipGeneration) {
        await generateClientFromTypeSpec(options);
    }

    const changelog = generateChangelog();
    
    const outputInfo = generateOutputInfo(changelog, options);
}
