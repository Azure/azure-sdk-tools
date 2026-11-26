import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { runInNewContext } from 'node:vm';
import { load } from 'js-yaml';
import { describe, expect, it } from 'vitest';

const pipeline = load(readFileSync(resolve(__dirname, '../../azure-sdk-qa-bot-agent/pipelines/server-cd.yml'), 'utf8')) as any;
const stages = pipeline.extends.parameters.stages;
const entries = stages[0].jobs;
const server = entries.find((entry: any) => entry.job === 'DeployServer');
const webjobGroup = entries.find((entry: any) => entry["${{ if in(parameters.environment, 'dev', 'prod') }}"])
    ["${{ if in(parameters.environment, 'dev', 'prod') }}"];
const knowledge = webjobGroup.find((job: any) => job.job === 'DeployKnowledgeSync');
const wiki = webjobGroup.find((job: any) => job.job === 'DeployWikiIndex');

describe('server CD job separation', () => {
    it('keeps the existing templates, one stage, Linux pools, and default combined deployment', () => {
        expect(pipeline.parameters.find((parameter: any) => parameter.name === 'deploymentTarget')).toMatchObject({ type: 'string', default: 'all', values: ['all', 'server', 'knowledge-sync', 'wiki-index'] });
        expect(JSON.stringify(pipeline)).not.toContain('webjobOnly');
        expect(pipeline.extends.template).toBe('/eng/pipelines/templates/stages/1es-redirect.yml');
        expect(stages).toHaveLength(1);
        expect(entries).toHaveLength(3);
        for (const job of [server, knowledge, wiki]) {
            expect(job.pool).toEqual({ name: '$(LINUXPOOL)', image: '$(LINUXVMIMAGE)', os: 'linux' });
            expect(job.steps[0].checkout).toBe('self');
        }
    });

    it('runs server steps only for all or server targets', () => {
        expect(server.condition).toBe("and(succeeded(), ${{ in(parameters.deploymentTarget, 'all', 'server') }})");
        expect(server.steps.map((step: any) => step.displayName)).toEqual([
            'Checkout repository', 'Resolve Image Tag', 'Build & Push Image',
            'Validate Image Exists', 'Configure Agent Routing', 'Update App Service', 'Health Check'
        ]);
        expect(server.steps[2].condition).toBe("and(succeeded(), ne('${{ parameters.environment }}','prod'))");
        expect(JSON.stringify(server.steps)).not.toContain('deploymentTarget');
        expect(server.templateContext).toBeUndefined();
    });

    it('owns knowledge steps and artifact without depending on server image variables', () => {
        expect(knowledge.job).toBe('DeployKnowledgeSync');
        expect(knowledge.dependsOn).toBe('DeployServer');
        expect(knowledge.steps.map((step: any) => step.displayName ?? step.template ?? Object.keys(step)[0])).toEqual([
            'Checkout repository', '/eng/common/pipelines/templates/steps/create-authenticated-npmrc.yml',
            'Use Node.js 24.x for Scheduled Knowledge Sync', 'Install Scheduled Knowledge Sync Dependencies',
            'Test Scheduled Knowledge Sync', 'Build and Package Scheduled Knowledge Sync',
            "${{ if in(parameters.deploymentTarget, 'all', 'knowledge-sync') }}", 'Deploy Scheduled Knowledge Sync WebJob'
        ]);
        expect(knowledge.steps[2].inputs.versionSpec).toBe('24.x');
        expect(knowledge.steps.slice(3, 6).map((step: any) => step.bash)).toEqual([
            'npm ci', 'npm test -- --no-file-parallelism',
            'npm run build:webjob -- $(Build.ArtifactStagingDirectory)/knowledge-sync/knowledge-sync.zip'
        ]);
        expect(knowledge.steps[6]["${{ if in(parameters.deploymentTarget, 'all', 'knowledge-sync') }}"].map((step: any) => step.displayName)).toEqual([
            'Download Knowledge Sync SSH Key', 'Configure Knowledge Sync SSH Key'
        ]);
        expect(knowledge.steps[7].inputs.inlineScript).toBe('node "$(syncWorkingDirectory)/scripts/deploy-webjob.mjs"');
        expect(knowledge.templateContext.outputs).toEqual([{
            output: 'pipelineArtifact', path: '$(Build.ArtifactStagingDirectory)/knowledge-sync',
            artifact: 'knowledge-sync', displayName: 'Publish Scheduled Knowledge Sync ZIP', condition: 'succeededOrFailed()'
        }]);
        expect(JSON.stringify(knowledge)).not.toMatch(/\$\((imageTag|imageName|appVersion)\)|outputs\[/);
    });

    it.each(['all', 'server', 'knowledge-sync', 'wiki-index'])('gates WebJob dependency results (target=%s), including cancellation', (target) => {
        for (const [job, jobTarget] of [[knowledge, 'knowledge-sync'], [wiki, 'wiki-index']] as const) {
            const condition = job.condition
                .replace("${{ eq(parameters.deploymentTarget, 'all') }}", String(target === 'all'))
                .replace(`\${{ eq(parameters.deploymentTarget, '${jobTarget}') }}`, String(target === jobTarget));
            for (const result of ['Succeeded', 'SucceededWithIssues', 'Skipped', 'Failed', 'Canceled']) {
                for (const canceled of [false, true]) {
                    const actual = runInNewContext(condition, {
                        and: (...values: boolean[]) => values.every(Boolean), or: (...values: boolean[]) => values.some(Boolean),
                        not: (value: boolean) => !value, eq: (left: string, right: string) => left === right,
                        canceled: () => canceled, dependencies: { DeployServer: { result } }
                    });
                    expect(actual).toBe(!canceled && ((target === 'all' && result === 'Succeeded') || (target === jobTarget && result === 'Skipped')));
                }
            }
        }
    });

    it('rejects direct WebJob targets in preview', () => {
        const validation = entries[0]["${{ if and(in(parameters.deploymentTarget, 'knowledge-sync', 'wiki-index'), eq(parameters.environment, 'preview')) }}"][0];
        expect(validation.job).toBe('ValidateDeployment');
        expect(validation.pool).toEqual(server.pool);
        expect(validation.steps[0].checkout).toBe('none');
        expect(validation.steps[1].bash).toContain('WebJob deployment is supported only for dev and prod.');
        expect(validation.steps[1].bash).toContain('exit 1');
    });

    it('deploys the Wiki Index with its own schedule only in dev and prod', () => {
        expect(webjobGroup.map((job: any) => job.job)).toEqual(['DeployKnowledgeSync', 'DeployWikiIndex']);
        expect(wiki.dependsOn).toBe('DeployServer');
        expect(wiki.steps.at(-1)).toMatchObject({
            inputs: { inlineScript: 'node "$(syncWorkingDirectory)/scripts/deploy-webjob.mjs"' },
            env: {
                WEBJOB_NAME: 'wiki-index',
                WEBJOB_SETTINGS: '$(wikiWorkingDirectory)/webjob/settings.job'
            }
        });
    });
});
