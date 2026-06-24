import Client from "smee-client";

if (!process.env.WEBHOOK_PROXY_URL) {
    throw new Error("WEBHOOK_PROXY_URL is not set");
}

const source = process.env.WEBHOOK_PROXY_URL;
const port = process.env.PORT || "3000";
const target = `http://localhost:${port}/api/webhook`;

const client = new Client({ source, target });
await client.start();
