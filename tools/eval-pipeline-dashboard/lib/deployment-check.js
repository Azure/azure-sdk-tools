export function validateDeploymentTarget(site, authentication, endpoints) {
  if (site.properties?.publicNetworkAccess !== "Disabled") throw new Error("Deployment refused: App Service public network access must be explicitly Disabled.");
  if (site.properties?.httpsOnly !== true) throw new Error("Deployment refused: App Service HTTPS-only must be enabled.");
  if (authentication.properties?.platform?.enabled !== false) throw new Error("Deployment refused: viewer Easy Auth must be explicitly disabled for the VPN-only no-login policy.");
  if (!endpoints.value?.some((endpoint) => endpoint.properties?.privateLinkServiceConnectionState?.status === "Approved")) {
    throw new Error("Deployment refused: an approved App Service private endpoint is required.");
  }
  if (!site.identity?.type || site.identity.type === "None") throw new Error("Deployment refused: configure the dashboard managed identity first.");
  return { name: site.name, publicNetworkAccess: "Disabled", viewerLogin: false, privateEndpointApproved: true };
}

export async function readDeploymentTarget({ subscription, resourceGroup, app, token, fetchImpl = fetch }) {
  const resource = `/subscriptions/${encodeURIComponent(subscription)}/resourceGroups/${encodeURIComponent(resourceGroup)}/providers/Microsoft.Web/sites/${encodeURIComponent(app)}`;
  async function read(suffix = "", method = "GET") {
    const response = await fetchImpl(`https://management.azure.com${resource}${suffix}?api-version=2024-11-01`, {
      method, headers: { authorization: `Bearer ${token}` }, signal: AbortSignal.timeout(30_000),
    });
    if (!response.ok) throw new Error(`Cannot verify deployment target (${response.status}); no deployment should proceed.`);
    return response.json();
  }
  // ARM uses GET for authsettingsV2/list, but POST for appsettings/list.
  // Do not log the settings response: it can contain unrelated application secrets.
  const [site, authentication, endpoints, settings] = await Promise.all([
    read(), read("/config/authsettingsV2/list"), read("/privateEndpointConnections"), read("/config/appsettings/list", "POST"),
  ]);
  return { site, authentication, endpoints, settings };
}