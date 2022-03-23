import * as fs from "fs";
import * as path from "path";

export function generateRollupConfig(packagePath) {
    const content = `import { makeConfig } from "@azure/dev-tool/shared-config/rollup";

export default makeConfig(require("./package.json"));`;
    fs.writeFileSync(path.join(packagePath, 'rollup.config.js'), content, {encoding: 'utf-8'});
}
