import { DockerContext } from "../userInterfaceCli";
import { initializeTaskEngineContext, runTaskEngine, TaskEngineContext } from "./TaskEngine";

export async function generateCodesInPipeline(dockerContext: DockerContext) {
    const context: TaskEngineContext = initializeTaskEngineContext(dockerContext);
    await runTaskEngine(context);
}