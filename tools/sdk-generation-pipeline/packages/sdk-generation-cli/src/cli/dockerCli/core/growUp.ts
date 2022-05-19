import { DockerContext } from "./DockerContext";
import { doNotExitDockerContainer } from "./doNotExitDockerContainer";

export async function growUp(dockerContext: DockerContext) {
    dockerContext.logger.info(`Please use vscode to connect this container.`)
    doNotExitDockerContainer();
}