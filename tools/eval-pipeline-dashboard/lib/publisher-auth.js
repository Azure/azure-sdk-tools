import { isIP } from "node:net";
import { createRemoteJWKSet, jwtVerify } from "jose";
import { IngestionError } from "./ingestion-error.js";

export function hostnameOf(value) {
  if (!value || /[\s\\/@?#]/.test(value) || value.endsWith(":")) return null;
  try {
    return new URL(`http://${value}`).hostname.replace(/^\[|\]$/g, "").replace(/\.$/, "").toLowerCase();
  } catch {
    return null;
  }
}

export function isLoopback(host) {
  return ["localhost", "127.0.0.1", "::1"].includes(host?.replace(/^\[|\]$/g, "").toLowerCase());
}

export function hostGuard(allowedHosts = []) {
  const allowed = new Set(["localhost", "127.0.0.1", "::1", ...allowedHosts.map(hostnameOf).filter(Boolean)]);
  return async (context, next) => {
    if (context.req.path === "/api/health") return next();
    const host = hostnameOf(context.req.header("host"));
    if (!host || (!allowed.has(host) && !isIP(host))) {
      return context.json({ error: { code: "forbidden_host", message: "Host is not allowed." } }, 403);
    }
    return next();
  };
}

export function createPublisherAuthorizer({
  anonymousLocal = false,
  bindHost = "127.0.0.1",
  tenantId,
  audience,
  clientIds = [],
  role = "Dashboard.Ingest",
  jwks,
} = {}) {
  if (anonymousLocal) {
    if (!isLoopback(bindHost)) throw new Error("Anonymous POC ingestion requires a loopback bind address.");
  } else if (!/^[a-f0-9-]{36}$/i.test(tenantId ?? "") || !audience || clientIds.length === 0) {
    throw new Error("Configure VALLY_INGEST_TENANT_ID, VALLY_INGEST_AUDIENCE and VALLY_INGEST_CLIENT_IDS, or explicitly enable the loopback POC.");
  }
  const issuer = `https://login.microsoftonline.com/${tenantId}/v2.0`;
  const keys = anonymousLocal ? null : jwks ?? createRemoteJWKSet(
    new URL(`https://login.microsoftonline.com/${tenantId}/discovery/v2.0/keys`)
  );
  return async (context) => {
    if (context.req.header("origin") || (context.req.header("sec-fetch-site") && context.req.header("sec-fetch-site") !== "none")) {
      throw new IngestionError(403, "browser_submission_forbidden", "Ingestion endpoints accept server-to-server requests only.");
    }
    if (anonymousLocal) {
      if (!isLoopback(hostnameOf(context.req.header("host")))) {
        throw new IngestionError(403, "local_only", "Anonymous ingestion is restricted to loopback.");
      }
      return { id: "local-poc" };
    }
    const token = context.req.header("authorization")?.match(/^Bearer (\S+)$/i)?.[1];
    if (!token) throw new IngestionError(401, "unauthorized", "A publisher access token is required.");
    let payload;
    try {
      ({ payload } = await jwtVerify(token, keys, {
        issuer,
        audience,
        algorithms: ["RS256"],
        requiredClaims: ["exp", "iss", "aud", "tid"],
      }));
    } catch {
      throw new IngestionError(401, "invalid_token", "The publisher access token is invalid or expired.");
    }
    const clientId = payload.azp ?? payload.appid;
    if (payload.tid !== tenantId || payload.scp !== undefined || !clientIds.includes(clientId) || !Array.isArray(payload.roles) || !payload.roles.includes(role)) {
      throw new IngestionError(403, "forbidden_publisher", "The application is not authorized to submit evaluations.");
    }
    return { id: clientId };
  };
}