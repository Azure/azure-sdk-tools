import { DockerContext } from "../dockerCli";

export async function growUp(dockerContext: DockerContext) {
    dockerContext.logger.info(`Please use vscode to connect this container.`)
}