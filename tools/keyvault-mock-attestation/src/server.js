const http = require("node:http");
const jose = require("node-jose");
const { createJWT } = require("./jwt");

/**
 * Get the base URL from the request
 * @param {http.IncomingMessage} req - The HTTP request
 * @param {number} port - The server port
 * @returns {string} The base URL
 */
const baseUrl = (req, port) => {
  const host = req.headers.host || `localhost:${port}`;
  return `https://${host}`;
};

/**
 * Send a JSON response
 * @param {http.ServerResponse} res - The HTTP response
 * @param {number} statusCode - The HTTP status code
 * @param {object} data - The data to send as JSON
 */
function sendJSON(res, statusCode, data) {
  res.writeHead(statusCode, { "Content-Type": "application/json" });
  res.end(JSON.stringify(data));
}

/**
 * Create an HTTP server with attestation endpoints
 * @param {object} config - Server configuration
 * @param {jose.JWK.KeyStore} config.keyStore - The JWK key store
 * @param {string} config.privateKey - The private key for signing
 * @param {string} config.signingKeyId - The signing key ID
 * @param {number} config.port - The server port
 * @returns {http.Server} The HTTP server
 */
function createServer({ keyStore, privateKey, signingKeyId, port }) {
  return http.createServer(async (req, res) => {
    // Route: /.well-known/openid-configuration
    if (
      req.url?.startsWith("/.well-known/openid-configuration") &&
      req.method === "GET"
    ) {
      sendJSON(res, 200, {
        jwks_uri: `${baseUrl(req, port)}/keys`,
      });
      return;
    }

    // Route: /keys
    if (req.url?.startsWith("/keys") && req.method === "GET") {
      sendJSON(res, 200, keyStore.toJSON(false));
      return;
    }

    // Route: /generate-test-token
    if (req.url?.startsWith("/generate-test-token") && req.method === "GET") {
      try {
        // create a fake release key to wrap a token with.
        const releaseKey = await jose.JWK.createKey("RSA", 2048, {
          use: "enc",
          kid: "fake-release-key",
        });

        const now = Math.floor(Date.now() / 1000);
        const exp = now + 7 * 24 * 60 * 60; // 7 days in seconds

        // sdk-test will be the claim used for tests.
        const tokenData = {
          iss: `${baseUrl(req, port)}/`,
          iat: now,
          exp: exp,
          "sdk-test": true,
          "x-ms-inittime": {},
          "x-ms-runtime": {
            keys: [releaseKey.toJSON(false)],
          },
        };

        const token = createJWT(tokenData, privateKey, {
          alg: "RS256",
          typ: "JWT",
          jku: `${baseUrl(req, port)}/keys`,
          kid: signingKeyId,
        });

        sendJSON(res, 200, { token });
      } catch (error) {
        console.error("Error generating token:", error);
        sendJSON(res, 500, { error: "Internal server error" });
      }
      return;
    }

    // 404 - Not Found
    sendJSON(res, 404, { error: "Not found" });
  });
}

module.exports = { createServer };
