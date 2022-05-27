We provide a docker image, which can be used to generate code, run mock test. This docker image can be used for local
development, and running in pipeline.

This document only describes the usages of docker image, if you want to get more information about the design details of
docker, please go to [design specs of docker image](docker-image-design.md).

# Prerequisites
Suggest to run the command by using wsl docker if you are using Windows machine because the docker container will request your local file system frequently, and wsl docker is much faster than running it in Windows directly.

# Docker IMAGE COMMANDS

The docker image will be used in different scenarios:

1. Run docker container in local (generate codes and do grow up development).
2. Run docker container in pipeline.

## RUN DOCKER CONTAINER IN LOCAL

### RUN DOCKER CONTAINER TO GENERATE CODES AND DO GROW UP DEVELOPMENT

Command

```shell
docker run -it --privileged  -v {local_spec_repo_path}:/spec-repo -v {local_work_folder}:/work-dir docker.image:latest --readme={relative_readme} --sdk={sdk_to_generate}
```

Parameter description:

| Parameter                | Description                                                                                                                                                                                                                                                                                                                                                                                                                                                                   | Example                                            |
|--------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------|
| { local_spec_repo_path } | Required. It's used to point to the swagger folder.                                                                                                                                                                                                                                                                                                                                                                                                                           | /home/test/azure-rest-api-specs                    |
| { local_work_folder}     | Required. It's used to point to the work folder, which will store all sdk repositories. If there is no sdk repository in the folder, the docker image will clone it                                                                                                                                                                                                                                                                                                           | /home/test/sdk-repos                               |
| { relative_readme }      | Required. It's used to specify the readme.md file and docker image uses it to generate SDKs. it's the relative path from {path_to_local_spec_repo}                                                                                                                                                                                                                                                                                                                            | specification/agrifood/resource-manager/readme.md  |
| { sdk_to_generate }      | Required. It's used to specify which language of sdk you want to generate. Supported value for management sdk: js, java, python, .net, and go. Supported value for dataplane sdk: js, java, python, and .net. If you want to generate multi-packages, use comma to separate them. (__Not recommend to generate multi packages in one docker container because the docker will failed when encoutering error in generating one sdk, and the remaining sdk will be generated__) | js,java                                            |

Example Command:
```shell
docker run -it --privileged  -v /home/test/azure-rest-api-specs:/spec-repo -v /home/test/work-dir:/work-dir docker.image:latest --readme="specification/agrifood/resource-manager/readme.md" --sdk=js,java
```

After running command, docker container generates SDKs. When SDKs are generated, the docker container doesn't exit, and you can open your browser and request `http://127.0.0.1:8080/?folder=/work-dir` for further grow up development.
If you want to re-generate codes after grow up development or changing swagger, please run command in docker container:
```shell
rerun-tasks -readme={relative_readme} --sdk={sdk_to_generate}
```
rerun-tasks is a script, which invokes task engine to re-run tasks.

**Attention**: rerun-tasks may clear your manual change, which depends whether there is `clear-output-folder: true` in the `readme.<langauge>.md`. Also, if your manual codes in a file which has the same name as generated one, it will also be overridden.

### RUN DOCKER CONTAINER TO DO GROW UP DEVELOPMENT
There are two scenarios here:
1. Service team has generated codes locally by using docker image and has exited the docker container. But they want to do grow up development now.
2. Service team has generated codes by using sdk generation pipeline, and sdk generation pipeline creates a work branch. Service team hope to do grow up based on the work branch.

Compared to scenario 1, scenario 2 needs user to clone and checkout the work branch by themselves. It’s very simple with git:
```shell
cd {local_work_folder}
git clone -b {work-branch} {repo-url}
```
Parameter description:

| Parameter             | Description                                                                             | Example                                       |
|-----------------------|-----------------------------------------------------------------------------------------|-----------------------------------------------|
| { local_work_folder } | Required. It's used to point to the work folder, which stores all sdk repositories.     | /home/test/work-dir                           |
| {work_branch }        | Required. It's used to point the work branch name generated by SDK generation pipeline. | sdkAuto/workbranch                            |
| {repo_url}            | Required. It's used to point the repository url that work branch is in.                 | https://github.com/Azure/azure-sdk-for-js.git |

Then run docker commands to do grow up development:
```shell
docker run -it --privileged  -v {local_spec_repo_path}:/spec-repo -v {local_work_folder}:/work-dir docker.image:latest --readme={relative_readme}
```
Parameter description:

