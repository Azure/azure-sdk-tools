import { describe, test, expect, vi, beforeEach, afterEach } from "vitest";

// vi.hoisted runs before vi.mock and imports — set env vars and create mock fns here
const { mockSign, mockGetKey } = vi.hoisted(() => {
  process.env.KEYVAULT_NAME = "test-vault";
  process.env.KEYVAULT_KEY_NAME = "test-key";
  process.env.GITHUB_APP_NUMERIC_ID = "12345";
  process.env.GITHUB_INSTALL_OWNER = "TestOrg";

  return {
    mockSign: vi.fn().mockResolvedValue({ result: new Uint8Array([1, 2, 3]) }),
    mockGetKey: vi.fn().mockResolvedValue({
      id: "https://test-vault.vault.azure.net/keys/test-key/version1",
    }),
  };
});

// Mock Azure SDK modules used by mintGitHubAppToken via dynamic import
vi.mock("@azure/identity", () => ({
  DefaultAzureCredential: vi.fn().mockImplementation(() => ({})),
}));
vi.mock("@azure/keyvault-keys", () => ({
  KeyClient: vi.fn().mockImplementation(() => ({
    getKey: mockGetKey,
  })),
  CryptographyClient: vi.fn().mockImplementation(() => ({
    sign: mockSign,
  })),
}));

