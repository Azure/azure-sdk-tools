import { getNextVersionTag } from "../hooks/lib/acr-tags";

const [registry, repository, environment] = process.argv.slice(2);

if (!registry || !repository || !environment) {
  console.error("Usage: resolve-image-version <registry> <repository> <environment>");
  process.exit(1);
}

process.stdout.write(getNextVersionTag(registry, repository, environment));