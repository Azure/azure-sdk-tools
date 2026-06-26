import { createServer } from "node:http";

import { createRouter } from "./routes/router.js";

const port = Number(process.env.PORT ?? 8080);
const router = createRouter();

const server = createServer((request, response) => {
    router.handle(request, response).catch((error: unknown) => {
        const message = error instanceof Error ? error.message : String(error);
        console.error(`Unhandled API Review Hub request failure: ${message}`);
        response.writeHead(500, { "content-type": "application/json" });
        response.end(JSON.stringify({ error: { code: "internalServerError", message: "The service could not complete the request." } }));
    });
});

server.listen(port, () => {
    console.log(`API Review Hub listening on port ${port}`);
});