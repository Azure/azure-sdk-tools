import { DockerContext } from "../dockerCli";
import { initializeTaskEngineContext, runTaskEngine, TaskEngineContext } from "./DockerTaskEngine";

export async function generateCodesInPipeline(dockerContext: DockerContext) {
    const context: TaskEngineContext = initializeTaskEngineContext(dockerContext);
    await runTaskEngine(context);
}