| Parameter                | Description                                                                                                                                                                                           | Example                                           |
|--------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------|
| { local_spec_repo_path } | Optional. If you want to change the swagger and re-generate codes, you need to mount the swagger repo. If you only want to do grow up development, no need to mount it.                               | /home/test/azure-rest-api-specs                   |
| { local_work_folder }    | Required. It's used to point to the work folder, which stores all sdk repositories.                                                                                                                   | /home/test/work-dir                               |
| { relative_readme }      | Optional. It's used to specify the readme.md file and docker image uses it to start mock server. it's the relative path from {path_to_local_spec_repo}. If not specified, mock server will not start. | specification/agrifood/resource-manager/readme.md |

Example Command:
```shell
docker run -it --privileged  -v /home/test/azure-rest-api-specs:/spec-repo -v /home/test/work-dir:/work-dir docker.image:latest
```

After running command, docker container generates SDKs. When SDKs are generated, the docker container doesn't exit, and you can open your browser and request `http://127.0.0.1:8080/?folder=/work-dir` for further grow up development.
If you want to re-generate codes after grow up development or changing swagger, please run command in docker container:
```shell
rerun-tasks -readme={relative_readme} --sdk={sdk_to_generate}
```
rerun-tasks is a script, which invokes task engine to re-run tasks.

**Attention**: rerun-tasks may clear your manual change, which depends whether there is `clear-output-folder: true` in the `readme.<langauge>.md`. Also, if your manual codes in a file which has the same name as generated one, it will also be overridden.

## RUN DOCKER CONTAINER IN PIPELINE
The docker image also can be used by SDK Generation Pipeline. Moreover, if the service team wants to integrate the docker image in their CI pipeline, the method of integration is the same here.

Before running docker command, pipeline must prepare the spec repo and sdk repo.

Command:

```shell
docker run --privileged  -v {spec_repo_path}:/spec-repo -v {sdk_repo_path}:/sdk-repo -v {output_folder_path}:/tmp/output docker.image:latest --readme={relative_readme}
```

Parameter description:

| Parameter           | Description                                                                                                                                        | Example                                             |
|---------------------|----------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------|
| { spec_repo_path }  | Required. It's used to point to the swagger folder.                                                                                                | /home/test/azure-rest-api-specs                     |
| { sdk_repo_path }   | Required. It's used to point to the sdk repository.                                                                                                | /home/test/sdk-repos                                |
| { relative_readme } | Required. It's used to specify the readme.md file and docker image uses it to generate SDKs. it's the relative path from {path_to_local_spec_repo} | specification/agrifood/resource-manager/readme.md   |

Example Command:
```shell
docker run -v /var/run/docker.sock:/var/run/docker.sock -v /home/vsts/work/azure-rest-api-specs:/spec-repo -v /home/vsts/work/azure-sdk-for-js:/sdk-repo -v /home/vsts/work/output:/tmp/output docker.image:latest --readme=specification/agrifood/resource-manager/readme.md
```

After running the command in pipeline, docker will execute tasks automatically. Also, there will be output files generated, which will be used by pipeline's other job, such as upload codes, parsing logs.
The following is the full list of generated files:

| File Types  | Files                       | Description                                                                                                        | Schema/Example                                                                               |
|-------------|-----------------------------|--------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------|
| Logs        | init-task.log               | It contains all logs while executing init task.                                                                    | 2022-03-24 03:35:12 xxxxxxx                                                                  |
| Logs        | generateAndBuild-task.log   | It contains all logs while executing generate and build task.                                                      | 2022-03-24 03:35:12 xxxxxxx                                                                  |
| Logs        | mockTest-task.log           | It contains all logs while executing mock test task.                                                               | 2022-03-24 03:35:12 xxxxxxx                                                                  |
| Outputs     | initOutput.json             | It contains the output of init task                                                                                | [InitOutputSchema.json](../task-engine/schema/InitOutputSchema.json)                         |
| Outputs     | generateAndBuildOutput.json | It contains the output of generateAndBuildOutput script, such as the path to generated codes, artifacts and so on. | [GenerateAndBuildOutputSchema.json](../task-engine/schema/GenerateAndBuildOutputSchema.json) |
| Outputs     | mockTestOutput.json         | It contains the output of mock test task                                                                           | [TestOutputSchema.json](../task-engine/schema/TestOutputSchema.json)                         |
| Outputs     | taskResults.json            | It contains each task execution result                                                                             | { "init": "success" }                                                                        |
