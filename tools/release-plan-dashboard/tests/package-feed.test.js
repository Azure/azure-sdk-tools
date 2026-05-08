import { describe, test, expect } from "vitest";
import { mapReleasePlan, LANGUAGES, LANGUAGE_DISPLAY } from "../lib/devops-api.js";

// Tests for package feed URL generation and version display logic.
// The getPackageFeedUrl function is defined in public/app.js (client-side).
// We replicate the logic here for unit testing since it's pure logic.

// Replicate classifyPlane from app.js
function classifyPlane(p) {
  if (p.mgmtScope === "Yes") return "mgmt";
  if (p.dataScope === "Yes") return "data";
  return "mgmt";
}

// Replicate getPackageFeedUrl from app.js
function getPackageFeedUrl(lang, packageName, version, plan) {
  if (!packageName) return "";
  switch (lang) {
    case ".NET":
      return version
        ? `https://www.nuget.org/packages/${encodeURIComponent(packageName)}/${encodeURIComponent(version)}`
        : `https://www.nuget.org/packages/${encodeURIComponent(packageName)}`;
    case "Python":
      return version
        ? `https://pypi.org/project/${encodeURIComponent(packageName)}/${encodeURIComponent(version)}`
        : `https://pypi.org/project/${encodeURIComponent(packageName)}`;
    case "JavaScript":
      return version
        ? `https://www.npmjs.com/package/${encodeURIComponent(packageName)}/v/${encodeURIComponent(version)}`
        : `https://www.npmjs.com/package/${encodeURIComponent(packageName)}`;
    case "Java": {
      let groupName, artifactName;
      if (packageName.includes(":")) {
        const parts = packageName.split(":");
        groupName = parts[0];
        artifactName = parts[1];
      } else {
        artifactName = packageName;
        groupName = classifyPlane(plan) === "mgmt" ? "com.azure.resourcemanager" : "com.azure";
      }
      return version
        ? `https://central.sonatype.com/artifact/${encodeURIComponent(groupName)}/${encodeURIComponent(artifactName)}/${encodeURIComponent(version)}`
        : `https://central.sonatype.com/artifact/${encodeURIComponent(groupName)}/${encodeURIComponent(artifactName)}`;
    }
    case "Go":
      return version
        ? `https://github.com/Azure/azure-sdk-for-go/tree/${encodeURIComponent(packageName)}/v${encodeURIComponent(version)}/${encodeURIComponent(packageName)}`
        : `https://github.com/Azure/azure-sdk-for-go/tree/main/${encodeURIComponent(packageName)}`;
    default:
      return "";
  }
}

// Replicate version display logic from app.js
function getDisplayVersion(l) {
  const isReleased = (l.releaseStatus || "").toLowerCase() === "released";
  return isReleased ? (l.releasedVersion || "") : (l.pkgVersion || "");
}

