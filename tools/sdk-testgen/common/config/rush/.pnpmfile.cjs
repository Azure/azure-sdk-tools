"use strict";

/**
 * When using the PNPM package manager, you can use pnpmfile.js to workaround
 * dependencies that have mistakes in their package.json file.  (This feature is
 * functionally similar to Yarn's "resolutions".)
 *
 * For details, see the PNPM documentation:
 * https://pnpm.js.org/docs/en/hooks.html
 *
 * IMPORTANT: SINCE THIS FILE CONTAINS EXECUTABLE CODE, MODIFYING IT IS LIKELY TO INVALIDATE
 * ANY CACHED DEPENDENCY ANALYSIS.  After any modification to pnpmfile.js, it's recommended to run
 * "rush update --full" so that PNPM will recalculate all version selections.
 */
module.exports = {
  hooks: {
    readPackage,
  },
};

const fixups = {
  braces: {
    applyTo: ["micromatch"],
    with: "3.0.2",
  }
};

function readPackage(packageJson, context) {
  for (const dep of Object.keys(fixups)) {
    const to = fixups[dep];
    if (to.applyTo.includes(packageJson.name)) {
      context.log(
        `Fixed up dependencies for ${packageJson.name} => ${dep}:${to.with}`
      );
      packageJson.dependencies[dep] = to.with
    }
  }

  return packageJson;
}
