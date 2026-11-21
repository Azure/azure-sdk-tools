const fs = require("node:fs");
const path = require("node:path");

const source = process.env.AZSDK_EVAL_NODE_MODULES;
if (!source) {
    console.log("AZSDK_EVAL_NODE_MODULES is not set; using the workspace's own dependencies.");
} else {
    if (!path.isAbsolute(source) || !fs.statSync(source).isDirectory()) {
        throw new Error(`AZSDK_EVAL_NODE_MODULES must point to an existing absolute directory: ${source}`);
    }
    const target = path.resolve("node_modules");
    if (fs.existsSync(target)) {
        throw new Error(`Cannot link shared dependencies: ${target} already exists.`);
    }
    fs.symlinkSync(source, target, "junction");
}
