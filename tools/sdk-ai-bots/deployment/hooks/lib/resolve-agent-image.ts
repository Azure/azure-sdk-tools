export function resolveAgentTargetImage(
  env: NodeJS.ProcessEnv,
): string | undefined {
  const registry = env.AZURE_CONTAINER_REGISTRY_ENDPOINT
    ?.trim()
    .replace(/^https?:\/\//, "")
    .replace(/\/+$/, "");
  const environmentName = env.AZURE_ENV_NAME?.trim();
  const repository = env.AGENT_IMAGE_REPOSITORY?.trim();
  const imageTag = env.AZD_IMAGE_TAG?.trim();
  if (!registry || !environmentName || !repository || !imageTag) return undefined;

  return `${registry}/${repository}:${imageTag}`;
}