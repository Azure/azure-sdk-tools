const crypto = require("node:crypto");

/**
 * Base64url encode an object
 * @param {object} obj - The object to encode
 * @returns {string} Base64url encoded string
 */
function base64urlEncode(obj) {
  return Buffer.from(JSON.stringify(obj))
    .toString("base64")
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=/g, "");
}

/**
 * Create a JWT token without external JWT library
 * @param {object} payload - The JWT payload
 * @param {string} privateKey - The private key for signing
 * @param {object} header - The JWT header
 * @returns {string} The JWT token
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
