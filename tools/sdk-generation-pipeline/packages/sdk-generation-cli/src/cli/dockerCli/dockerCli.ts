#!/usr/bin/env node
import { DockerCliConfig, dockerCliConfig } from "./schema/dockerCliConfig";
import { generateCodesInLocal } from "./core/generateCodesInLocal";
import { generateCodesInPipeline } from "./core/generateCodesInPipeline";
import { growUp } from "./core/growUp";
import { DockerContext } from "./core/DockerContext";

async function main() {
    const inputParams: DockerCliConfig = dockerCliConfig.getProperties();
    const context: DockerContext = new DockerContext();
    context.initialize(inputParams);

    switch (context.mode) {
        case "generateCodesInLocal":
            await generateCodesInLocal(context);
            break;
        case "growUp":
            await growUp(context);
            break;
        case "generateCodesInPipeline":
            await generateCodesInPipeline(context);
            break;
    }
}

main().catch(e => {
    console.error("\x1b[31m", e.toString());
    console.error("\x1b[31m", e.message);
    console.error("\x1b[31m", e.stack);
    process.exit(1);
})