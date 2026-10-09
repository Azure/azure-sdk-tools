import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { runInNewContext } from 'node:vm';
import { load } from 'js-yaml';
import { describe, expect, it } from 'vitest';

const pipeline = load(readFileSync(resolve(__dirname, '../../azure-sdk-qa-bot-agent/pipelines/server-cd.yml'), 'utf8')) as any;
const stages = pipeline.extends.parameters.stages;
const entries = stages[0].jobs;
const server = entries.find((entry: any) => entry.job === 'DeployServer');
const knowledge = entries.find((entry: any) => entry["${{ if ne(parameters.environment, 'preview') }}"])
    ["${{ if ne(parameters.environment, 'preview') }}"][0];

describe('server CD job separation', () => {
    it('keeps the existing templates, one stage, Linux pools, and default combined deployment', () => {
        expect(pipeline.parameters.find((parameter: any) => parameter.name === 'deploymentTarget')).toMatchObject({ type: 'string', default: 'all', values: ['all', 'server', 'knowledge-sync'] });
        expect(JSON.stringify(pipeline)).not.toContain('webjobOnly');
        expect(pipeline.extends.template).toBe('/eng/pipelines/templates/stages/1es-redirect.yml');
        expect(stages).toHaveLength(1);
        expect(entries).toHaveLength(3);
        for (const job of [server, knowledge]) {
            expect(job.pool).toEqual({ name: '$(LINUXPOOL)', image: '$(LINUXVMIMAGE)', os: 'linux' });
            expect(job.steps[0].checkout).toBe('self');
        }
    });

    it('runs server steps only for all or server targets', () => {
        expect(server.condition).toBe("and(succeeded(), ${{ ne(parameters.deploymentTarget, 'knowledge-sync') }})");
        expect(server.steps.map((step: any) => step.displayName)).toEqual([
            'Checkout repository', 'Resolve Image Tag', 'Build & Push Image',
            'Validate Image Exists', 'Update App Service', 'Health Check'
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
            "${{ if eq(parameters.deploymentTarget, 'all') }}", 'Deploy Scheduled Knowledge Sync WebJob'
        ]);
        expect(knowledge.steps[2].inputs.versionSpec).toBe('24.x');
        expect(knowledge.steps.slice(3, 6).map((step: any) => step.bash)).toEqual([
            'npm ci', 'npm test -- --no-file-parallelism',
            'npm run build:webjob -- $(Build.ArtifactStagingDirectory)/knowledge-sync/knowledge-sync.zip'
        ]);
        expect(knowledge.steps[6]["${{ if eq(parameters.deploymentTarget, 'all') }}"].map((step: any) => step.displayName)).toEqual([
            'Download Knowledge Sync SSH Key', 'Configure Knowledge Sync SSH Key'
        ]);
        expect(knowledge.steps[7].inputs.inlineScript).toBe('node "$(syncWorkingDirectory)/scripts/deploy-webjob.mjs"');
        expect(knowledge.templateContext.outputs).toEqual([{
            output: 'pipelineArtifact', path: '$(Build.ArtifactStagingDirectory)/knowledge-sync',
            artifact: 'knowledge-sync', displayName: 'Publish Scheduled Knowledge Sync ZIP', condition: 'succeededOrFailed()'
        }]);
        expect(JSON.stringify(knowledge)).not.toMatch(/\$\((imageTag|imageName|appVersion)\)|outputs\[/);
    });

    it.each(['all', 'server', 'knowledge-sync'])('gates dependency results explicitly (target=%s), including cancellation', (target) => {
        expect(knowledge.condition).toBe("and(not(canceled()), or(and(${{ eq(parameters.deploymentTarget, 'all') }}, eq(dependencies.DeployServer.result, 'Succeeded')), and(${{ eq(parameters.deploymentTarget, 'knowledge-sync') }}, eq(dependencies.DeployServer.result, 'Skipped'))))");
        const condition = knowledge.condition
            .replace("${{ eq(parameters.deploymentTarget, 'all') }}", String(target === 'all'))
            .replace("${{ eq(parameters.deploymentTarget, 'knowledge-sync') }}", String(target === 'knowledge-sync'));
        for (const result of ['Succeeded', 'SucceededWithIssues', 'Skipped', 'Failed', 'Canceled']) {
            for (const canceled of [false, true]) {
                const actual = runInNewContext(condition, {
                    and: (...values: boolean[]) => values.every(Boolean), or: (...values: boolean[]) => values.some(Boolean),
                    not: (value: boolean) => !value, eq: (left: string, right: string) => left === right,
                    canceled: () => canceled, dependencies: { DeployServer: { result } }
                });
                expect(actual).toBe(!canceled && target !== 'server' && result === (target === 'knowledge-sync' ? 'Skipped' : 'Succeeded'));
            }
        }
    });

    it('includes the failing validation job only for knowledge-sync preview', () => {
        const validation = entries[0]["${{ if and(eq(parameters.deploymentTarget, 'knowledge-sync'), eq(parameters.environment, 'preview')) }}"][0];
        expect(validation.job).toBe('ValidateDeployment');
        expect(validation.pool).toEqual(server.pool);
        expect(validation.steps[0].checkout).toBe('none');
        expect(validation.steps[1].bash).toContain('Knowledge sync deployment is supported only for dev and prod.');
        expect(validation.steps[1].bash).toContain('exit 1');
    });
});