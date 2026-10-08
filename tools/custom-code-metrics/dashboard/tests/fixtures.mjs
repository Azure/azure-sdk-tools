import { aggregate } from "../generated/data.mjs";

export const categories = ["management", "data-plane", "provisioning"];
export const filters = { category: "", service: "", search: "" };
export function library(name, custom, generated = 0, service = "alpha", category = "data-plane") {
  const total = custom + generated;
  const customFiles = custom ? 1 : 0;
  const generatedFiles = generated ? 1 : 0;
  return {
    library: name, service, category, projectPath: `sdk/${service}/${name}/src/${name}.csproj`,
    targetFrameworks: ["net8.0"],
    metrics: {
      libraryCount: 1, customFiles, generatedFiles,
      totalFiles: customFiles + generatedFiles,
      customLines: custom, generatedLines: generated,
      totalLines: total, customRatio: total ? custom / total : null,
    },
  };
}
export function snapshot(libraries = [library("Azure.One", 10, 35)], date = "2026-10-01T12:00:00Z", revision = "1", dirty = false) {
  const fraction = date.match(/\.(\d+)(?:Z|[+-]\d{2}:\d{2})$/)?.[1] ?? "";
  const stamp = new Date(date).toISOString().slice(0, 19).replace(/[-:]/g, "") + fraction.padEnd(7, "0").slice(0, 7);
  const commit = revision.repeat(40);
  const rows = (members) => categories.map((category) => ({
    category, metrics: aggregate(members.filter((item) => item.category === category)),
  }));
  return {
    schemaVersion: "3.0", snapshotId: `${stamp}Z-${commit}`,
    collectedAt: date, repository: { name: "Azure/azure-sdk-for-net", commit, isDirty: dirty },
    summary: aggregate(libraries),
    categories: rows(libraries),
    services: [...new Set(libraries.map((item) => item.service))].map((service) => {
      const members = libraries.filter((item) => item.service === service);
      return { service, metrics: aggregate(members), categories: rows(members) };
    }),
    libraries, excludedLibraries: [],
  };
}
export const browserSnapshot = () => snapshot([
  library("Azure.Identity", 10, 35),
  library("Azure.ResourceManager.Sample", 12, 88, "beta", "management"),
  library("Azure.Provisioning.Sample", 5, 5, "beta", "provisioning"),
]);
