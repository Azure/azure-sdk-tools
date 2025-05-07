import { readFile, writeFile } from "fs/promises";
import pacote from "pacote";
import semver from "semver";
import { parseArgs } from "util";

const knownPackages = [
  "@typespec/compiler",
  "@typespec/http",
  "@typespec/openapi",
  "@typespec/rest",
  "@typespec/versioning",
  "@typespec/xml",
  "@typespec/events",
  "@typespec/sse",
  "@typespec/streams",
  "@typespec/http-specs",
  "@typespec/spector",
  "@typespec/spec-api",
  "@azure-tools/typespec-client-generator-core",
  "@azure-tools/typespec-azure-core",
  "@azure-tools/typespec-azure-resource-manager",
  "@azure-tools/typespec-azure-rulesets",
  "@azure-tools/typespec-autorest",
  "@azure-tools/azure-http-specs",
  "@typespec/eslint-config-typespec",
  "@typespec/library-linter",
];

const depTypes = ["dependencies", "devDependencies", "peerDependencies"];

export function getVersionRange(version: string): string {
  const parsed = semver.parse(version);
  if (!parsed) {
    throw new Error(`Unable to parse version ${version}`);
  }

  return `>=${parsed.major}.${parsed.minor}.0-0 <${parsed.major}.${parsed.minor}.0`;
}

async function getKnownPackageVersion(packageName: string): Promise<string> {
  return (await pacote.manifest(`${packageName}@next`)).version;
}

export async function main() {
  const args = process.argv.slice(2);
  const options = {
    "add-rush-overrides": {
      type: "boolean",
    },
    "add-npm-overrides": {
      type: "boolean",
    },
    "use-peer-ranges": {
      type: "boolean",
    },
  } as const;

  const { values, positionals } = parseArgs({ args, options, allowPositionals: true });

  const packageJsonPaths = positionals;
  const addRushOverrides = values["add-rush-overrides"] ?? false;
  const addNpmOverrides = values["add-npm-overrides"] ?? false;
  const usePeerRanges = values["use-peer-ranges"] ?? false;

  const packageToVersionRecord = Object.fromEntries(
    await Promise.all(knownPackages.map(async (x) => [x, await getKnownPackageVersion(x)])),
  );

  // eslint-disable-next-line no-console
  console.log("The following is a mapping between packages and the versions we will update them to");
  // eslint-disable-next-line no-console
  console.log(packageToVersionRecord);

  for (const packageJsonPath of packageJsonPaths) {
    const content = await readFile(packageJsonPath);
    const packageJson = JSON.parse(content.toString());

    updatePackageJson(packageJson, packageToVersionRecord, usePeerRanges, addNpmOverrides, addRushOverrides);

    // eslint-disable-next-line no-console
    console.log(`Updated ${packageJsonPath}`);
    await writeFile(packageJsonPath, JSON.stringify(packageJson, null, 2));
  }
}

export function updatePackageJson(
  packageJson: any,
  packageToVersionRecord: { [key: string]: string },
  usePeerRanges: boolean,
  addNpmOverrides: boolean,
  addRushOverrides: boolean,
) {
  const packageToVersionRange = Object.fromEntries(
    Object.entries(packageToVersionRecord).map(([key, value]) => [key, getVersionRange(value)]),
  );

  let overridesType: string | undefined = undefined;
  if (addNpmOverrides) {
    overridesType = "overrides";
  } else if (addRushOverrides) {
    overridesType = "globalOverrides";
  }

  for (const [packageName, version] of Object.entries(packageToVersionRecord)) {
    if (usePeerRanges) {
      const peerDependency = packageJson.peerDependencies ? packageJson.peerDependencies[packageName] : undefined;

      if (peerDependency) {
        if (packageJson.dependencies && packageJson.dependencies[packageName]) {
          throw new Error(`${packageName} is both a dependency and peerDependency`);
        }

        // we don't do range based peerDependencies if there's a dependencies entry for the same package
        if (!semver.satisfies(version, peerDependency)) {
          // if the new version doesn't satisfy the existing range, we append the new range
          // ">= 0.3.0 <1.0.0" becomes ">= 0.3.0 <1.0.0 || >= 0.4.0-dev <0.4.0"
          // "1.2.0" becomes "1.2.0 || >= 0.4.0-dev <0.4.0"
          packageJson.peerDependencies[packageName] += ` || ${packageToVersionRange[packageName]}`;
        }

        packageJson.devDependencies[packageName] = version;
        continue;
      }
    }

    for (const depType of depTypes) {
      const deps = packageJson[depType];

      if (deps && deps[packageName]) {
        deps[packageName] = version;
      }
    }

    // add/merge package versions into "overrides" or "globalOverrides"
    if (overridesType) {
      const deps = (packageJson[overridesType] ??= {});
      deps[packageName] = version;
    }
  }
}
