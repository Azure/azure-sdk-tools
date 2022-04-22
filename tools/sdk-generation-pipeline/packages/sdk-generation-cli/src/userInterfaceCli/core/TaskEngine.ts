import { DockerContext } from "../userInterfaceCli";
import { taskEngineConfig } from "../schema/taskEngineConfig";
import {
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
    MockTestOptions,
    requireJsonc,
    runScript,
    StringMap
} from "@azure-tools/sdk-generation-lib";
import * as path from "path";
import * as fs from "fs";
import { disableFileMode, getHeadRef, getHeadSha, safeDirectory } from "../../utils/git";
import { Logger } from "winston";
import { addFileLog, removeFileLog } from "../../utils/logger";
import { execSync } from "child_process";

export type TaskEngineContext = {
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
}

export function initializeTaskEngineContext(dockerContext: DockerContext): TaskEngineContext {
    const taskEngineConfigProperties = taskEngineConfig.getProperties();
    const taskEngineContext: TaskEngineContext = {
        logger: dockerContext.logger,
        configFilePath: taskEngineConfigProperties.configFilePath,
        initOutput: path.join(dockerContext.resultOutputFolder, taskEngineConfigProperties.initOutput),
        generateAndBuildInputJson: path.join(dockerContext.resultOutputFolder, taskEngineConfigProperties.generateAndBuildInputJson),
        generateAndBuildOutputJson: path.join(dockerContext.resultOutputFolder, taskEngineConfigProperties.generateAndBuildOutputJson),
        mockTestInputJson: path.join(dockerContext.resultOutputFolder, taskEngineConfigProperties.mockTestInputJson),
        mockTestOutputJson: path.join(dockerContext.resultOutputFolder, taskEngineConfigProperties.mockTestOutputJson),
        initTaskLog: path.join(dockerContext.resultOutputFolder, taskEngineConfigProperties.initTaskLog),
        generateAndBuildTaskLog: path.join(dockerContext.resultOutputFolder, taskEngineConfigProperties.generateAndBuildTaskLog),
        mockTestTaskLog: path.join(dockerContext.resultOutputFolder, taskEngineConfigProperties.mockTestTaskLog),
        readmeMdPath: dockerContext.readmeMdPath,
        specRepo: {
            repoPath: dockerContext.specRepo,
            headSha: taskEngineConfigProperties.headSha ?? getHeadSha(dockerContext.specRepo),
            headRef: taskEngineConfigProperties.headRef ?? getHeadRef(dockerContext.specRepo),
            repoHttpsUrl: taskEngineConfigProperties.repoHttpsUrl
        },
        serviceType: dockerContext.readmeMdPath.includes('data-plane') && taskEngineConfigProperties.serviceType ? 'data-plane' : 'resource-manager',
        tag: dockerContext.tag,
        sdkRepo: dockerContext.sdkRepo,
        resultOutputFolder: dockerContext.resultOutputFolder ?? '/tmp/output',
        mockServerHost: taskEngineConfigProperties.mockServerHost
    }
    return taskEngineContext;
}

async function beforeRunTaskEngine(context: TaskEngineContext) {
    if (!!context.resultOutputFolder && !fs.existsSync(context.resultOutputFolder)) {
        fs.mkdirSync(context.resultOutputFolder, {recursive: true});
    }
    safeDirectory(context.sdkRepo);
}

async function afterRunTaskEngine(context: TaskEngineContext) {
    if (!context.specRepo?.repoPath || !fs.existsSync(context.specRepo.repoPath)) return;
    const userGroupId = (execSync(`stat -c "%u:%g" ${context.specRepo.repoPath}`, {encoding: "utf8"})).trim();
    if (!!context.resultOutputFolder && fs.existsSync(context.resultOutputFolder)) {
        execSync(`chown -R ${userGroupId} ${context.specRepo.repoPath}`);
    }
    if (!!context.sdkRepo && fs.existsSync(context.sdkRepo)) {
        execSync(`chown -R ${userGroupId} ${context.sdkRepo}`, {encoding: "utf8"});
        disableFileMode(context.sdkRepo);
    }
}

async function getTaskToRun(context: TaskEngineContext): Promise<string[]> {
    const codegenToSdkConfig: CodegenToSdkConfig = getCodegenToSdkConfig(requireJsonc(path.join(context.sdkRepo, context.configFilePath)));
    context.logger.info(`Get codegen_to_sdk_config.json`);
    context.logger.info(JSON.stringify(codegenToSdkConfig, undefined, 2));
    const tasksToRun: string[] = [];
    for (const task of Object.keys(codegenToSdkConfig)) {
        tasksToRun.push(task);
    }
    context.logger.info(`Get tasks to run: ${tasksToRun.join(', ')}`);
    return tasksToRun;
}

async function runInitTask(context: TaskEngineContext) {
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
    if (executeResult === 'failed') {
        throw `Execute init script failed.`
    }
    if (fs.existsSync(context.initOutput)) {
        const initOutputJson = initOutput(requireJsonc(context.initOutput));
        context.logger.info(initOutputJson);

        if (initOutputJson?.envs) {
            context.envs = initOutputJson.envs;
        }
    }
}

async function runGenerateAndBuildTask(context: TaskEngineContext) {
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

async function runMockTestTask(context: TaskEngineContext) {
    const mockTestTask = getTask(path.join(context.sdkRepo, context.configFilePath), 'mockTest');
    if (!mockTestTask) {
        throw `Init task is ${mockTestTask}`;
    }
    const mockTestOptions = mockTestTask as MockTestOptions;
    const runOptions = mockTestOptions.mockTestScript;
    for (const packageFolder of context.packageFolders) {
        context.logger.info(`Run MockTest for ${packageFolder}`);

        const inputContent: MockTestInput = {
            packageFolder: packageFolder,
            mockServerHost: context.mockServerHost
        };
        const inputJson = JSON.stringify(inputContent, undefined, 2)
        context.logger.info(inputJson);
        const formattedPackageName = packageFolder.replace(/[^a-zA-z0-9]/g, '-');
        const mockTestInputJsonPath = context.mockTestInputJson.replace('.json', `${formattedPackageName}.json`);
        const mockTestOutputJsonPath = context.mockTestOutputJson.replace('.json', `${formattedPackageName}.json`);
        const mockTestTaskLogPath = context.mockTestTaskLog.replace('task.log', `${formattedPackageName}-task.log`)
        fs.writeFileSync(context.mockTestInputJson, inputJson, {encoding: 'utf-8'});
        addFileLog(context.logger, mockTestTaskLogPath, `mockTest_${formattedPackageName}`);
        const executeResult = await runScript(runOptions, {
            cwd: path.resolve(context.sdkRepo),
            args: [mockTestInputJsonPath, mockTestOutputJsonPath],
            envs: context.envs,
            customizedLogger: context.logger
        });
        removeFileLog(context.logger, `mockTest_${formattedPackageName}`);
        if (fs.existsSync(mockTestOutputJsonPath)) {
            const mockTestOutputJson = getTestOutput(requireJsonc(mockTestOutputJsonPath))
            context.logger.info(JSON.stringify(mockTestOutputJson, undefined, 2));
        }
    }
}

export async function runTaskEngine(context: TaskEngineContext) {
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