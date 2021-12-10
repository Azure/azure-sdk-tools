import path from "path";
import fs from "fs";
const yaml = require('yaml');

function addExcludeBranch(branches: any) {
    if (branches && branches.include.includes('feature/*')) {
        if (!branches['exclude']) {
            branches['exclude'] = [];
        }
        if (!branches['exclude'].includes('feature/v4')) {
            branches['exclude'].push('feature/v4');
            return true;
        }
    }
    return false;
}

function addArtifact(artifacts: any, name: string, safeName: string) {
    if (!artifacts) return false;
    for (const artifact of artifacts) {
        if (name === artifact.name) return false;
    }
    artifacts.push({
        name: name,
        safeName: safeName
    });
    return true;
}

export function modifyOrGenerateCiYaml(azureSDKForJSRepoRoot: string, changedPackageDirectory: string, packageName: string) {
    const relativeRpFolderPathRegexResult = /sdk[\/\\][^\/]*[\/\\]/.exec(changedPackageDirectory);
    if (relativeRpFolderPathRegexResult) {
        let relativeRpFolderPath = relativeRpFolderPathRegexResult[0];
        const rpFolderName = path.basename(relativeRpFolderPath);
        const rpFolderPath = path.join(azureSDKForJSRepoRoot, relativeRpFolderPath);
        const ciYamlPath = path.join(rpFolderPath, 'ci.yml');
        const name = packageName.replace('@', '').replace('/', '-');
        const safeName = name.replace(/-/g, '');
        if (fs.existsSync(ciYamlPath)) {
            const ciYaml = yaml.parse(fs.readFileSync(ciYamlPath, {encoding: 'utf-8'}));
            let changed = addExcludeBranch(ciYaml?.trigger?.branches);
            changed = addExcludeBranch(ciYaml?.pr?.branches) || changed;
            changed = addArtifact(ciYaml?.extends?.parameters?.Artifacts, name, safeName) || changed;
            if (changed) {
                fs.writeFileSync(ciYamlPath, yaml.stringify(ciYaml), {encoding: 'utf-8'});
            }
        } else {
            relativeRpFolderPath = relativeRpFolderPath.replace(/\\/g, '/');
            const ciYaml = `# NOTE: Please refer to https://aka.ms/azsdk/engsys/ci-yaml before editing this file.
trigger:
  branches:
    include:
      - main
      - release/*
      - hotfix/*
  paths:
    include:
      - ${relativeRpFolderPath}

pr:
  branches:
    include:
      - main
      - release/*
      - hotfix/*
  paths:
    include:
      - ${relativeRpFolderPath}

extends:
  template: ../../eng/pipelines/templates/stages/archetype-sdk-client.yml
  parameters:
    ServiceDirectory: ${rpFolderName}
    Artifacts:
      - name: ${name}
        safeName: ${safeName}
        `;
            fs.writeFileSync(ciYamlPath, ciYaml, {encoding: 'utf-8'});
        }
    }
}
