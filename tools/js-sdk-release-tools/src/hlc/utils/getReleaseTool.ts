import * as path from "path";

export function getReleaseTool() {
    const {name, version} = require(path.resolve(__dirname, '..', '..', '..', 'package.json'));
    return `${name}@${version}`;
}
