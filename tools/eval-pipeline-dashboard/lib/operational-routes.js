export function mountOperationalRoutes(app, { service, provider }) {
  app.get("/api/readiness", async (context) => {
    context.header("Cache-Control", "no-store");
    try {
      await service.checkReady();
      return context.json({ status: "ready", storage: provider });
    } catch {
      return context.json({ status: "unavailable", storage: provider, errorCode: "storage_unavailable" }, 503);
    }
  });
  app.get("/api/dashboard/status", async (context) => {
    context.header("Cache-Control", "no-store");
    try { return context.json({ storage: provider, ...await service.status() }); }
    catch { return context.json({ storage: provider, errorCode: "status_unavailable" }, 503); }
  });
}