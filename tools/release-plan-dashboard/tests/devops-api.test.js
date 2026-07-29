import { describe, test, expect } from "vitest";

// Test DevOps API utility functions that don't require network
import {
  LANGUAGES,
  LANGUAGE_DISPLAY,
  LANGUAGE_PACKAGE_WI,
  extractChildIds,
  getField,
  mapReleasePlan,
  isGAVersion,
  stripEmail,
  extractSpecPrUrls,
} from "../lib/devops-api.js";

describe("devops-api module", () => {
  describe("constants", () => {
    test("LANGUAGES has all 5 SDK languages", () => {
      expect(LANGUAGES).toEqual([
        "Dotnet",
        "JavaScript",
        "Python",
        "Java",
        "Go",
      ]);
    });

    test("LANGUAGE_DISPLAY maps internal names to display names", () => {
      expect(LANGUAGE_DISPLAY.Dotnet).toBe(".NET");
      expect(LANGUAGE_DISPLAY.JavaScript).toBe("JavaScript");
      expect(LANGUAGE_DISPLAY.Python).toBe("Python");
      expect(LANGUAGE_DISPLAY.Java).toBe("Java");
      expect(LANGUAGE_DISPLAY.Go).toBe("Go");
    });

    test("LANGUAGE_PACKAGE_WI has correct mappings", () => {
      expect(LANGUAGE_PACKAGE_WI[".NET"]).toBe(".NET");
      expect(LANGUAGE_PACKAGE_WI["JavaScript"]).toBe("JavaScript");
    });
  });

  describe("extractChildIds", () => {
    test("extracts child IDs from hierarchy relations", () => {
      const wi = {
        relations: [
          {
            rel: "System.LinkTypes.Hierarchy-Forward",
            url: "https://dev.azure.com/_apis/wit/workItems/123",
          },
          {
            rel: "System.LinkTypes.Hierarchy-Forward",
            url: "https://dev.azure.com/_apis/wit/workItems/456",
          },
        ],
      };
      expect(extractChildIds(wi)).toEqual([123, 456]);
    });

    test("ignores non-hierarchy relations", () => {
      const wi = {
        relations: [
          {
            rel: "System.LinkTypes.Related",
            url: "https://dev.azure.com/_apis/wit/workItems/789",
          },
          {
            rel: "System.LinkTypes.Hierarchy-Reverse",
            url: "https://dev.azure.com/_apis/wit/workItems/101",
          },
        ],
      };
      expect(extractChildIds(wi)).toEqual([]);
    });

    test("returns empty array when no relations", () => {
      expect(extractChildIds({})).toEqual([]);
      expect(extractChildIds({ relations: [] })).toEqual([]);
    });

    test("handles invalid URL format gracefully", () => {
      const wi = {
        relations: [
          { rel: "System.LinkTypes.Hierarchy-Forward", url: "invalid-url" },
          { rel: "System.LinkTypes.Hierarchy-Forward", url: null },
        ],
      };
      expect(extractChildIds(wi)).toEqual([]);
    });
  });

  describe("getField", () => {
    test("returns field value from work item", () => {
      const wi = {
        fields: { "System.Title": "My Title", "Custom.Foo": "bar" },
      };
      expect(getField(wi, "System.Title")).toBe("My Title");
      expect(getField(wi, "Custom.Foo")).toBe("bar");
    });

    test("returns undefined for missing field", () => {
      const wi = { fields: { "System.Title": "Test" } };
      expect(getField(wi, "NonExistent")).toBeUndefined();
    });

    test("returns undefined when no fields object", () => {
      expect(getField({}, "System.Title")).toBeUndefined();
      expect(getField({ fields: null }, "System.Title")).toBeUndefined();
    });
  });

  describe("mapReleasePlan", () => {
    test("maps basic work item fields correctly", () => {
      const wi = {
        id: 100,
        fields: {
          "System.Id": 100,
          "System.Title": "Test Plan",
          "System.State": "In Progress",
          "System.CreatedDate": "2024-01-01T00:00:00Z",
          "System.ChangedDate": "2024-01-15T00:00:00Z",
          "System.CreatedBy": { displayName: "John Doe" },
          "Custom.SDKReleasemonth": "2024-03",
          "Custom.SDKtypetobereleased": "GA",
          "Custom.ReleasePlanID": "RP-123",
          "Custom.ReleasePlanSubmittedby": { displayName: "Jane Smith" },
          "Custom.PrimaryPM": "PM User <pm@example.com>",
          "Custom.ApiSpecProjectPath": "specification/compute",
          "Custom.ProductName": "Azure Compute",
          "Custom.CreatedUsing": "Copilot",
        },
        relations: [],
      };
      const result = mapReleasePlan(wi, {});
      expect(result.id).toBe(100);
      expect(result.title).toBe("Test Plan");
      expect(result.state).toBe("In Progress");
      expect(result.releaseMonth).toBe("2024-03");
      expect(result.releaseType).toBe("GA");
      expect(result.releasePlanId).toBe("RP-123");
      expect(result.submittedBy).toBe("Jane Smith");
      expect(result.ownerPM).toBe("PM User");
      expect(result.typeSpecPath).toBe("specification/compute");
      expect(result.productName).toBe("Azure Compute");
      expect(result.createdUsing).toBe("Copilot");
    });

    test("maps language fields for all 5 languages", () => {
      const fields = {
        "System.Id": 200,
        "System.Title": "Multi-lang Plan",
        "System.State": "New",
      };
      for (const lang of LANGUAGES) {
        fields[`Custom.${lang}PackageName`] = `pkg-${lang}`;
        fields[`Custom.SDKPullRequestFor${lang}`] =
          `https://github.com/Azure/azure-sdk-for-${lang.toLowerCase()}/pull/1`;
        fields[`Custom.SDKPullRequestStatusFor${lang}`] = "Active";
        fields[`Custom.ReleaseStatusFor${lang}`] = "Unreleased";
        fields[`Custom.ReleaseExclusionStatusFor${lang}`] = "";
        fields[`Custom.GenerationStatusFor${lang}`] = "Succeeded";
      }
      const wi = { id: 200, fields, relations: [] };
      const result = mapReleasePlan(wi, {});

      expect(Object.keys(result.languages)).toHaveLength(5);
      expect(result.languages[".NET"].packageName).toBe("pkg-Dotnet");
      expect(result.languages["JavaScript"].sdkPrUrl).toContain("github.com");
      expect(result.languages["Python"].prStatus).toBe("Active");
      expect(result.languages["Java"].releaseStatus).toBe("Unreleased");
      expect(result.languages["Go"].generationStatus).toBe("Succeeded");
    });

    test("hides Go language for data plane plans when Go package name is empty", () => {
      const wi = {
        id: 201,
        fields: {
          "System.Title": "Data plane without Go package",
          "System.State": "New",
          "Custom.DataScope": "Yes",
          "Custom.MgmtScope": "No",
          "Custom.DotnetPackageName": "Azure.Sample",
        },
        relations: [],
      };
      const result = mapReleasePlan(wi, {});
      expect(result.languages[".NET"].packageName).toBe("Azure.Sample");
      expect(result.languages).not.toHaveProperty("Go");
    });

    test("keeps Go language for data plane plans when Go package name is present", () => {
      const wi = {
        id: 202,
        fields: {
          "System.Title": "Data plane with Go package",
          "System.State": "New",
          "Custom.DataScope": "Yes",
          "Custom.MgmtScope": "No",
          "Custom.GoPackageName": "sdk/resourcemanager/sample/armsample",
        },
        relations: [],
      };
      const result = mapReleasePlan(wi, {});
      expect(result.languages["Go"].packageName).toBe(
        "sdk/resourcemanager/sample/armsample",
      );
    });

    test("strips trailing slashes from PR URLs", () => {
      const wi = {
        id: 300,
        fields: {
          "System.Title": "URL Test",
          "System.State": "New",
          "Custom.SDKPullRequestForDotnet":
            "https://github.com/Azure/azure-sdk-for-net/pull/123///",
        },
        relations: [],
      };
      const result = mapReleasePlan(wi, {});
      expect(result.languages[".NET"].sdkPrUrl).toBe(
        "https://github.com/Azure/azure-sdk-for-net/pull/123",
      );
    });

    test("maps API spec from child work items", () => {
      const wi = {
        id: 400,
        fields: { "System.Title": "With Spec", "System.State": "New" },
        relations: [
          {
            rel: "System.LinkTypes.Hierarchy-Forward",
            url: "https://dev.azure.com/_apis/wit/workItems/401",
          },
        ],
      };
      const apiSpecMap = {
        401: {
          id: 401,
          fields: {
            "System.WorkItemType": "API Spec",
            "Custom.ActiveSpecPullRequestUrl":
              "https://github.com/Azure/azure-rest-api-specs/pull/50",
            "Custom.APISpecversion": "2024-01-01",
            "Custom.APISpecDefinitionType": "TypeSpec",
            "Custom.RESTAPIReviews": "",
          },
        },
      };
      const result = mapReleasePlan(wi, apiSpecMap);
      expect(result.apiSpec).not.toBeNull();
      expect(result.apiSpec.specPrUrl).toBe(
        "https://github.com/Azure/azure-rest-api-specs/pull/50",
      );
      expect(result.apiSpec.apiVersion).toBe("2024-01-01");
      expect(result.apiSpec.definitionType).toBe("TypeSpec");
    });

    test("returns null apiSpec when no matching children", () => {
      const wi = {
        id: 500,
        fields: { "System.Title": "No Spec", "System.State": "New" },
        relations: [],
      };
      const result = mapReleasePlan(wi, {});
      expect(result.apiSpec).toBeNull();
    });

    test("strips email from createdBy field", () => {
      const wi = {
        id: 600,
        fields: {
          "System.Title": "Email Test",
          "System.State": "New",
          "System.CreatedBy": {
            displayName: "John Doe <john.doe@microsoft.com>",
          },
        },
        relations: [],
      };
      const result = mapReleasePlan(wi, {});
      expect(result.createdBy).toBe("John Doe");
      expect(result.createdBy).not.toContain("@");
    });

    test("handles missing optional fields gracefully", () => {
      const wi = {
        id: 700,
        fields: { "System.Title": "Minimal" },
        relations: [],
      };
      const result = mapReleasePlan(wi, {});
      expect(result.title).toBe("Minimal");
      expect(result.state).toBe("");
      expect(result.releaseMonth).toBe("");
      expect(result.apiSpec).toBeNull();
      expect(result.languages).toBeDefined();
    });

    test("handles createdBy as non-object (string)", () => {
      const wi = {
        id: 701,
        fields: {
          "System.Title": "String CreatedBy",
          "System.CreatedBy": "plain-string@example.com",
        },
        relations: [],
      };
      const result = mapReleasePlan(wi, {});
      expect(result.createdBy).toBe("");
    });

    test("handles submittedBy with uniqueName fallback", () => {
      const wi = {
        id: 702,
        fields: {
          "System.Title": "UniqueName Fallback",
          "Custom.ReleasePlanSubmittedby": {
            uniqueName: "jane@microsoft.com",
          },
        },
        relations: [],
      };
      const result = mapReleasePlan(wi, {});
      expect(result.submittedBy).toBe("jane@microsoft.com");
    });

    test("handles submittedBy as plain string", () => {
      const wi = {
        id: 703,
        fields: {
          "System.Title": "String SubmittedBy",
          "Custom.ReleasePlanSubmittedby": "submitted-user",
        },
        relations: [],
      };
      const result = mapReleasePlan(wi, {});
      expect(result.submittedBy).toBe("submitted-user");
    });

    test("handles submittedBy object with no displayName or uniqueName", () => {
      const wi = {
        id: 710,
        fields: {
          "System.Title": "Empty Object SubmittedBy",
          "Custom.ReleasePlanSubmittedby": { id: "some-guid" },
        },
        relations: [],
      };
      const result = mapReleasePlan(wi, {});
      expect(result.submittedBy).toBe("");
    });

    test("handles work item with no title field", () => {
      const wi = {
        id: 711,
        fields: { "System.State": "New" },
        relations: [],
      };
      const result = mapReleasePlan(wi, {});
      expect(result.title).toBe("");
    });

    test("extracts specPrUrl from RESTAPIReviews when ActiveSpecPullRequestUrl is empty", () => {
      const wi = {
        id: 704,
        fields: { "System.Title": "Reviews Fallback" },
        relations: [
          {
            rel: "System.LinkTypes.Hierarchy-Forward",
            url: "https://dev.azure.com/_apis/wit/workItems/705",
          },
        ],
      };
      const specChild = {
        id: 705,
        fields: {
          "System.WorkItemType": "API Spec",
          "Custom.ActiveSpecPullRequestUrl": "",
          "Custom.RESTAPIReviews":
            '<a href="https://github.com/Azure/azure-rest-api-specs/pull/77">PR</a>',
        },
      };
      const result = mapReleasePlan(wi, { 705: specChild });
      expect(result.apiSpec.specPrUrl).toBe(
        "https://github.com/Azure/azure-rest-api-specs/pull/77",
      );
    });
  });

  describe("isGAVersion", () => {
    test("returns true for GA versions", () => {
      expect(isGAVersion("1.0.0")).toBe(true);
      expect(isGAVersion("2.3.1")).toBe(true);
      expect(isGAVersion("12.0.0")).toBe(true);
    });

    test("returns false for beta versions", () => {
      expect(isGAVersion("1.0.0-beta.1")).toBe(false);
      expect(isGAVersion("2.0.0-beta")).toBe(false);
    });

    test("returns false for alpha versions", () => {
      expect(isGAVersion("1.0.0-alpha")).toBe(false);
      expect(isGAVersion("1.0.0-alpha.2")).toBe(false);
    });

    test("returns false for preview versions", () => {
      expect(isGAVersion("1.0.0-preview")).toBe(false);
      expect(isGAVersion("1.0.0-preview.3")).toBe(false);
    });

    test("returns false for RC versions", () => {
      expect(isGAVersion("1.0.0-rc.1")).toBe(false);
      expect(isGAVersion("1.0.0-rc")).toBe(false);
    });

    test("returns false for Python-style beta markers", () => {
      // The regex [-.]b\d requires a separator before 'b'
      expect(isGAVersion("2.0.0.b2")).toBe(false);
      expect(isGAVersion("1.0.0-b1")).toBe(false);
    });

    test("returns false for empty/null values", () => {
      expect(isGAVersion("")).toBe(false);
      expect(isGAVersion(null)).toBe(false);
      expect(isGAVersion(undefined)).toBe(false);
    });
  });

  describe("stripEmail", () => {
    test("returns empty string for empty input", () => {
      expect(stripEmail("")).toBe("");
    });

    test("returns empty string for null input", () => {
      expect(stripEmail(null)).toBe("");
    });

    test("strips email in angle brackets from display name", () => {
      expect(stripEmail("John Doe <john@example.com>")).toBe("John Doe");
    });

    test("splits email-only input at @ and replaces dots/underscores with spaces", () => {
      expect(stripEmail("john.doe@microsoft.com")).toBe("john doe");
    });

    test("returns plain name unchanged", () => {
      expect(stripEmail("Plain Name")).toBe("Plain Name");
    });
  });

  describe("extractSpecPrUrls", () => {
    test("extracts GitHub PR URLs from HTML href attributes", () => {
      const html =
        '<a href="https://github.com/Azure/azure-rest-api-specs/pull/123">PR 123</a> <a href="https://github.com/Azure/azure-rest-api-specs/pull/456">PR 456</a>';
      expect(extractSpecPrUrls(html)).toEqual([
        "https://github.com/Azure/azure-rest-api-specs/pull/123",
        "https://github.com/Azure/azure-rest-api-specs/pull/456",
      ]);
    });

    test("ignores non-GitHub URLs", () => {
      const html =
        '<a href="https://example.com/page">Link</a> <a href="https://github.com/Azure/azure-rest-api-specs/pull/99">PR</a>';
      expect(extractSpecPrUrls(html)).toEqual([
        "https://github.com/Azure/azure-rest-api-specs/pull/99",
      ]);
    });

    test("deduplicates PR URLs", () => {
      const html =
        '<a href="https://github.com/Azure/azure-rest-api-specs/pull/10">PR</a> <a href="https://github.com/Azure/azure-rest-api-specs/pull/10">PR again</a>';
      expect(extractSpecPrUrls(html)).toEqual([
        "https://github.com/Azure/azure-rest-api-specs/pull/10",
      ]);
    });

    test("returns empty array for empty string", () => {
      expect(extractSpecPrUrls("")).toEqual([]);
    });

    test("strips trailing slashes from URLs", () => {
      const html =
        '<a href="https://github.com/Azure/azure-rest-api-specs/pull/55/">PR</a>';
      expect(extractSpecPrUrls(html)).toEqual([
        "https://github.com/Azure/azure-rest-api-specs/pull/55",
      ]);
    });
  });
});
