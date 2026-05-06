import { describe, test, expect } from "vitest";

// Test auth module functions that don't require external network calls

// Set env vars before importing the module
process.env.KEYVAULT_NAME = "test-vault";
process.env.KEYVAULT_KEY_NAME = "test-key";
process.env.GITHUB_APP_NUMERIC_ID = "12345";
process.env.GITHUB_INSTALL_OWNER = "TestOrg";

import { escapeHtml, getBaseUrl } from "../lib/auth.js";

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

  describe("getBaseUrl", () => {
    test("returns http URL for localhost", () => {
      const req = { hostname: "localhost", get: (h) => h === "host" ? "localhost:3000" : "" };
      expect(getBaseUrl(req)).toBe("http://localhost:3000");
    });

    test("returns http URL for 127.0.0.1", () => {
      const req = { hostname: "127.0.0.1", get: (h) => h === "host" ? "127.0.0.1:3000" : "" };
      expect(getBaseUrl(req)).toBe("http://127.0.0.1:3000");
    });

    test("returns REDIRECT_URL env var for non-localhost when set", () => {
      const original = process.env.REDIRECT_URL;
      process.env.REDIRECT_URL = "https://custom.example.com";
      const req = { hostname: "myapp.azurewebsites.net", get: () => "myapp.azurewebsites.net" };
      expect(getBaseUrl(req)).toBe("https://custom.example.com");
      if (original) process.env.REDIRECT_URL = original;
      else delete process.env.REDIRECT_URL;
    });

    test("returns default Azure URL for non-localhost when REDIRECT_URL not set", () => {
      const original = process.env.REDIRECT_URL;
      delete process.env.REDIRECT_URL;
      const req = { hostname: "myapp.azurewebsites.net", get: () => "myapp.azurewebsites.net" };
      expect(getBaseUrl(req)).toBe("https://releaseplan-dashboard.azurewebsites.net");
      if (original) process.env.REDIRECT_URL = original;
    });
  });
});
