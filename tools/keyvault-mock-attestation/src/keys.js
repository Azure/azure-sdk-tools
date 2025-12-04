const crypto = require("node:crypto");
const jose = require("node-jose");

/**
 * Initialize RSA key pair and create a JWK key store
 * @returns {Promise<{signingKeyId: string, privateKey: string, publicKey: string, keyStore: jose.JWK.KeyStore}>}
 */
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

module.exports = { initKeys };
