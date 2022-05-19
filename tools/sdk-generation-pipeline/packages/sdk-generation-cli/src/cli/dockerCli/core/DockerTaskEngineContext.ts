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
    MockTestInput,
    MockTestOptions,
    removeFileLog,
    requireJsonc,
    runScript,
    StringMap
} from "@azure-tools/sdk-generation-lib";
import { execSync } from "child_process";
import * as fs from "fs";
import { writeFileSync } from "fs";
import * as path from "path";
import { Logger } from "winston";
import { disableFileMode, getHeadRef, getHeadSha, safeDirectory } from "../../../utils/git";
import { dockerTaskEngineInput } from "../schema/dockerTaskEngineInput";
import { DockerContext } from "./DockerContext";

export class DockerTaskEngineContext {
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
    changeOwner: boolean

    public initialize(dockerContext: DockerContext) {
        // before execute task engine, safe spec repos and sdk repos because they may be owned by others
        safeDirectory(dockerContext.specRepo);
        safeDirectory(dockerContext.sdkRepo);
        const dockerTaskEngineConfigProperties = dockerTaskEngineInput.getProperties();
        this.logger = dockerContext.logger;
        this.configFilePath = dockerTaskEngineConfigProperties.configFilePath;
        this.initOutput = path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.initOutput);
        this.generateAndBuildInputJson = path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.generateAndBuildInputJson);
        this.generateAndBuildOutputJson = path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.generateAndBuildOutputJson);
        this.mockTestInputJson = path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.mockTestInputJson);
        this.mockTestOutputJson = path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.mockTestOutputJson);
        this.initTaskLog = path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.initTaskLog);
        this.generateAndBuildTaskLog = path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.generateAndBuildTaskLog);
        this.mockTestTaskLog = path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.mockTestTaskLog);
        this.readmeMdPath = dockerContext.readmeMdPath;
        this.specRepo = {
                repoPath: dockerContext.specRepo,
                headSha: dockerTaskEngineConfigProperties.headSha ?? getHeadSha(dockerContext.specRepo),
                headRef: dockerTaskEngineConfigProperties.headRef ?? getHeadRef(dockerContext.specRepo),
                repoHttpsUrl: dockerTaskEngineConfigProperties.repoHttpsUrl
        };
        this.serviceType = dockerContext.readmeMdPath.includes('data-plane') && dockerTaskEngineConfigProperties.serviceType ? 'data-plane': 'resource-manager';
        this.tag = dockerContext.tag;
        this.sdkRepo = dockerContext.sdkRepo;
        this.resultOutputFolder = dockerContext.resultOutputFolder ?? '/tmp/output';
        this.mockServerHost = dockerTaskEngineConfigProperties.mockServerHost;
        this.taskResultJsonPath = path.join(dockerContext.resultOutputFolder, dockerTaskEngineConfigProperties.taskResultJson);
        this.changeOwner = dockerTaskEngineConfigProperties.changeOwner

    }

    public async beforeRunTaskEngine() {
        if (!!this.resultOutputFolder && !fs.existsSync(this.resultOutputFolder)) {
            fs.mkdirSync(this.resultOutputFolder, {recursive: true});
        }
        this.logger.info(`Start to run task engine in ${path.basename(this.sdkRepo)}`);
    }

    public async afterRunTaskEngine() {
        if (this.changeOwner && !!this.specRepo?.repoPath && !!fs.existsSync(this.specRepo.repoPath)) {
            const userGroupId = (execSync(`stat -c "%u:%g" ${this.specRepo.repoPath}`, {encoding: "utf8"})).trim();
            if (!!this.resultOutputFolder && fs.existsSync(this.resultOutputFolder)) {
                execSync(`chown -R ${userGroupId} ${this.specRepo.repoPath}`);
            }
            if (!!this.sdkRepo && fs.existsSync(this.sdkRepo)) {
                execSync(`chown -R ${userGroupId} ${this.sdkRepo}`, {encoding: "utf8"});
                disableFileMode(this.sdkRepo);
            }
        }
        if (!!this.taskResults) {
            writeFileSync(this.taskResultJsonPath, JSON.stringify(this.taskResults, undefined, 2), 'utf-8');
        }
        this.logger.info(`Finish running task engine in ${path.basename(this.sdkRepo)}`);
    }

    public async getTaskToRun(): Promise<string[]> {
        const codegenToSdkConfig: CodegenToSdkConfig = getCodegenToSdkConfig(requireJsonc(path.join(this.sdkRepo, this.configFilePath)));
        this.logger.info(`Get codegen_to_sdk_config.json`);
        this.logger.info(JSON.stringify(codegenToSdkConfig, undefined, 2));
        const tasksToRun: string[] = [];
        for (const task of Object.keys(codegenToSdkConfig)) {
            tasksToRun.push(task);
            if (!this.taskResults) {
                this.taskResults = {};
            }
            this.taskResults[task] = 'skipped';
        }
        this.logger.info(`Get tasks to run: ${tasksToRun.join(', ')}`);
        return tasksToRun;
    }

    public async runInitTask() {
        const initTask = getTask(path.join(this.sdkRepo, this.configFilePath), 'init');
        if (!initTask) {
            throw `Init task is ${initTask}`;
        }
        const initOptions = initTask as InitOptions;
        const runOptions = initOptions.initScript;
        addFileLog(this.logger, this.initTaskLog, 'init');
        const executeResult = await runScript(runOptions, {
            cwd: path.resolve(this.sdkRepo),
            args: [this.initOutput],
            customizedLogger: this.logger
        });
        removeFileLog(this.logger, 'init');
        this.taskResults['init'] = executeResult === 'succeeded'? 'success' : 'failure';
        if (executeResult === 'failed') {
            throw `Execute init script failed.`
        }
        if (fs.existsSync(this.initOutput)) {
            const initOutputJson = initOutput(requireJsonc(this.initOutput));
            this.logger.info(`Get ${path.basename(this.initOutput)}:`);
            this.logger.info(JSON.stringify(initOutputJson, undefined, 2));

            if (initOutputJson?.envs) {
                this.envs = initOutputJson.envs;
            }
        }
    }

    public async runGenerateAndBuildTask() {
        const generateAndBuildTask = getTask(path.join(this.sdkRepo, this.configFilePath), 'generateAndBuild');
        if (!generateAndBuildTask) {
            throw `Generate and build task is ${generateAndBuildTask}`;
        }
        const generateAndBuildOptions = generateAndBuildTask as GenerateAndBuildOptions;
        const runOptions = generateAndBuildOptions.generateAndBuildScript;
        const readmeMdAbsolutePath = path.join(this.specRepo.repoPath, this.readmeMdPath);
        const specRepoPath = this.specRepo.repoPath.includes('specification') ? this.specRepo.repoPath : path.join(this.specRepo.repoPath, 'specification');
        const relatedReadmeMdFileRelativePath = path.relative(specRepoPath, readmeMdAbsolutePath);
        const inputContent: GenerateAndBuildInput = {
            specFolder: specRepoPath,
            headSha: this.specRepo.headSha,
            headRef: this.specRepo.headRef,
            repoHttpsUrl: this.specRepo.repoHttpsUrl,
            relatedReadmeMdFile: relatedReadmeMdFileRelativePath,
            serviceType: this.serviceType
        };
        const inputJson = JSON.stringify(inputContent, undefined, 2)
        this.logger.info(`Get ${path.basename(this.generateAndBuildInputJson)}:`);
        this.logger.info(inputJson);
        fs.writeFileSync(this.generateAndBuildInputJson, inputJson, {encoding: 'utf-8'});
        addFileLog(this.logger, this.generateAndBuildTaskLog, 'generateAndBuild');
        const executeResult = await runScript(runOptions, {
            cwd: path.resolve(this.sdkRepo),
            args: [this.generateAndBuildInputJson, this.generateAndBuildOutputJson],
            envs: this.envs,
            customizedLogger: this.logger
        });
        removeFileLog(this.logger, 'generateAndBuild');
        this.taskResults['generateAndBuild'] = executeResult === 'succeeded'? 'success' : 'failure';
        if (executeResult === 'failed') {
            throw `Execute generateAndBuild script failed.`
        }
        if (fs.existsSync(this.generateAndBuildOutputJson)) {
            const generateAndBuildOutputJson = getGenerateAndBuildOutput(requireJsonc(this.generateAndBuildOutputJson));
            this.logger.info(`Get ${path.basename(this.generateAndBuildOutputJson)}:`);
            this.logger.info(JSON.stringify(generateAndBuildOutputJson, undefined, 2));
            const packageFolders: string[] = [];
            for (const p of generateAndBuildOutputJson.packages) {
                packageFolders.push(p.packageFolder);
            }
            this.packageFolders = packageFolders;
        }
    }

    public async runMockTestTask() {
        const mockTestTask = getTask(path.join(this.sdkRepo, this.configFilePath), 'mockTest');
        if (!mockTestTask) {
            throw `Init task is ${mockTestTask}`;
        }
        const mockTestOptions = mockTestTask as MockTestOptions;
        const runOptions = mockTestOptions.mockTestScript;
        for (const packageFolder of this.packageFolders) {
            this.logger.info(`Run MockTest for ${packageFolder}`);

            const inputContent: MockTestInput = {
                packageFolder: path.join(this.sdkRepo, packageFolder),
                mockServerHost: this.mockServerHost
            };
            const inputJson = JSON.stringify(inputContent, undefined, 2)
            const formattedPackageName = packageFolder.replace(/[^a-zA-z0-9]/g, '-');
            const mockTestInputJsonPath = this.packageFolders.length > 1? this.mockTestInputJson.replace('.json', `${formattedPackageName}.json`) : this.mockTestInputJson;
            const mockTestOutputJsonPath = this.packageFolders.length > 1? this.mockTestOutputJson.replace('.json', `${formattedPackageName}.json`) : this.mockTestOutputJson;
            const mockTestTaskLogPath = this.packageFolders.length > 1? this.mockTestTaskLog.replace('task.log', `${formattedPackageName}-task.log`) : this.mockTestTaskLog;
            fs.writeFileSync(mockTestInputJsonPath, inputJson, {encoding: 'utf-8'});
            this.logger.info(`Get ${path.basename(mockTestInputJsonPath)}:`);
            this.logger.info(inputJson);
            addFileLog(this.logger, mockTestTaskLogPath, `mockTest_${formattedPackageName}`);
            const executeResult = await runScript(runOptions, {
                cwd: path.resolve(this.sdkRepo),
                args: [mockTestInputJsonPath, mockTestOutputJsonPath],
                envs: this.envs,
                customizedLogger: this.logger
            });
            this.taskResults['mockTest'] = executeResult === 'succeeded' && this.taskResults['mockTest'] !== 'failure'? 'success' : 'failure';
            removeFileLog(this.logger, `mockTest_${formattedPackageName}`);
            if (fs.existsSync(mockTestOutputJsonPath)) {
                const mockTestOutputJson = getTestOutput(requireJsonc(mockTestOutputJsonPath))
                this.logger.info(`Get ${path.basename(mockTestOutputJsonPath)}:`);
                this.logger.info(JSON.stringify(mockTestOutputJson, undefined, 2));
            }
            if (this.taskResults['mockTest'] === 'failure') {
                throw new Error('Run Mock Test Failed');
            }
        }
    }

    public async runTaskEngine() {
        await this.beforeRunTaskEngine();
        try {
            const tasksToRun: string[] = await this.getTaskToRun();
            if (tasksToRun.includes('init')) {
                await this.runInitTask();
            }
            if (tasksToRun.includes('generateAndBuild')) {
                await this.runGenerateAndBuildTask();
            }
            if (tasksToRun.includes('mockTest')) {
                await this.runMockTestTask();
            }
        } finally {
            await this.afterRunTaskEngine();
        }
    }
}

