# @azure-tools/sdk-generation-lib

This packages includes some basic functionalities and definitions used by sdk generation pipeline.

## Install

```shell
npm i @azure-tools/sdk-generation-lib
```

# Functionalities

| Functionalities  | Description                                                                  |
|------------------|------------------------------------------------------------------------------|
| runScript        | Run any kind of script/command os supported.                                 |
| createTaskResult | It parses the logs produced by tasks, and generate a summarized task result. |
| executeTask      | The wrapper of `runScript` and `createTaskResult`.                           |
| logger           | The logger instance can be used by sdk generation pipeline.                  |
| getTask          | Get task configuration from sdk repo's task configuration.                   |

# Definitions

| Definitions             | Description                                            |
|-------------------------|--------------------------------------------------------|
| CodegenToSdkConfig      | The configuration type of `codegen_to_sdk_config.json` |
| InitOptions             | The configuration type of init task.                   |
| GenerateAndBuildOptions | The configuration type of generate and build task.     |
| MockTestOptions         | The configuration type of mock test task.              |
| RunOptions              | The configuration type of running script.              |
| LogFilter               | The configuration type of filtering log.               |
| InitOutput              | The output type of init task.                          |
| GenerateAndBuildInput   | The input type of generate and build task.             |
| GenerateAndBuildOutput  | The output type of generate and build task.            |
| MockTestInput           | The input type of mock test task.                      |
| TestOutput              | The output type of mock test task.                     |
| TaskResultStatus        | The task status.                                       |
| TaskResult              | The details of a task result.                          |
