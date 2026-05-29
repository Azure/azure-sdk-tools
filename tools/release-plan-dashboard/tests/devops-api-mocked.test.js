import { describe, test, expect, vi, afterEach } from "vitest";

process.env.KEYVAULT_NAME = "test-vault";
process.env.KEYVAULT_KEY_NAME = "test-key";
process.env.GITHUB_APP_NUMERIC_ID = "12345";
process.env.GITHUB_INSTALL_OWNER = "TestOrg";

vi.mock("@azure/identity", () => ({
  DefaultAzureCredential: vi.fn().mockImplementation(() => ({
    getToken: vi.fn().mockResolvedValue({ token: "mock-bearer-token" }),
  })),
}));

import {
  devopsRequest,
  runWiql,
  fetchWorkItemsBatch,
  fetchPackageWorkItems,
  fetchAzureSdkPackageList,
  getAuthHeader,
} from "../lib/devops-api.js";

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("getAuthHeader", () => {
  test("returns Bearer token from DefaultAzureCredential", async () => {
    const header = await getAuthHeader();
    expect(header).toBe("Bearer mock-bearer-token");
  });
});

describe("devopsRequest", () => {
  test("GET request returns parsed JSON", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        text: () => Promise.resolve(JSON.stringify({ count: 5 })),
        headers: new Headers(),
      }),
    );
    const result = await devopsRequest("https://dev.azure.com/test", "GET");
    expect(result).toEqual({ count: 5 });
  });

  test("defaults to GET when method is not provided", async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      text: () => Promise.resolve(JSON.stringify({ items: [] })),
      headers: new Headers(),
    });
    vi.stubGlobal("fetch", mockFetch);
    const result = await devopsRequest("https://dev.azure.com/test");
    expect(result).toEqual({ items: [] });
    expect(mockFetch.mock.calls[0][1].method).toBe("GET");
  });

  test("POST with body sends JSON", async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      text: () => Promise.resolve(JSON.stringify({ success: true })),
      headers: new Headers(),
    });
    vi.stubGlobal("fetch", mockFetch);

    const body = { query: "SELECT [System.Id] FROM WorkItems" };
    const result = await devopsRequest(
      "https://dev.azure.com/test",
      "POST",
      body,
    );
    expect(result).toEqual({ success: true });
    expect(mockFetch).toHaveBeenCalledTimes(1);
    const callArgs = mockFetch.mock.calls[0];
    expect(JSON.parse(callArgs[1].body)).toEqual(body);
  });

  test("non-200 response throws error", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 404,
        text: () => Promise.resolve("Not Found"),
        headers: new Headers(),
      }),
    );
    await expect(
      devopsRequest("https://dev.azure.com/test", "GET"),
    ).rejects.toThrow("DevOps 404");
  });

  test("response that isn't valid JSON returns text", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        text: () => Promise.resolve("plain text response"),
        headers: new Headers(),
      }),
    );
    const result = await devopsRequest("https://dev.azure.com/test", "GET");
    expect(result).toBe("plain text response");
  });

  test("options.returnHeaders returns { body, headers }", async () => {
    const headers = new Headers({ "x-request-id": "abc123" });
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        text: () => Promise.resolve(JSON.stringify({ data: "value" })),
        headers,
      }),
    );
    const result = await devopsRequest(
      "https://dev.azure.com/test",
      "GET",
      null,
      { returnHeaders: true },
    );
    expect(result.body).toEqual({ data: "value" });
    expect(result.headers).toHaveProperty("x-request-id", "abc123");
  });
});

describe("runWiql", () => {
  test("returns work item IDs from WIQL response", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        text: () =>
          Promise.resolve(
            JSON.stringify({
              workItems: [{ id: 1 }, { id: 2 }, { id: 3 }],
            }),
          ),
        headers: new Headers(),
      }),
    );
    const ids = await runWiql("SELECT [System.Id] FROM WorkItems");
    expect(ids).toEqual([1, 2, 3]);
  });

  test("returns empty array when no workItems in response", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        text: () => Promise.resolve(JSON.stringify({})),
        headers: new Headers(),
      }),
    );
    const ids = await runWiql("SELECT [System.Id] FROM WorkItems");
    expect(ids).toEqual([]);
  });
});

describe("fetchWorkItemsBatch", () => {
  test("empty ids returns empty array", async () => {
    const result = await fetchWorkItemsBatch([]);
    expect(result).toEqual([]);
  });

  test("fetches work items with field selection", async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      text: () =>
        Promise.resolve(
          JSON.stringify({
            value: [
              { id: 1, fields: { "System.Title": "Item 1" } },
              { id: 2, fields: { "System.Title": "Item 2" } },
            ],
          }),
        ),
      headers: new Headers(),
    });
    vi.stubGlobal("fetch", mockFetch);

    const result = await fetchWorkItemsBatch(
      [1, 2],
      ["System.Title", "System.State"],
    );
    expect(result).toHaveLength(2);
    expect(result[0].id).toBe(1);
    // Should have fields param in URL, not $expand
    const calledUrl = mockFetch.mock.calls[0][0];
    expect(calledUrl).toContain("fields=");
    expect(calledUrl).not.toContain("$expand");
  });

  test("fetches work items with expand=All when no fields param", async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      text: () =>
        Promise.resolve(
          JSON.stringify({
            value: [{ id: 1, fields: {}, relations: [] }],
          }),
        ),
      headers: new Headers(),
    });
    vi.stubGlobal("fetch", mockFetch);

    const result = await fetchWorkItemsBatch([1]);
    expect(result).toHaveLength(1);
    const calledUrl = mockFetch.mock.calls[0][0];
    expect(calledUrl).toContain("$expand=All");
    expect(calledUrl).not.toContain("fields=");
  });

  test("batches in groups of 200", async () => {
    const ids = Array.from({ length: 450 }, (_, i) => i + 1);
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      text: () => Promise.resolve(JSON.stringify({ value: [{ id: 1 }] })),
      headers: new Headers(),
    });
    vi.stubGlobal("fetch", mockFetch);

    const result = await fetchWorkItemsBatch(ids);
    // 450 items should be 3 batches: 200, 200, 50
    // Each batch triggers 1 fetch call from devopsRequest (+ 1 for getAuthHeader internally)
    // devopsRequest calls fetch once per batch
    expect(mockFetch.mock.calls.length).toBe(3);
    expect(result).toHaveLength(3); // 1 item per batch response
  });
});

