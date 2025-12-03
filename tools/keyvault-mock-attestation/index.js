const http = require("node:http");
const crypto = require("node:crypto");
const jose = require("node-jose");

const PORT = process.env.PORT || 5000;

const baseUrl = (req) => {
  const host = req.headers.host || `localhost:${PORT}`;
  return `https://${host}`;
};

// Helper function to send JSON response
function sendJSON(res, statusCode, data) {
  res.writeHead(statusCode, { "Content-Type": "application/json" });
  res.end(JSON.stringify(data));
}

// Helper function to create JWT without jsonwebtoken library
function createJWT(payload, privateKey, header) {
  const base64urlEncode = (obj) => {
    return Buffer.from(JSON.stringify(obj))
      .toString("base64")
      .replace(/\+/g, "-")
      .replace(/\//g, "_")
      .replace(/=/g, "");
  };

  const encodedHeader = base64urlEncode(header);
  const encodedPayload = base64urlEncode(payload);
  const signatureInput = `${encodedHeader}.${encodedPayload}`;

  const signature = crypto
    .sign("RSA-SHA256", Buffer.from(signatureInput), privateKey)
    .toString("base64")
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=/g, "");

  return `${signatureInput}.${signature}`;
}

async function initKeys() {
  const { publicKey, privateKey } = crypto.generateKeyPairSync("rsa", {
    modulusLength: 2048,
    publicKeyEncoding: { type: "spki", format: "pem" },
    privateKeyEncoding: { type: "pkcs8", format: "pem" },
  });
  const keystore = jose.JWK.createKeyStore();

  const key = await keystore.add(publicKey, "pem");
  return {
    signingKeyId: key.kid,
    privateKey,
    publicKey,
    keyStore: keystore,
  };
}

function createServer({ keyStore, privateKey, signingKeyId }) {
  return http.createServer(async (req, res) => {
    // Route: /.well-known/openid-configuration
    if (
      req.url?.startsWith("/.well-known/openid-configuration") &&
      req.method === "GET"
    ) {
      sendJSON(res, 200, {
        jwks_uri: `${baseUrl(req)}/keys`,
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
          iss: `${baseUrl(req)}/`,
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
          jku: `${baseUrl(req)}/keys`,
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

async function main() {
  const { keyStore, privateKey, signingKeyId } = await initKeys();
  const server = createServer({ keyStore, privateKey, signingKeyId });

  await new Promise((resolve, reject) => {
    server.listen(PORT, (err) => {
      if (err) {
        reject(err);
      }

      console.log(`Server listening on port ${PORT}`);
      resolve();
    });
  });
}

main().catch(console.error);