describe("getPackageFeedUrl", () => {
  const dataPlan = { mgmtScope: "", dataScope: "Yes" };
  const mgmtPlan = { mgmtScope: "Yes", dataScope: "" };

  describe(".NET packages (NuGet)", () => {
    test("generates correct NuGet URL", () => {
      const url = getPackageFeedUrl(".NET", "Azure.Storage.Blobs", "12.20.0", dataPlan);
      expect(url).toBe("https://www.nuget.org/packages/Azure.Storage.Blobs/12.20.0");
    });

    test("handles special characters in package name", () => {
      const url = getPackageFeedUrl(".NET", "Azure.ResourceManager.Compute", "1.0.0", mgmtPlan);
      expect(url).toBe("https://www.nuget.org/packages/Azure.ResourceManager.Compute/1.0.0");
    });
  });

  describe("Python packages (PyPI)", () => {
    test("generates correct PyPI URL", () => {
      const url = getPackageFeedUrl("Python", "azure-storage-blob", "12.20.0", dataPlan);
      expect(url).toBe("https://pypi.org/project/azure-storage-blob/12.20.0");
    });

    test("handles preview versions", () => {
      const url = getPackageFeedUrl("Python", "azure-ai-inference", "1.0.0b1", dataPlan);
      expect(url).toBe("https://pypi.org/project/azure-ai-inference/1.0.0b1");
    });
  });

  describe("JavaScript packages (npm)", () => {
    test("generates correct npm URL", () => {
      const url = getPackageFeedUrl("JavaScript", "@azure/storage-blob", "12.20.0", dataPlan);
      expect(url).toBe("https://www.npmjs.com/package/%40azure%2Fstorage-blob/v/12.20.0");
    });

    test("handles scoped packages", () => {
      const url = getPackageFeedUrl("JavaScript", "@azure/identity", "4.0.0", dataPlan);
      expect(url).toContain("%40azure%2Fidentity");
    });
  });

  describe("Java packages (Maven Central)", () => {
    test("generates correct URL with explicit group:artifact format", () => {
      const url = getPackageFeedUrl("Java", "com.azure:azure-storage-blob", "12.20.0", dataPlan);
      expect(url).toBe("https://central.sonatype.com/artifact/com.azure/azure-storage-blob/12.20.0");
    });

    test("uses com.azure for data plane when no group specified", () => {
      const url = getPackageFeedUrl("Java", "azure-storage-blob", "12.20.0", dataPlan);
      expect(url).toBe("https://central.sonatype.com/artifact/com.azure/azure-storage-blob/12.20.0");
    });

    test("uses com.azure.resourcemanager for mgmt plane when no group specified", () => {
      const url = getPackageFeedUrl("Java", "azure-resourcemanager-compute", "1.0.0", mgmtPlan);
      expect(url).toBe("https://central.sonatype.com/artifact/com.azure.resourcemanager/azure-resourcemanager-compute/1.0.0");
    });

    test("uses explicit group even for mgmt packages", () => {
      const url = getPackageFeedUrl("Java", "com.azure.resourcemanager:azure-resourcemanager-storage", "1.0.0", mgmtPlan);
      expect(url).toBe("https://central.sonatype.com/artifact/com.azure.resourcemanager/azure-resourcemanager-storage/1.0.0");
    });
  });

  describe("Go packages (GitHub)", () => {
    test("generates correct Go module URL", () => {
      const url = getPackageFeedUrl("Go", "sdk/resourcemanager/compute/armcompute", "2.0.0", mgmtPlan);
      expect(url).toBe("https://github.com/Azure/azure-sdk-for-go/tree/sdk%2Fresourcemanager%2Fcompute%2Farmcompute/v2.0.0/sdk%2Fresourcemanager%2Fcompute%2Farmcompute");
    });

    test("handles data plane Go packages", () => {
      const url = getPackageFeedUrl("Go", "sdk/storage/azblob", "1.3.0", dataPlan);
      expect(url).toContain("sdk%2Fstorage%2Fazblob/v1.3.0");
    });
  });

  describe("edge cases", () => {
    test("returns empty string when package name is empty", () => {
      expect(getPackageFeedUrl(".NET", "", "1.0.0", dataPlan)).toBe("");
    });

    test("returns URL without version when version is empty", () => {
      expect(getPackageFeedUrl(".NET", "Azure.Core", "", dataPlan)).toBe("https://www.nuget.org/packages/Azure.Core");
    });

    test("returns empty string when both are empty", () => {
      expect(getPackageFeedUrl("Python", "", "", dataPlan)).toBe("");
    });

    test("returns empty string for unknown language", () => {
      expect(getPackageFeedUrl("Rust", "azure-sdk", "1.0.0", dataPlan)).toBe("");
    });

    test("returns URL without version when version is null-ish", () => {
      expect(getPackageFeedUrl(".NET", "Azure.Core", null, dataPlan)).toBe("https://www.nuget.org/packages/Azure.Core");
      expect(getPackageFeedUrl(".NET", "Azure.Core", undefined, dataPlan)).toBe("https://www.nuget.org/packages/Azure.Core");
    });
  });
});

describe("getDisplayVersion (version display logic)", () => {
  test("shows releasedVersion when available (released status)", () => {
    const l = { releaseStatus: "Released", releasedVersion: "2.0.0", pkgVersion: "1.9.0" };
    expect(getDisplayVersion(l)).toBe("2.0.0");
  });

  test("shows pkgVersion when not released (even if releasedVersion field has a value)", () => {
    const l = { releaseStatus: "Unreleased", releasedVersion: "2.0.0-beta.1", pkgVersion: "1.9.0" };
    expect(getDisplayVersion(l)).toBe("1.9.0");
  });

  test("shows pkgVersion when not released and no releasedVersion", () => {
    const l = { releaseStatus: "Unreleased", releasedVersion: "", pkgVersion: "1.9.0" };
    expect(getDisplayVersion(l)).toBe("1.9.0");
  });

  test("does NOT show pkgVersion when released and no releasedVersion", () => {
    const l = { releaseStatus: "Released", releasedVersion: "", pkgVersion: "1.9.0" };
    expect(getDisplayVersion(l)).toBe("");
  });

  test("handles missing fields gracefully", () => {
    expect(getDisplayVersion({})).toBe("");
    expect(getDisplayVersion({ releaseStatus: "", releasedVersion: "", pkgVersion: "" })).toBe("");
  });

  test("is case-insensitive for released status check", () => {
    const l = { releaseStatus: "RELEASED", releasedVersion: "", pkgVersion: "1.0.0" };
    expect(getDisplayVersion(l)).toBe("");
  });

  test("does NOT treat Unreleased as released (exact match)", () => {
    // With exact match === "released", "Unreleased" is NOT treated as released
    const l = { releaseStatus: "Unreleased", releasedVersion: "", pkgVersion: "1.0.0" };
    expect(getDisplayVersion(l)).toBe("1.0.0");
  });
});