import {
  escapeHtml,
  parseEasyAuthPrincipal,
  mintGitHubAppToken,
} from "../lib/auth.js";

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
      expect(escapeHtml('<a href="x&y">')).toBe(
        "&lt;a href=&quot;x&amp;y&quot;&gt;",
      );
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
      if (originalDevAuthObjectId)
        process.env.DEV_AUTH_OBJECT_ID = originalDevAuthObjectId;
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
          {
            typ: "http://schemas.microsoft.com/identity/claims/objectidentifier",
            val: "obj-123",
          },
        ],
      };
      const encoded = Buffer.from(JSON.stringify(principal)).toString("base64");
      const req = { headers: { "x-ms-client-principal": encoded } };
      const result = parseEasyAuthPrincipal(req);
      expect(result).toEqual({
        login: "user@microsoft.com",
        name: "Test User",
        objectId: "obj-123",
      });
    });

    test("falls back to UPN claim when preferred_username is missing", () => {
      const principal = {
        claims: [
          {
            typ: "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn",
            val: "upn@contoso.com",
          },
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
      const req = {
        headers: {
          "x-ms-client-principal": encoded,
          "x-ms-client-principal-name": "fallback@example.com",
        },
      };
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
      expect(result).toEqual({
        login: "dev@microsoft.com",
        name: "Dev User",
        objectId: "dev-obj-456",
      });
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
      const req = {
        headers: {
          "x-ms-client-principal": encoded,
          "x-ms-client-principal-name": "header@example.com",
        },
      };
      const result = parseEasyAuthPrincipal(req);
      expect(result.login).toBe("header@example.com");
    });

    test("handles missing claims property (falls back to empty array)", () => {
      const principal = { userId: "test-user" };
      const encoded = Buffer.from(JSON.stringify(principal)).toString("base64");
      const req = {
        headers: {
          "x-ms-client-principal": encoded,
          "x-ms-client-principal-name": "noclaims@example.com",
        },
      };
      const result = parseEasyAuthPrincipal(req);
      expect(result.login).toBe("noclaims@example.com");
    });

    test("DEV_AUTH_USER in production calls process.exit(1)", () => {
      const originalNodeEnv = process.env.NODE_ENV;
      const exitSpy = vi.spyOn(process, "exit").mockImplementation(() => {
        throw new Error("process.exit called");
      });
      const errorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
      try {
        process.env.DEV_AUTH_USER = "dev@microsoft.com";
        process.env.NODE_ENV = "production";
        const req = { headers: {} };
        expect(() => parseEasyAuthPrincipal(req)).toThrow(
          "process.exit called",
        );
        expect(exitSpy).toHaveBeenCalledWith(1);
        expect(errorSpy).toHaveBeenCalledWith(
          expect.stringContaining("DEV_AUTH_USER is set in production"),
        );
      } finally {
        exitSpy.mockRestore();
        errorSpy.mockRestore();
        if (originalNodeEnv) process.env.NODE_ENV = originalNodeEnv;
        else delete process.env.NODE_ENV;
      }
    });
  });

  describe("mintGitHubAppToken", () => {
    let fetchStub;

    beforeEach(() => {
      mockSign
        .mockReset()
        .mockResolvedValue({ result: new Uint8Array([1, 2, 3]) });
      mockGetKey.mockReset().mockResolvedValue({
        id: "https://test-vault.vault.azure.net/keys/test-key/v1",
      });

      fetchStub = vi.fn();
      vi.stubGlobal("fetch", fetchStub);
    });

    afterEach(() => {
      vi.unstubAllGlobals();
      delete process.env.GH_TOKEN;
    });

    test("successfully mints a token", async () => {
      // First call: installations list
      fetchStub.mockResolvedValueOnce({
        ok: true,
        json: () =>
          Promise.resolve([
            { id: 42, account: { login: "TestOrg" } },
            { id: 99, account: { login: "OtherOrg" } },
          ]),
      });
      // Second call: token exchange
      fetchStub.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve({ token: "ghs_minted_token_123" }),
      });

      const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
      const logSpy = vi.spyOn(console, "log").mockImplementation(() => {});
      try {
        const token = await mintGitHubAppToken();
        expect(token).toBe("ghs_minted_token_123");
        expect(process.env.GH_TOKEN).toBe("ghs_minted_token_123");
        expect(fetchStub).toHaveBeenCalledTimes(2);
        // Verify installations call
        expect(fetchStub.mock.calls[0][0]).toBe(
          "https://api.github.com/app/installations",
        );
        // Verify token exchange call with correct installation id
        expect(fetchStub.mock.calls[1][0]).toBe(
          "https://api.github.com/app/installations/42/access_tokens",
        );
        expect(fetchStub.mock.calls[1][1].method).toBe("POST");
      } finally {
        warnSpy.mockRestore();
        logSpy.mockRestore();
      }
    });

    test("returns null when Key Vault sign returns no result", async () => {
      mockSign.mockResolvedValueOnce({ result: null });

      const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
      try {
        const token = await mintGitHubAppToken();
        expect(token).toBeNull();
        expect(warnSpy).toHaveBeenCalledWith(
          expect.stringContaining("Key Vault sign returned no result"),
        );
      } finally {
        warnSpy.mockRestore();
      }
    });

    test("returns null when GitHub installations API fails", async () => {
      fetchStub.mockResolvedValueOnce({
        ok: false,
        status: 401,
        text: () => Promise.resolve("Bad credentials"),
      });

      const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
      try {
        const token = await mintGitHubAppToken();
        expect(token).toBeNull();
        expect(warnSpy).toHaveBeenCalledWith(
          expect.stringContaining("GitHub installations API 401"),
        );
      } finally {
        warnSpy.mockRestore();
      }
    });

    test("returns null when no matching installation for owner", async () => {
      fetchStub.mockResolvedValueOnce({
        ok: true,
        json: () =>
          Promise.resolve([{ id: 99, account: { login: "OtherOrg" } }]),
      });

      const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
      try {
        const token = await mintGitHubAppToken();
        expect(token).toBeNull();
        expect(warnSpy).toHaveBeenCalledWith(
          expect.stringContaining("No GitHub App installation found for owner"),
        );
      } finally {
        warnSpy.mockRestore();
      }
    });

    test("returns null when token exchange fails", async () => {
      fetchStub.mockResolvedValueOnce({
        ok: true,
        json: () =>
          Promise.resolve([{ id: 42, account: { login: "TestOrg" } }]),
      });
      fetchStub.mockResolvedValueOnce({
        ok: false,
        status: 403,
        text: () => Promise.resolve("Forbidden"),
      });

      const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
      try {
        const token = await mintGitHubAppToken();
        expect(token).toBeNull();
        expect(warnSpy).toHaveBeenCalledWith(
          expect.stringContaining("GitHub token exchange 403"),
        );
      } finally {
        warnSpy.mockRestore();
      }
    });

    test("returns null when token exchange returns no token", async () => {
      fetchStub.mockResolvedValueOnce({
        ok: true,
        json: () =>
          Promise.resolve([{ id: 42, account: { login: "TestOrg" } }]),
      });
      fetchStub.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve({}),
      });

      const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
      try {
        const token = await mintGitHubAppToken();
        expect(token).toBeNull();
        expect(warnSpy).toHaveBeenCalledWith(
          expect.stringContaining("GitHub token exchange returned no token"),
        );
      } finally {
        warnSpy.mockRestore();
      }
    });

    test("owner matching is case-insensitive", async () => {
      fetchStub.mockResolvedValueOnce({
        ok: true,
        json: () =>
          Promise.resolve([{ id: 77, account: { login: "testorg" } }]),
      });
      fetchStub.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve({ token: "ghs_case_test" }),
      });

      const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
      const logSpy = vi.spyOn(console, "log").mockImplementation(() => {});
      try {
        const token = await mintGitHubAppToken();
        expect(token).toBe("ghs_case_test");
      } finally {
        warnSpy.mockRestore();
        logSpy.mockRestore();
      }
    });
  });
});
