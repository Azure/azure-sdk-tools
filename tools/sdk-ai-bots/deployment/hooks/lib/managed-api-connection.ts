export function isManagedApiConnection(apiId: unknown, apiName: string): boolean {
  if (typeof apiId !== "string") return false;
  return apiId.toLowerCase().endsWith(`/managedapis/${apiName.toLowerCase()}`);
}