describe("mapReleasePlan includes releasedVersion", () => {
  // Test that the devops-api mapReleasePlan correctly extracts ReleasedVersionFor<lang>

  test("maps ReleasedVersionFor fields to releasedVersion", () => {
    const fields = {
      "System.Title": "Version Test",
      "System.State": "In Progress",
    };
    for (const lang of LANGUAGES) {
      fields[`Custom.ReleasedVersionFor${lang}`] = `1.0.${LANGUAGES.indexOf(lang)}`;
    }
    const wi = { id: 900, fields, relations: [] };
    const result = mapReleasePlan(wi, {});

    expect(result.languages[".NET"].releasedVersion).toBe("1.0.0");
    expect(result.languages["JavaScript"].releasedVersion).toBe("1.0.1");
    expect(result.languages["Python"].releasedVersion).toBe("1.0.2");
    expect(result.languages["Java"].releasedVersion).toBe("1.0.3");
    expect(result.languages["Go"].releasedVersion).toBe("1.0.4");
  });

  test("releasedVersion defaults to empty string when field is missing", () => {
    const wi = {
      id: 901,
      fields: { "System.Title": "No Version", "System.State": "New" },
      relations: [],
    };
    const result = mapReleasePlan(wi, {});
    for (const lang of Object.keys(result.languages)) {
      expect(result.languages[lang].releasedVersion).toBe("");
    }
  });
});

describe("enrichment skips Package WI version when released", () => {
  // This tests the logic in routes/api.js where pkgVersion is not set when released
  test("pkgVersion is always set from Package WI, but namespaceApproval is skipped when released", () => {
    // Simulate the updated enrichment logic from routes/api.js
    const li = { packageName: "Azure.Core", releaseStatus: "Released", releasedVersion: "2.0.0" };
    const isReleased = (li.releaseStatus || "").toLowerCase() === "released";
    const pkgData = { version: "1.5.0", namespaceApproval: "Approved", apiReviewStatus: "Approved" };

    // Replicate the condition from routes/api.js
    if (pkgData) {
      li.pkgVersion = pkgData.version;
      if (!isReleased) {
        li.namespaceApproval = pkgData.namespaceApproval;
      }
    }

    expect(li.pkgVersion).toBe("1.5.0");
    expect(li.namespaceApproval).toBeUndefined();
  });

  test("pkgVersion and namespaceApproval are both applied when not released", () => {
    const li = { packageName: "Azure.Core", releaseStatus: "Unreleased", releasedVersion: "" };
    const isReleased = (li.releaseStatus || "").toLowerCase() === "released";
    const pkgData = { version: "1.5.0", namespaceApproval: "Approved", apiReviewStatus: "Approved" };

    if (pkgData) {
      li.pkgVersion = pkgData.version;
      if (!isReleased) {
        li.namespaceApproval = pkgData.namespaceApproval;
      }
    }

    expect(li.pkgVersion).toBe("1.5.0");
    expect(li.namespaceApproval).toBe("Approved");
  });
});

// Replicate getPackageFeedInfo from app.js
function getPackageFeedInfo(lang) {
  switch (lang) {
    case ".NET":
      return { name: "NuGet", icon: "svg-nuget" };
    case "Python":
      return { name: "PyPI", icon: "svg-pypi" };
    case "JavaScript":
      return { name: "npm", icon: "svg-npm" };
    case "Java":
      return { name: "Maven", icon: "svg-maven" };
    case "Go":
      return { name: "GitHub", icon: "svg-github" };
    default:
      return { name: "Package", icon: "📦" };
  }
}

