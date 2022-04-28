import { doNotExitDockerContainer } from "./doNotExitDockerContainer";
import { DockerContext } from "./DockerContext";

export async function growUp(dockerContext: DockerContext) {
    dockerContext.logger.info(`Please use vscode to connect this container.`)
    doNotExitDockerContainer();
}