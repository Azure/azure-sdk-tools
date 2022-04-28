import * as fs from "fs";

export function doNotExitDockerContainer() {
    // this file will be used by entrypoint.sh to determine whether exit the docker container.
    fs.writeFileSync('/tmp/notExit', 'yes', 'utf-8');
}