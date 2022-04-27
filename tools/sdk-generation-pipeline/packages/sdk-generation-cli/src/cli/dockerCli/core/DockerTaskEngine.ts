import { DockerContext } from "../dockerCli";
import { dockerTaskEngineConfig } from "../schema/dockerTaskEngineConfig";
import {
    addFileLog,
    CodegenToSdkConfig,
    GenerateAndBuildInput,
    GenerateAndBuildOptions,
    getCodegenToSdkConfig,
    getGenerateAndBuildOutput,
    getTask,
    getTestOutput,
    InitOptions,
    initOutput,
    logger,
    MockTestInput,
    MockTestOptions, removeFileLog,
    requireJsonc,
    runScript,
    StringMap
} from "@azure-tools/sdk-generation-lib";
import * as path from "path";
import * as fs from "fs";
import { disableFileMode, getHeadRef, getHeadSha, safeDirectory } from "../../../utils/git";
import { Logger } from "winston";
import { execSync } from "child_process";
import { writeFileSync } from "fs";

export type DockerTaskEngineContext = {
    logger: Logger;
    configFilePath: string;
    initOutput: string;
    generateAndBuildInputJson: string;
    generateAndBuildOutputJson: string;
    mockTestInputJson: string;
    mockTestOutputJson: string;
    initTaskLog: string;
    generateAndBuildTaskLog: string;
    mockTestTaskLog: string;
    readmeMdPath: string;
    specRepo: {
        repoPath: string;
        headSha: string;
        headRef: string;
        repoHttpsUrl: string;
    };
    serviceType?: string;
    tag?: string;
    sdkRepo: string;
    resultOutputFolder?: string;
    envs?: StringMap<string | boolean | number>;
    packageFolders?: string[];
    mockServerHost?: string;
    taskResults?: {};
    taskResultJsonPath: string;
}

export function initializeDockerTaskEngineContext(dockerContext: DockerContext): DockerTaskEngineContext {
    // before execute task engine, safe spec repos and sdk repos because they may be owned by others
    safeDirectory(dockerContext.specRepo);
    safeDirectory(dockerContext.sdkRepo);
    const dockerTaskEngineConfigProperties = dockerTaskEngineConfig.getProperties();
    const dockerTaskEngineContext: DockerTaskEngineContext = {
        logger: dockerContext.logger,
        configFilePath: dockerTaskEngineConfigProperties.configFilePath,
        initOutput: path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.initOutput),
        generateAndBuildInputJson: path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.generateAndBuildInputJson),
        generateAndBuildOutputJson: path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.generateAndBuildOutputJson),
        mockTestInputJson: path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.mockTestInputJson),
        mockTestOutputJson: path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.mockTestOutputJson),
        initTaskLog: path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.initTaskLog),
        generateAndBuildTaskLog: path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.generateAndBuildTaskLog),
        mockTestTaskLog: path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.mockTestTaskLog),
        readmeMdPath: dockerContext.readmeMdPath,
        specRepo: {
            repoPath: dockerContext.specRepo,
            headSha: dockerTaskEngineConfigProperties.headSha ?? getHeadSha(dockerContext.specRepo),
            headRef: dockerTaskEngineConfigProperties.headRef ?? getHeadRef(dockerContext.specRepo),
            repoHttpsUrl: dockerTaskEngineConfigProperties.repoHttpsUrl
        },
        serviceType: dockerContext.readmeMdPath.includes('data-plane') && dockerTaskEngineConfigProperties.serviceType ? 'data-plane' : 'resource-manager',
        tag: dockerContext.tag,
        sdkRepo: dockerContext.sdkRepo,
        resultOutputFolder: dockerContext.resultOutputFolder ?? '/tmp/output',
        mockServerHost: dockerTaskEngineConfigProperties.mockServerHost,
        taskResultJsonPath: path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.taskResultJson)
    }
    return dockerTaskEngineContext;
}

async function beforeRunTaskEngine(context: DockerTaskEngineContext) {
    if (!!context.resultOutputFolder && !fs.existsSync(context.resultOutputFolder)) {
        fs.mkdirSync(context.resultOutputFolder, {recursive: true});
    }
}

