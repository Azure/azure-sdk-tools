import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

export function prepareSkillBundle(repoRoot, outputDirectory) {
  const skills = ["azure-typespec-author", "azsdk-common-typespec-naming"];
  for (const skill of skills) {
    const source = path.join(repoRoot, ".github", "skills", skill);
    for (const entry of ["SKILL.md", "references"]) {
      if (!fs.existsSync(path.join(source, entry))) {
        throw new Error(`Missing benchmark skill input: ${skill}/${entry}`);
      }
    }
  }
  fs.mkdirSync(outputDirectory, { recursive: true });
  for (const skill of skills) {
    const source = path.join(repoRoot, ".github", "skills", skill);
    const destination = path.join(outputDirectory, skill);
    // Clear only the named staged skill so removed references cannot survive a rerun.
    fs.rmSync(destination, { recursive: true, force: true });
    fs.mkdirSync(destination);
    fs.copyFileSync(
      path.join(source, "SKILL.md"),
      path.join(destination, "SKILL.md"),
    );
    fs.cpSync(
      path.join(source, "references"),
      path.join(destination, "references"),
      {
        recursive: true,
      },
    );
  }
}

if (
  process.argv[1] &&
  import.meta.url === pathToFileURL(process.argv[1]).href
) {
  const repoRoot = fileURLToPath(new URL("../../../../../", import.meta.url));
  const output = path.join(repoRoot, "artifacts", "typespec-author-skills");
  prepareSkillBundle(repoRoot, output);
  console.log(`Prepared authoring and shared naming skills in ${output}`);
}
