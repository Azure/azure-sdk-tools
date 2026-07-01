/**
 * Build the azure-sdk-qa-bot-function Docker image and optionally push it to ACR.
 *
 * Usage:
 *   npx tsx scripts/setup-docker-image.ts --tag <tag> --acr-name <acrName> [--image-name <name>] [--push]
 *
 * Examples:
 *   npx tsx scripts/setup-docker-image.ts --tag dev-0.0.1 --acr-name myacr
 *   npx tsx scripts/setup-docker-image.ts --tag dev-0.0.1 --acr-name myacr --push
 */

import { execSync } from "child_process";
import * as path from "path";

interface Args {
  tag: string;
  acrName: string;
  imageName: string;
  push: boolean;
}

function parseArgs(): Args {
  const argv = process.argv.slice(2);
  const args: Partial<Args> = { imageName: "azure-sdk-qa-bot-function", push: false };

  for (let i = 0; i < argv.length; i++) {
    switch (argv[i]) {
      case "--tag":
        args.tag = argv[++i];
        break;
      case "--acr-name":
        args.acrName = argv[++i];
        break;
      case "--image-name":
        args.imageName = argv[++i];
        break;
      case "--push":
        args.push = true;
        break;
      default:
        console.error(`Unknown argument: ${argv[i]}`);
        process.exit(1);
    }
  }

  if (!args.tag) {
    console.error("Error: --tag is required (e.g. --tag dev-0.0.1)");
    process.exit(1);
  }
  if (!args.acrName) {
    console.error("Error: --acr-name is required (e.g. --acr-name myacr)");
    process.exit(1);
  }

  return args as Args;
}

function run(cmd: string, cwd?: string): void {
  console.log(`\n> ${cmd}`);
  execSync(cmd, { stdio: "inherit", cwd });
}

function main(): void {
  const { tag, acrName, imageName, push } = parseArgs();
  const repoRoot = path.resolve(__dirname, "..");

  console.log("Running npm install...");
  run("npm install", repoRoot);

  console.log("Running npm run build...");
  run("npm run build", repoRoot);

  console.log("Building Docker image...");
  run(`docker build . -t ${imageName}:local`, repoRoot);

  if (push) {
    console.log("Push flag is enabled, logging into ACR and pushing image...");
    run(`az acr login --name ${acrName}`);
    run(`docker tag ${imageName}:local ${acrName}.azurecr.io/${imageName}:${tag}`);
    run(`docker push ${acrName}.azurecr.io/${imageName}:${tag}`);
    console.log(`\nImage successfully pushed to ${acrName}.azurecr.io/${imageName}:${tag}`);
  } else {
    console.log("\nPush flag is disabled, skipping ACR login and push operations.");
    console.log(`Local image built: ${imageName}:local`);
    console.log(`To push later, rerun with --push`);
  }
}

main();