async function afterRunTaskEngine(context: DockerTaskEngineContext) {
    if (!context.specRepo?.repoPath || !fs.existsSync(context.specRepo.repoPath)) return;
    const userGroupId = (execSync(`stat -c "%u:%g" ${context.specRepo.repoPath}`, {encoding: "utf8"})).trim();
    if (!!context.resultOutputFolder && fs.existsSync(context.resultOutputFolder)) {
        execSync(`chown -R ${userGroupId} ${context.specRepo.repoPath}`);
    }
    if (!!context.sdkRepo && fs.existsSync(context.sdkRepo)) {
        execSync(`chown -R ${userGroupId} ${context.sdkRepo}`, {encoding: "utf8"});
        disableFileMode(context.sdkRepo);
    }
    if (!!context.taskResults) {
        writeFileSync(context.taskResultJsonPath, JSON.stringify(context.taskResults, undefined, 2), 'utf-8');
    }
}

async function getTaskToRun(context: DockerTaskEngineContext): Promise<string[]> {
    const codegenToSdkConfig: CodegenToSdkConfig = getCodegenToSdkConfig(requireJsonc(path.join(context.sdkRepo, context.configFilePath)));
    context.logger.info(`Get codegen_to_sdk_config.json`);
    context.logger.info(JSON.stringify(codegenToSdkConfig, undefined, 2));
    const tasksToRun: string[] = [];
    for (const task of Object.keys(codegenToSdkConfig)) {
        tasksToRun.push(task);
        if (!context.taskResults) {
            context.taskResults = {};
        }
        context.taskResults[task] = 'skipped';
    }
    context.logger.info(`Get tasks to run: ${tasksToRun.join(', ')}`);
    return tasksToRun;
}

async function runInitTask(context: DockerTaskEngineContext) {
    const initTask = getTask(path.join(context.sdkRepo, context.configFilePath), 'init');
    if (!initTask) {
        throw `Init task is ${initTask}`;
    }
    const initOptions = initTask as InitOptions;
    const runOptions = initOptions.initScript;
    addFileLog(context.logger, context.initTaskLog, 'init');
    const executeResult = await runScript(runOptions, {
        cwd: path.resolve(context.sdkRepo),
        args: [context.initOutput],
        customizedLogger: context.logger
    });
    removeFileLog(context.logger, 'init');
    context.taskResults['init'] = executeResult === 'succeeded'? 'success' : 'failure';
    if (executeResult === 'failed') {
        throw `Execute init script failed.`
    }
    if (fs.existsSync(context.initOutput)) {
        const initOutputJson = initOutput(requireJsonc(context.initOutput));
        context.logger.info(JSON.stringify(initOutputJson, undefined, 2));

        if (initOutputJson?.envs) {
            context.envs = initOutputJson.envs;
        }
    }
}

async function runGenerateAndBuildTask(context: DockerTaskEngineContext) {
    const generateAndBuildTask = getTask(path.join(context.sdkRepo, context.configFilePath), 'generateAndBuild');
    if (!generateAndBuildTask) {
        throw `Generate and build task is ${generateAndBuildTask}`;
    }
    const generateAndBuildOptions = generateAndBuildTask as GenerateAndBuildOptions;
    const runOptions = generateAndBuildOptions.generateAndBuildScript;
    const readmeMdAbsolutePath = path.join(context.specRepo.repoPath, context.readmeMdPath);
    const specRepoPath = context.specRepo.repoPath.includes('specification') ? context.specRepo.repoPath : path.join(context.specRepo.repoPath, 'specification');
    const relatedReadmeMdFileRelativePath = path.relative(specRepoPath, readmeMdAbsolutePath);
    const inputContent: GenerateAndBuildInput = {
        specFolder: specRepoPath,
        headSha: context.specRepo.headSha,
        headRef: context.specRepo.headRef,
        repoHttpsUrl: context.specRepo.repoHttpsUrl,
        relatedReadmeMdFile: relatedReadmeMdFileRelativePath,
        serviceType: context.serviceType
    };
    const inputJson = JSON.stringify(inputContent, undefined, 2)
    context.logger.info(inputJson);
    fs.writeFileSync(context.generateAndBuildInputJson, inputJson, {encoding: 'utf-8'});
    addFileLog(context.logger, context.generateAndBuildTaskLog, 'generateAndBuild');
    const executeResult = await runScript(runOptions, {
        cwd: path.resolve(context.sdkRepo),
        args: [context.generateAndBuildInputJson, context.generateAndBuildOutputJson],
        envs: context.envs,
        customizedLogger: context.logger
    });
    removeFileLog(context.logger, 'generateAndBuild');
    context.taskResults['generateAndBuild'] = executeResult === 'succeeded'? 'success' : 'failure';
    if (executeResult === 'failed') {
        throw `Execute generateAndBuild script failed.`
    }
    if (fs.existsSync(context.generateAndBuildOutputJson)) {
        const generateAndBuildOutputJson = getGenerateAndBuildOutput(requireJsonc(context.generateAndBuildOutputJson));
        context.logger.info(JSON.stringify(generateAndBuildOutputJson, undefined, 2));
        const packageFolders: string[] = [];
        for (const p of generateAndBuildOutputJson.packages) {
            packageFolders.push(p.packageFolder);
        }
        context.packageFolders = packageFolders;
    }
}

