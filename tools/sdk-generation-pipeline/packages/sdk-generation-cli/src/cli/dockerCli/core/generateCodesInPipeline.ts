import { DockerTaskEngineContext, initializeDockerTaskEngineContext, runTaskEngine } from "./dockerTaskEngine";
import { DockerContext } from "./DockerContext";

export async function generateCodesInPipeline(dockerContext: DockerContext) {
    const context: DockerTaskEngineContext = initializeDockerTaskEngineContext(dockerContext);
    await runTaskEngine(context);
}