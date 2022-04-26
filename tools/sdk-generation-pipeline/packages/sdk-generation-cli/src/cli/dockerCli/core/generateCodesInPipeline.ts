import { DockerContext } from "../dockerCli";
import { initializeDockerTaskEngineContext, runTaskEngine, DockerTaskEngineContext } from "./DockerTaskEngine";

export async function generateCodesInPipeline(dockerContext: DockerContext) {
    const context: DockerTaskEngineContext = initializeDockerTaskEngineContext(dockerContext);
    await runTaskEngine(context);
    dockerContext.mockHostProcess.kill('SIGINT');
}