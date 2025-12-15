const crypto = require("node:crypto");

function base64urlEncode(obj) {
  return Buffer.from(JSON.stringify(obj))
    .toString("base64")
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=/g, "");
}

/**
 * Create a JWT token for tests
 * @param {object} payload - The JWT payload
 * @param {string} privateKey - The private key for signing
 * @param {object} header - The JWT header
 * @returns {string} The signed JWT token
 */
function createJWT(payload, privateKey, header) {
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

module.exports = { createJWT };