describe("fetchPackageWorkItems", () => {
  test("empty input returns empty Map", async () => {
    const result = await fetchPackageWorkItems([]);
    expect(result).toBeInstanceOf(Map);
    expect(result.size).toBe(0);
  });

  test("fetches and maps package data correctly", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation((url) => {
        // WIQL query response
        if (url.includes("wiql")) {
          return Promise.resolve({
            ok: true,
            status: 200,
            text: () =>
              Promise.resolve(JSON.stringify({ workItems: [{ id: 100 }] })),
            headers: new Headers(),
          });
        }
        // Work items batch response
        return Promise.resolve({
          ok: true,
          status: 200,
          text: () =>
            Promise.resolve(
              JSON.stringify({
                value: [
                  {
                    id: 100,
                    fields: {
                      "Custom.Package": "azure-core",
                      "Custom.Language": "Python",
                      "Custom.PackageVersion": "1.2.0",
                      "Custom.APIReviewStatus": "Approved",
                      "Custom.PackageNameApprovalStatus": "Approved",
                      "System.ChangedDate": "2024-06-01T00:00:00Z",
                    },
                  },
                ],
              }),
            ),
          headers: new Headers(),
        });
      }),
    );

    const result = await fetchPackageWorkItems([
      { pkg: "azure-core", lang: "Python" },
    ]);
    expect(result).toBeInstanceOf(Map);
    expect(result.has("azure-core|Python")).toBe(true);
    const entry = result.get("azure-core|Python");
    expect(entry.version).toBe("1.2.0");
    expect(entry.apiReviewStatus).toBe("Approved");
    expect(entry.namespaceApproval).toBe("Approved");
  });

  test("keeps most recent by changedDate when duplicates", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation((url) => {
        if (url.includes("wiql")) {
          return Promise.resolve({
            ok: true,
            status: 200,
            text: () =>
              Promise.resolve(
                JSON.stringify({ workItems: [{ id: 100 }, { id: 101 }] }),
              ),
            headers: new Headers(),
          });
        }
        return Promise.resolve({
          ok: true,
          status: 200,
          text: () =>
            Promise.resolve(
              JSON.stringify({
                value: [
                  {
                    id: 100,
                    fields: {
                      "Custom.Package": "azure-core",
                      "Custom.Language": "Python",
                      "Custom.PackageVersion": "1.0.0",
                      "System.ChangedDate": "2024-01-01T00:00:00Z",
                    },
                  },
                  {
                    id: 101,
                    fields: {
                      "Custom.Package": "azure-core",
                      "Custom.Language": "Python",
                      "Custom.PackageVersion": "2.0.0",
                      "System.ChangedDate": "2024-06-01T00:00:00Z",
                    },
                  },
                ],
              }),
            ),
          headers: new Headers(),
        });
      }),
    );

    const result = await fetchPackageWorkItems([
      { pkg: "azure-core", lang: "Python" },
    ]);
    const entry = result.get("azure-core|Python");
    expect(entry.version).toBe("2.0.0");
  });

  test("handles errors gracefully (logs warning)", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockRejectedValue(new Error("Network error")),
    );
    const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});

    const result = await fetchPackageWorkItems([
      { pkg: "azure-core", lang: "Python" },
    ]);
    expect(result).toBeInstanceOf(Map);
    expect(result.size).toBe(0);
    expect(warnSpy).toHaveBeenCalled();

    warnSpy.mockRestore();
  });

  test("filters out empty package names", async () => {
    const result = await fetchPackageWorkItems([
      { pkg: "", lang: "Python" },
      { pkg: null, lang: "Java" },
    ]);
    expect(result.size).toBe(0);
  });
});

describe("fetchAzureSdkPackageList", () => {
  test("success returns HTML text", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        text: () => Promise.resolve("<html>azure-core</html>"),
      }),
    );
    const result = await fetchAzureSdkPackageList();
    expect(result).toBe("<html>azure-core</html>");
  });

  test("non-OK response returns empty string", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 500,
      }),
    );
    const result = await fetchAzureSdkPackageList();
    expect(result).toBe("");
  });

  test("fetch error returns empty string", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockRejectedValue(new Error("DNS resolution failed")),
    );
    const result = await fetchAzureSdkPackageList();
    expect(result).toBe("");
  });
});