describe("getPackageFeedInfo", () => {
  test("returns NuGet for .NET", () => {
    const info = getPackageFeedInfo(".NET");
    expect(info.name).toBe("NuGet");
    expect(info.icon).toBeDefined();
  });

  test("returns PyPI for Python", () => {
    const info = getPackageFeedInfo("Python");
    expect(info.name).toBe("PyPI");
  });

  test("returns npm for JavaScript", () => {
    const info = getPackageFeedInfo("JavaScript");
    expect(info.name).toBe("npm");
  });

  test("returns Maven for Java", () => {
    const info = getPackageFeedInfo("Java");
    expect(info.name).toBe("Maven");
  });

  test("returns GitHub for Go", () => {
    const info = getPackageFeedInfo("Go");
    expect(info.name).toBe("GitHub");
  });

  test("returns Package for unknown language", () => {
    const info = getPackageFeedInfo("Rust");
    expect(info.name).toBe("Package");
    expect(info.icon).toBe("📦");
  });
});

describe("closed PR action logic", () => {
  // Replicates the action determination logic from app.js SDK table rendering
  function determineAction(l) {
    const prSt = (l.sdkPrGitHubStatus || l.prStatus || "").toLowerCase();
    const relSt = (l.releaseStatus || "").toLowerCase();
    const hasPr = !!l.sdkPrUrl;
    const isMerged = prSt.includes("merged") || prSt === "completed";
    const isDraft = prSt === "draft";
    const isOpen = prSt === "open" || isDraft;
    const isClosed = prSt === "closed";
    const hasFailedChecks = l.prDetails && l.prDetails.failedChecks && l.prDetails.failedChecks.length > 0;
    const isApproved = l.prDetails && l.prDetails.isApproved;
    const isMergeable = l.prDetails && l.prDetails.mergeable && l.prDetails.mergeableState === "clean";

    if (!hasPr) return "generate";
    if (isClosed && !isMerged) return "link-pr";
    if (isDraft && !relSt.includes("released")) return "mark-ready";
    if (isOpen && hasFailedChecks) return "fix-checks";
    if (isMerged && !relSt.includes("released")) return "release";
    if (isOpen && isApproved && isMergeable) return "merge";
    return null;
  }

  test("returns link-pr when PR status is closed", () => {
    const action = determineAction({
      sdkPrUrl: "https://github.com/Azure/azure-sdk-for-go/pull/123",
      sdkPrGitHubStatus: "closed",
      prStatus: "closed",
      releaseStatus: "",
    });
    expect(action).toBe("link-pr");
  });

  test("returns link-pr when only prStatus is closed (no GitHub status)", () => {
    const action = determineAction({
      sdkPrUrl: "https://github.com/Azure/azure-sdk-for-go/pull/123",
      sdkPrGitHubStatus: "",
      prStatus: "Closed",
      releaseStatus: "",
    });
    expect(action).toBe("link-pr");
  });

  test("does not return link-pr when PR is merged (completed)", () => {
    const action = determineAction({
      sdkPrUrl: "https://github.com/Azure/azure-sdk-for-go/pull/123",
      sdkPrGitHubStatus: "",
      prStatus: "completed",
      releaseStatus: "",
    });
    expect(action).toBe("release");
  });

  test("does not return link-pr when PR is merged", () => {
    const action = determineAction({
      sdkPrUrl: "https://github.com/Azure/azure-sdk-for-go/pull/123",
      sdkPrGitHubStatus: "merged",
      prStatus: "",
      releaseStatus: "",
    });
    expect(action).toBe("release");
  });

  test("returns generate when no PR URL", () => {
    const action = determineAction({
      sdkPrUrl: "",
      prStatus: "",
      releaseStatus: "",
    });
    expect(action).toBe("generate");
  });

  test("returns fix-checks when open with failed checks", () => {
    const action = determineAction({
      sdkPrUrl: "https://github.com/Azure/azure-sdk-for-go/pull/123",
      sdkPrGitHubStatus: "open",
      prStatus: "open",
      releaseStatus: "",
      prDetails: { failedChecks: ["CodeQL"], isApproved: false },
    });
    expect(action).toBe("fix-checks");
  });

  test("returns merge when open, approved, and mergeable", () => {
    const action = determineAction({
      sdkPrUrl: "https://github.com/Azure/azure-sdk-for-go/pull/123",
      sdkPrGitHubStatus: "open",
      prStatus: "open",
      releaseStatus: "",
      prDetails: { failedChecks: [], isApproved: true, mergeable: true, mergeableState: "clean" },
    });
    expect(action).toBe("merge");
  });

  test("returns null when merged and already released", () => {
    const action = determineAction({
      sdkPrUrl: "https://github.com/Azure/azure-sdk-for-go/pull/123",
      sdkPrGitHubStatus: "merged",
      prStatus: "",
      releaseStatus: "Released",
    });
    expect(action).toBeNull();
  });

  test("closed takes priority over fix-checks even with failed checks", () => {
    const action = determineAction({
      sdkPrUrl: "https://github.com/Azure/azure-sdk-for-go/pull/123",
      sdkPrGitHubStatus: "closed",
      prStatus: "closed",
      releaseStatus: "",
      prDetails: { failedChecks: ["Some check"] },
    });
    expect(action).toBe("link-pr");
  });

  test("returns mark-ready when PR is in draft status and not released", () => {
    const action = determineAction({
      sdkPrUrl: "https://github.com/Azure/azure-sdk-for-python/pull/45261",
      sdkPrGitHubStatus: "draft",
      prStatus: "draft",
      releaseStatus: "",
    });
    expect(action).toBe("mark-ready");
  });

  test("mark-ready takes priority over fix-checks for draft PRs", () => {
    const action = determineAction({
      sdkPrUrl: "https://github.com/Azure/azure-sdk-for-python/pull/45261",
      sdkPrGitHubStatus: "draft",
      prStatus: "draft",
      releaseStatus: "",
      prDetails: { failedChecks: ["Some check"] },
    });
    expect(action).toBe("mark-ready");
  });

  test("does not return mark-ready when draft but already released", () => {
    const action = determineAction({
      sdkPrUrl: "https://github.com/Azure/azure-sdk-for-python/pull/45261",
      sdkPrGitHubStatus: "draft",
      prStatus: "draft",
      releaseStatus: "Released",
    });
    expect(action).toBeNull();
  });
});

