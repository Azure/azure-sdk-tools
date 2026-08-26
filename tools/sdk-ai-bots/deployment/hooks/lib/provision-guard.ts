export function enforceProvisionGuard(): void {
  const environmentName = process.env.AZURE_ENV_NAME ?? "";
  const runningInPipeline = !!process.env.TF_BUILD || !!process.env.GITHUB_ACTIONS;

  if (environmentName === "prod" && !runningInPipeline) {
    throw new Error(
      "Refusing to provision prod from a non-pipeline context. " +
        "Use the production provisioning pipeline.",
    );
  }
}