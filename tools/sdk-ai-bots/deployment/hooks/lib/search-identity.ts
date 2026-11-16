export function getSearchIndexerIdentityResourceId(env: NodeJS.ProcessEnv): string | undefined {
  let identities: unknown;
  try {
    identities = JSON.parse(env.SEARCH_USER_ASSIGNED_IDENTITIES?.trim() || "{}");
  } catch (error) {
    throw new Error("SEARCH_USER_ASSIGNED_IDENTITIES must be a valid JSON object.", { cause: error });
  }
  if (identities === null || typeof identities !== "object" || Array.isArray(identities)) {
    throw new Error("SEARCH_USER_ASSIGNED_IDENTITIES must be a JSON object.");
  }
  if (Object.values(identities).some(value =>
    value === null || typeof value !== "object" || Array.isArray(value))) {
    throw new Error("SEARCH_USER_ASSIGNED_IDENTITIES must map resource IDs to objects.");
  }

  const resourceIds = Object.keys(identities);
  if (resourceIds.length === 0) return undefined;

  const sharedIdentity = env.MANAGED_IDENTITY_RESOURCE_ID?.trim();
  if (!sharedIdentity) {
    throw new Error(
      "MANAGED_IDENTITY_RESOURCE_ID is required when Search user-assigned identities are configured.",
    );
  }
  const normalize = (resourceId: string) => resourceId.replace(/\/+$/, "").toLowerCase();
  return resourceIds.find(resourceId => normalize(resourceId) === normalize(sharedIdentity));
}