describe("feed link visibility based on release status", () => {
  // Replicates the logic: only show feed link when release status is "Released"
  function shouldShowFeedLink(releaseStatus, packageName) {
    const isReleased = (releaseStatus || "").toLowerCase() === "released";
    if (!isReleased) return false;
    const url = getPackageFeedUrl(".NET", packageName, "", { mgmtScope: "No", dataScope: "Yes" });
    return !!url;
  }

  test("shows feed link when status is Released", () => {
    expect(shouldShowFeedLink("Released", "Azure.Core")).toBe(true);
  });

  test("does not show feed link when status is not Released", () => {
    expect(shouldShowFeedLink("Unreleased", "Azure.Core")).toBe(false);
  });

  test("does not show feed link when status is empty", () => {
    expect(shouldShowFeedLink("", "Azure.Core")).toBe(false);
  });

  test("does not show feed link when status is InProgress", () => {
    expect(shouldShowFeedLink("InProgress", "Azure.Core")).toBe(false);
  });

  test("shows feed link when Released even without version", () => {
    expect(shouldShowFeedLink("Released", "Azure.Core")).toBe(true);
  });

  test("does not show feed link when no package name", () => {
    expect(shouldShowFeedLink("Released", "")).toBe(false);
  });

  test("displayVersion does not fall back to pkgVersion when released", () => {
    const l = { releasedVersion: "", pkgVersion: "2.0.0", releaseStatus: "Released" };
    const isReleased = (l.releaseStatus || "").toLowerCase() === "released";
    const displayVersion = isReleased ? (l.releasedVersion || "") : (l.pkgVersion || "");
    expect(displayVersion).toBe("");
  });

  test("displayVersion uses releasedVersion when released", () => {
    const l = { releasedVersion: "3.0.0", pkgVersion: "2.0.0", releaseStatus: "Released" };
    const isReleased = (l.releaseStatus || "").toLowerCase() === "released";
    const displayVersion = isReleased ? (l.releasedVersion || "") : (l.pkgVersion || "");
    expect(displayVersion).toBe("3.0.0");
  });

  test("displayVersion uses pkgVersion when not released", () => {
    const l = { releasedVersion: "", pkgVersion: "2.0.0", releaseStatus: "Unreleased" };
    const isReleased = (l.releaseStatus || "").toLowerCase() === "released";
    const displayVersion = isReleased ? (l.releasedVersion || "") : (l.pkgVersion || "");
    expect(displayVersion).toBe("2.0.0");
  });
});
