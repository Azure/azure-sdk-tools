import { join } from "node:path";

// Pinned Azurite implementation, used only in tests. Start only Blob and Queue,
// with explicit IPC readiness instead of parsing CLI stdout or starting telemetry/Table.
const root = process.argv[2];
const servers = [];
async function load(name) {
  const module = await import(`azurite/dist/src/${name}.js`);
  return module.default.default ?? module.default;
}
try {
  process.send({ phase: "loading" });
  for (const [kind, name] of [["blob", "Blob"], ["queue", "Queue"]]) {
    const [Configuration, Server] = await Promise.all([load(`${kind}/${name}Configuration`), load(`${kind}/${name}Server`)]);
    const config = new Configuration("127.0.0.1", 0, 1, join(root, `${kind}.json`), join(root, `${kind}-extent.json`),
      [{ locationId: "Default", locationPath: join(root, `${kind}-data`), maxConcurrency: 4 }],
      false, undefined, false, undefined, false, true);
    const server = new Server(config);
    await server.start(); servers.push(server);
  }
  process.send({ ready: true, blob: servers[0].getHttpServerAddress(), queue: servers[1].getHttpServerAddress() });
  let closing;
  function close() {
    closing ??= Promise.all(servers.map((server) => server.close())).finally(() => process.disconnect());
    return closing;
  }
  process.on("message", (message) => { if (message === "shutdown") void close(); });
  process.once("SIGTERM", () => void close());
} catch (error) {
  process.send({ error: error.message });
  await Promise.allSettled(servers.map((server) => server.close()));
  process.exitCode = 1;
  process.disconnect();
}