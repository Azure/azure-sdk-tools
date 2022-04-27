import { DockerContext } from "../dockerCli";
import { initializeDockerTaskEngineContext, runTaskEngine, DockerTaskEngineContext } from "./DockerTaskEngine";
import * as fs from "fs";

export async function generateCodesInPipeline(dockerContext: DockerContext) {
    const context: DockerTaskEngineContext = initializeDockerTaskEngineContext(dockerContext);
    await runTaskEngine(context);
}