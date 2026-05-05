import { describe, test, expect } from "vitest";

// Test DevOps API utility functions that don't require network
import {
  LANGUAGES, LANGUAGE_DISPLAY, LANGUAGE_PACKAGE_WI,
  extractChildIds, getField, mapReleasePlan, isKnownPackage, isGAVersion,
} from "../lib/devops-api.js";

describe("devops-api module", () => {
  describe("constants", () => {
    test("LANGUAGES has all 5 SDK languages", () => {
      expect(LANGUAGES).toEqual(["Dotnet", "JavaScript", "Python", "Java", "Go"]);
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
          { rel: "System.LinkTypes.Hierarchy-Forward", url: "https://dev.azure.com/_apis/wit/workItems/123" },
          { rel: "System.LinkTypes.Hierarchy-Forward", url: "https://dev.azure.com/_apis/wit/workItems/456" },
        ],
      };
      expect(extractChildIds(wi)).toEqual([123, 456]);
    });

    test("ignores non-hierarchy relations", () => {
      const wi = {
        relations: [
          { rel: "System.LinkTypes.Related", url: "https://dev.azure.com/_apis/wit/workItems/789" },
          { rel: "System.LinkTypes.Hierarchy-Reverse", url: "https://dev.azure.com/_apis/wit/workItems/101" },
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
      const wi = { fields: { "System.Title": "My Title", "Custom.Foo": "bar" } };
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
        fields[`Custom.SDKPullRequestFor${lang}`] = `https://github.com/Azure/azure-sdk-for-${lang.toLowerCase()}/pull/1`;
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

    test("strips trailing slashes from PR URLs", () => {
      const wi = {
        id: 300,
        fields: {
          "System.Title": "URL Test",
          "System.State": "New",
          "Custom.SDKPullRequestForDotnet": "https://github.com/Azure/azure-sdk-for-net/pull/123///",
        },
        relations: [],
      };
      const result = mapReleasePlan(wi, {});
      expect(result.languages[".NET"].sdkPrUrl).toBe("https://github.com/Azure/azure-sdk-for-net/pull/123");
    });

    test("maps API spec from child work items", () => {
      const wi = {
        id: 400,
        fields: { "System.Title": "With Spec", "System.State": "New" },
        relations: [
          { rel: "System.LinkTypes.Hierarchy-Forward", url: "https://dev.azure.com/_apis/wit/workItems/401" },
        ],
      };
      const apiSpecMap = {
        401: {
          id: 401,
          fields: {
            "System.WorkItemType": "API Spec",
            "Custom.ActiveSpecPullRequestUrl": "https://github.com/Azure/azure-rest-api-specs/pull/50",
            "Custom.APISpecversion": "2024-01-01",
            "Custom.APISpecDefinitionType": "TypeSpec",
            "Custom.RESTAPIReviews": '',
          },
        },
      };
      const result = mapReleasePlan(wi, apiSpecMap);
      expect(result.apiSpec).not.toBeNull();
      expect(result.apiSpec.specPrUrl).toBe("https://github.com/Azure/azure-rest-api-specs/pull/50");
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
          "System.CreatedBy": { displayName: "John Doe <john.doe@microsoft.com>" },
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
  });

  describe("isKnownPackage", () => {
    test("returns true when package name is found in page content", () => {
      const page = "azure-storage-blob\nazure-identity\nazure-core";
      expect(isKnownPackage("azure-identity", page)).toBe(true);
    });

    test("returns false when package name is not found", () => {
      const page = "azure-storage-blob\nazure-identity";
      expect(isKnownPackage("azure-cosmos", page)).toBe(false);
    });

    test("is case-insensitive", () => {
      const page = "Azure.Storage.Blob";
      expect(isKnownPackage("azure.storage.blob", page)).toBe(true);
    });

    test("returns false for empty name or page", () => {
      expect(isKnownPackage("", "some page")).toBeFalsy();
      expect(isKnownPackage("pkg", "")).toBeFalsy();
      expect(isKnownPackage(null, "page")).toBeFalsy();
      expect(isKnownPackage("pkg", null)).toBeFalsy();
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
});
