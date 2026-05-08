import { describe, test, expect, beforeEach, afterEach } from "vitest";

// Set env vars before importing the module
process.env.KEYVAULT_NAME = "test-vault";
process.env.KEYVAULT_KEY_NAME = "test-key";
process.env.GITHUB_APP_NUMERIC_ID = "12345";
process.env.GITHUB_INSTALL_OWNER = "TestOrg";

import { escapeHtml, parseEasyAuthPrincipal } from "../lib/auth.js";

describe("auth module", () => {
  describe("escapeHtml", () => {
    test("escapes ampersands", () => {
      expect(escapeHtml("foo & bar")).toBe("foo &amp; bar");
    });

    test("escapes less than", () => {
      expect(escapeHtml("<script>")).toBe("&lt;script&gt;");
    });

    test("escapes greater than", () => {
      expect(escapeHtml("a > b")).toBe("a &gt; b");
    });

    test("escapes double quotes", () => {
      expect(escapeHtml('say "hello"')).toBe("say &quot;hello&quot;");
    });

    test("escapes multiple characters together", () => {
      expect(escapeHtml('<a href="x&y">')).toBe("&lt;a href=&quot;x&amp;y&quot;&gt;");
    });

    test("handles empty string", () => {
      expect(escapeHtml("")).toBe("");
    });

    test("handles non-string input", () => {
      expect(escapeHtml(123)).toBe("123");
      expect(escapeHtml(null)).toBe("null");
      expect(escapeHtml(undefined)).toBe("undefined");
    });

    test("passes through safe strings unchanged", () => {
      expect(escapeHtml("hello world 123")).toBe("hello world 123");
    });
  });

  describe("parseEasyAuthPrincipal", () => {
    const originalDevAuthUser = process.env.DEV_AUTH_USER;
    const originalDevAuthName = process.env.DEV_AUTH_NAME;
    const originalDevAuthObjectId = process.env.DEV_AUTH_OBJECT_ID;

    beforeEach(() => {
      delete process.env.DEV_AUTH_USER;
      delete process.env.DEV_AUTH_NAME;
      delete process.env.DEV_AUTH_OBJECT_ID;
    });

    afterEach(() => {
      if (originalDevAuthUser) process.env.DEV_AUTH_USER = originalDevAuthUser;
      else delete process.env.DEV_AUTH_USER;
      if (originalDevAuthName) process.env.DEV_AUTH_NAME = originalDevAuthName;
      else delete process.env.DEV_AUTH_NAME;
      if (originalDevAuthObjectId) process.env.DEV_AUTH_OBJECT_ID = originalDevAuthObjectId;
      else delete process.env.DEV_AUTH_OBJECT_ID;
    });

    test("returns null when no headers present", () => {
      const req = { headers: {} };
      expect(parseEasyAuthPrincipal(req)).toBeNull();
    });

    test("parses X-MS-CLIENT-PRINCIPAL with preferred_username claim", () => {
      const principal = {
        claims: [
          { typ: "preferred_username", val: "user@microsoft.com" },
          { typ: "name", val: "Test User" },
          { typ: "http://schemas.microsoft.com/identity/claims/objectidentifier", val: "obj-123" },
        ],
      };
      const encoded = Buffer.from(JSON.stringify(principal)).toString("base64");
      const req = { headers: { "x-ms-client-principal": encoded } };
      const result = parseEasyAuthPrincipal(req);
      expect(result).toEqual({ login: "user@microsoft.com", name: "Test User", objectId: "obj-123" });
    });

    test("falls back to UPN claim when preferred_username is missing", () => {
      const principal = {
        claims: [
          { typ: "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn", val: "upn@contoso.com" },
          { typ: "name", val: "UPN User" },
        ],
      };
      const encoded = Buffer.from(JSON.stringify(principal)).toString("base64");
      const req = { headers: { "x-ms-client-principal": encoded } };
      const result = parseEasyAuthPrincipal(req);
      expect(result.login).toBe("upn@contoso.com");
      expect(result.name).toBe("UPN User");
    });

    test("falls back to X-MS-CLIENT-PRINCIPAL-NAME header", () => {
      const principal = { claims: [{ typ: "name", val: "Fallback User" }] };
      const encoded = Buffer.from(JSON.stringify(principal)).toString("base64");
      const req = { headers: { "x-ms-client-principal": encoded, "x-ms-client-principal-name": "fallback@example.com" } };
      const result = parseEasyAuthPrincipal(req);
      expect(result.login).toBe("fallback@example.com");
      expect(result.name).toBe("Fallback User");
    });

    test("returns null for invalid base64 header", () => {
      const req = { headers: { "x-ms-client-principal": "not-valid-json!!!" } };
      expect(parseEasyAuthPrincipal(req)).toBeNull();
    });

    test("returns null when no login can be determined", () => {
      const principal = { claims: [{ typ: "aud", val: "some-audience" }] };
      const encoded = Buffer.from(JSON.stringify(principal)).toString("base64");
      const req = { headers: { "x-ms-client-principal": encoded } };
      expect(parseEasyAuthPrincipal(req)).toBeNull();
    });

    test("uses DEV_AUTH_USER env var for local development", () => {
      process.env.DEV_AUTH_USER = "dev@microsoft.com";
      process.env.DEV_AUTH_NAME = "Dev User";
      process.env.DEV_AUTH_OBJECT_ID = "dev-obj-456";
      const req = { headers: {} };
      const result = parseEasyAuthPrincipal(req);
      expect(result).toEqual({ login: "dev@microsoft.com", name: "Dev User", objectId: "dev-obj-456" });
    });

    test("DEV_AUTH_USER defaults name to login when DEV_AUTH_NAME not set", () => {
      process.env.DEV_AUTH_USER = "dev@microsoft.com";
      const req = { headers: {} };
      const result = parseEasyAuthPrincipal(req);
      expect(result.name).toBe("dev@microsoft.com");
    });

    test("handles empty claims array", () => {
      const principal = { claims: [] };
      const encoded = Buffer.from(JSON.stringify(principal)).toString("base64");
      const req = { headers: { "x-ms-client-principal": encoded, "x-ms-client-principal-name": "header@example.com" } };
      const result = parseEasyAuthPrincipal(req);
      expect(result.login).toBe("header@example.com");
    });
  });
});
