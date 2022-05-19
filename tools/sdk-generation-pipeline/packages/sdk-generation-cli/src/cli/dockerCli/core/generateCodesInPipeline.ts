import { DockerContext } from "./DockerContext";
import { DockerTaskEngineContext, initializeDockerTaskEngineContext, runTaskEngine } from "./dockerTaskEngine";

export async function generateCodesInPipeline(dockerContext: DockerContext) {
    const context: DockerTaskEngineContext = initializeDockerTaskEngineContext(dockerContext);
    await runTaskEngine(context);
}