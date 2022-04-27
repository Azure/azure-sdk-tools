import { DockerContext } from "../dockerCli";
import fs from "fs";

export async function growUp(dockerContext: DockerContext) {
    dockerContext.logger.info(`Please use vscode to connect this container.`)
    fs.writeFileSync('/tmp/notExit', 'yes', 'utf-8');
}