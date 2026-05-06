import crypto from "node:crypto";

// ── GitHub App token minting via Azure Key Vault ──────────────
const KEYVAULT_NAME = process.env.KEYVAULT_NAME;
const KEYVAULT_KEY_NAME = process.env.KEYVAULT_KEY_NAME;
const GITHUB_APP_ID = process.env.GITHUB_APP_NUMERIC_ID;
const GITHUB_INSTALL_OWNER = process.env.GITHUB_INSTALL_OWNER;

let cachedGhToken = null;

function base64UrlEncode(buffer) {
  return (Buffer.isBuffer(buffer) ? buffer : Buffer.from(buffer)).toString("base64url");
}

/**
 * Mints a GitHub App installation token via Azure Key Vault JWT signing.
 * Falls back to GITHUB_PAT_RELEASE_PLAN or pre-set GH_TOKEN if minting fails.
 * @returns {Promise<string|null>} The minted token, or null on failure
 */
async function mintGitHubAppToken() {
  try {
    const { DefaultAzureCredential } = await import("@azure/identity");
    const { CryptographyClient, KeyClient } = await import("@azure/keyvault-keys");

    const credential = new DefaultAzureCredential();
    const vaultUrl = `https://${KEYVAULT_NAME}.vault.azure.net`;
    const keyClient = new KeyClient(vaultUrl, credential);
    const key = await keyClient.getKey(KEYVAULT_KEY_NAME);
    const cryptoClient = new CryptographyClient(key.id, credential);

    // Build JWT header & payload
    const header = JSON.stringify({ alg: "RS256", typ: "JWT" });
    const nowSec = Math.floor(Date.now() / 1000);
    const payload = JSON.stringify({ iat: nowSec - 10, exp: nowSec + 600, iss: GITHUB_APP_ID });
    const unsignedToken = `${base64UrlEncode(header)}.${base64UrlEncode(payload)}`;

    // Sign with Key Vault (RS256)
    const digest = crypto.createHash("sha256").update(unsignedToken, "ascii").digest();
    const signResult = await cryptoClient.sign("RS256", digest);
    if (!signResult.result) throw new Error("Key Vault sign returned no result.");
    const jwt = `${unsignedToken}.${base64UrlEncode(Buffer.from(signResult.result))}`;

    // Get installation ID for the owner
    const apiHeaders = {
      Authorization: `Bearer ${jwt}`,
      Accept: "application/vnd.github+json",
      "X-GitHub-Api-Version": "2022-11-28",
      "User-Agent": "release-plan-dashboard",
    };
    const instRes = await fetch("https://api.github.com/app/installations", { headers: apiHeaders });
    if (!instRes.ok) throw new Error(`GitHub installations API ${instRes.status}: ${await instRes.text()}`);
    const installations = await instRes.json();
    const match = installations.find(i => i.account.login.toLowerCase() === GITHUB_INSTALL_OWNER.toLowerCase());
    if (!match) throw new Error(`No GitHub App installation found for owner "${GITHUB_INSTALL_OWNER}".`);

    // Exchange JWT for installation access token
    const tokenRes = await fetch(`https://api.github.com/app/installations/${match.id}/access_tokens`, {
      method: "POST", headers: apiHeaders,
    });
    if (!tokenRes.ok) throw new Error(`GitHub token exchange ${tokenRes.status}: ${await tokenRes.text()}`);
    const tokenData = await tokenRes.json();
    if (!tokenData.token) throw new Error("GitHub token exchange returned no token.");

    cachedGhToken = tokenData.token;
    process.env.GH_TOKEN = cachedGhToken;
    console.log(`GitHub App installation token minted successfully (owner: ${GITHUB_INSTALL_OWNER}).`);
    return cachedGhToken;
  } catch (err) {
    console.warn(`GitHub App token minting failed: ${err.message}`);
    console.warn("Falling back to GITHUB_PAT_RELEASE_PLAN or pre-set GH_TOKEN.");
    return null;
  }
}

// ── Azure Easy Auth identity parsing ──────────────────────────

/**
 * Parses the Azure App Service Easy Auth X-MS-CLIENT-PRINCIPAL header.
 * Returns an object with user identity claims, or null if not authenticated.
 */
function parseEasyAuthPrincipal(req) {
  // In local dev, use mock identity from env vars (blocked in production)
  if (process.env.DEV_AUTH_USER) {
    if (process.env.NODE_ENV === "production") {
      console.error("FATAL: DEV_AUTH_USER is set in production. This is a security misconfiguration.");
      process.exit(1);
    }
    return {
      login: process.env.DEV_AUTH_USER,
      name: process.env.DEV_AUTH_NAME || process.env.DEV_AUTH_USER,
      objectId: process.env.DEV_AUTH_OBJECT_ID || "dev-object-id",
    };
  }

  const principalHeader = req.headers["x-ms-client-principal"];
  if (!principalHeader) return null;

  try {
    const decoded = JSON.parse(Buffer.from(principalHeader, "base64").toString("utf8"));
    const claims = decoded.claims || [];

    const findClaim = (...types) => {
      for (const typ of types) {
        const claim = claims.find(c => c.typ === typ);
        if (claim && claim.val) return claim.val;
      }
      return null;
    };

    const login = findClaim(
      "preferred_username",
      "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn",
      "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
    ) || req.headers["x-ms-client-principal-name"] || null;

    const name = findClaim(
      "name",
      "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name",
    ) || login;

    const objectId = findClaim(
      "http://schemas.microsoft.com/identity/claims/objectidentifier",
      "oid",
    ) || req.headers["x-ms-client-principal-id"] || null;

    if (!login) return null;

    return { login, name, objectId };
  } catch {
    return null;
  }
}

/** Escapes HTML special characters to prevent XSS in server-rendered content. */
function escapeHtml(str) {
  return String(str).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

export {
  mintGitHubAppToken,
  parseEasyAuthPrincipal,
  escapeHtml,
};