async function runMockTestTask(context: DockerTaskEngineContext) {
    const mockTestTask = getTask(path.join(context.sdkRepo, context.configFilePath), 'mockTest');
    if (!mockTestTask) {
        throw `Init task is ${mockTestTask}`;
    }
    const mockTestOptions = mockTestTask as MockTestOptions;
    const runOptions = mockTestOptions.mockTestScript;
    for (const packageFolder of context.packageFolders) {
        context.logger.info(`Run MockTest for ${packageFolder}`);

        const inputContent: MockTestInput = {
            packageFolder: path.join(context.sdkRepo, packageFolder),
            mockServerHost: context.mockServerHost
        };
        const inputJson = JSON.stringify(inputContent, undefined, 2)
        context.logger.info(inputJson);
        const formattedPackageName = packageFolder.replace(/[^a-zA-z0-9]/g, '-');
        const mockTestInputJsonPath = context.packageFolders.length > 1? context.mockTestInputJson.replace('.json', `${formattedPackageName}.json`) : context.mockTestInputJson;
        const mockTestOutputJsonPath = context.packageFolders.length > 1? context.mockTestOutputJson.replace('.json', `${formattedPackageName}.json`) : context.mockTestOutputJson;
        const mockTestTaskLogPath = context.packageFolders.length > 1? context.mockTestTaskLog.replace('task.log', `${formattedPackageName}-task.log`) : context.mockTestTaskLog;
        fs.writeFileSync(mockTestInputJsonPath, inputJson, {encoding: 'utf-8'});
        addFileLog(context.logger, mockTestTaskLogPath, `mockTest_${formattedPackageName}`);
        const executeResult = await runScript(runOptions, {
            cwd: path.resolve(context.sdkRepo),
            args: [mockTestInputJsonPath, mockTestOutputJsonPath],
            envs: context.envs,
            customizedLogger: context.logger
        });
        context.taskResults['mockTest'] = executeResult === 'succeeded' && context.taskResults['mockTest'] !== 'failure'? 'success' : 'failure';
        removeFileLog(context.logger, `mockTest_${formattedPackageName}`);
        if (fs.existsSync(mockTestOutputJsonPath)) {
            const mockTestOutputJson = getTestOutput(requireJsonc(mockTestOutputJsonPath))
            context.logger.info(JSON.stringify(mockTestOutputJson, undefined, 2));
        }
        if (context.taskResults['mockTest'] === 'failure') {
            throw new Error('Run Mock Test Failed');
        }
    }
}

export async function runTaskEngine(context: DockerTaskEngineContext) {
    await beforeRunTaskEngine(context);
    try {
        const tasksToRun: string[] = await getTaskToRun(context);
        if (tasksToRun.includes('init')) {
            await runInitTask(context);
        }
        if (tasksToRun.includes('generateAndBuild')) {
            await runGenerateAndBuildTask(context);
        }
        if (tasksToRun.includes('mockTest')) {
            await runMockTestTask(context);
        }
    } finally {
        await afterRunTaskEngine(context);
    }
}