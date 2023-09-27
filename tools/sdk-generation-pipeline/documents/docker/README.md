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
1. Generate codes with Autorest:
    ```shell
    docker run -it --privileged -v {local_spec_repo_path}:/spec-repo -v {local_work_folder}:/work-dir -v {local_autorest_config}:/autorest.md sdkgeneration.azurecr.io/sdk-generation:latest --readme={relative_readme} --sdk={sdk_to_generate}
    ```
2. Generate codes with TypeSpec:
    ```shell
    docker run -it --privileged -v {local_spec_repo_path}:/spec-repo -v {local_work_folder}:/work-dir -v sdkgeneration.azurecr.io/sdk-generation:latest --typespec-project={relative_typespec_project} --sdk={sdk_to_generate}
    ```
Parameter description:

| Parameter                   | Description                                                                                                                                                                                                                                                                                                                                                                                                                                                                   | Example                                                                   |
|-----------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------|
| { local_spec_repo_path }    | Required. It's used to point to the swagger folder.                                                                                                                                                                                                                                                                                                                                                                                                                           | /home/test/azure-rest-api-specs                                           |
| { local_work_folder}        | Required. It's used to point to the work folder, which will store all sdk repositories. If there is no sdk repository in the folder, the docker image will clone it                                                                                                                                                                                                                                                                                                           | /home/test/sdk-repos                                                      |
| { local_autorest_config }   | Optional. When you generate data-plane sdk, and there is no autorest configuration in sdk repository or you want to change the autorest configuration, you can set new autorest config in a file and mount it to the docker container. About the content of file, please refer to [document](https://github.com/Azure/azure-rest-api-specs/blob/dpg-doc/documentation/onboard-dpg-in-sdkautomation/add-autorest-configuration-in-spec-comment.md)                             | /home/test/autorest.md ([Example file](./autorest-config-file-sample.md)) |
| { relative_readme }         | Required. It's used to specify the readme.md file and docker image uses it to generate SDKs. it's the relative path from {path_to_local_spec_repo}                                                                                                                                                                                                                                                                                                                            | specification/agrifood/resource-manager/readme.md                         |
| { relative_typespec_project }   | Required. It's used to specify the typespec project folder and docker image uses it to generate SDKs. it's the relative path from {path_to_local_spec_repo}                                                                                                                                                                                                                                                                                                                       | specification/agrifood/resource-manager/readme.md                         |
| { sdk_to_generate }         | Required. It's used to specify which language of sdk you want to generate. Supported value for management sdk: js, java, python, .NET, and go. Supported value for dataplane sdk: js, java, python, and .NET. If you want to generate multi-packages, use comma to separate them. (__Not recommend to generate multi packages in one docker container because the docker will failed when encoutering error in generating one sdk, and the remaining sdk will be generated__) | js,java                                                                   |

Example Command:
1. Generate codes with Autorest:
    ```shell
    docker run -it --privileged  -v /home/test/azure-rest-api-specs:/spec-repo -v /home/test/work-dir:/work-dir sdkgeneration.azurecr.io/sdk-generation:latest --readme="specification/agrifood/resource-manager/readme.md" --sdk=js,java
    ```
2. Generate codes with TypeSpec:
    ```shell
    docker run -it --privileged  -v /home/test/azure-rest-api-specs:/spec-repo -v /home/test/work-dir:/work-dir sdkgeneration.azurecr.io/sdk-generation:latest --typespec-project="specification/agrifood/Agrifood" --sdk=js,java
    ```

After running command, docker container generates SDKs. When SDKs are generated, the docker container doesn't exit, and you can [open your local vscode and connect to docker container](./vscode-connect-docker-container.md) for further grow up development.
If you want to re-run task engine after grow up development or changing swagger, please run command in docker container:
1. Rerun task engine when codes are generated by Autorest:
    ```shell
    rerun-tasks --readme={relative_readme} --sdk={sdk_to_generate}
    ```
2. Rerun task engine when codes are generated by TypeSpec:
    ```shell
    rerun-tasks --typespec-project={relative_typespec_project} --sdk={sdk_to_generate}
    ```
rerun-tasks is a script, which invokes task engine to re-run tasks.

### RUN DOCKER CONTAINER TO DO GROW UP DEVELOPMENT
There are two scenarios here:
1. Service team has generated codes locally by using docker image and has exited the docker container. But they want to do grow up development now.
2. Service team has generated codes by using sdk generation pipeline, and sdk generation pipeline creates a work branch. Service team hope to do grow up based on the work branch.

Run docker commands to do grow up development:
1. Codes are generated by Autorest:
    ```shell
    docker run -it --privileged -v {local_spec_repo_path}:/spec-repo -v {local_work_folder}:/work-dir -v {local_autorest_config}:/autorest.md sdkgeneration.azurecr.io/sdk-generation:latest --readme={relative_readme} --spec-link={spec-link} --sdk-work-branch={sdk-work-branch-link}
    ```
2. Codes are generated by TypeSpec:
    ```shell
    docker run -it --privileged -v {local_spec_repo_path}:/spec-repo -v {local_work_folder}:/work-dir sdkgeneration.azurecr.io/sdk-generation:latest --spec-link={spec-link} --sdk-work-branch={sdk-work-branch-link}
    ```
Parameter description:

| Parameter                 | Description                                                                                                                                                                                                                                                                                                                                                                                                                                       | Example                                                                     |
|---------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------|
| { local_spec_repo_path }  | Optional. If you want to change the swagger and re-generate codes, you need to mount the swagger repo. If you only want to do grow up development, no need to mount it. If you input a the link by parameter {spec-link}, docker container helps clone it.                                                                                                                                                                                        | /home/test/azure-rest-api-specs                                             |
| { local_work_folder }     | Required. It's used to point to the work folder, which stores all sdk repositories.                                                                                                                                                                                                                                                                                                                                                               | /home/test/work-dir                                                         |
| { local_autorest_config } | Optional. When you generate data-plane sdk, and there is no autorest configuration in sdk repository or you want to change the autorest configuration, you can set new autorest config in a file and mount it to the docker container. About the content of file, please refer to [document](https://github.com/Azure/azure-rest-api-specs/blob/dpg-doc/documentation/onboard-dpg-in-sdkautomation/add-autorest-configuration-in-spec-comment.md) | /home/test/autorest.md ([Example file](./autorest-config-file-sample.md))   |
| { relative_readme }       | Optional. It's used to specify the readme.md file and docker image uses it to start mock server. it's the relative path from {path_to_local_spec_repo}. If not specified, mock server will not start.                                                                                                                                                                                                                                             | specification/agrifood/resource-manager/readme.md                           |
| { sdk-work-branch-link }  | **Only Required in Scenario 2**. It's used to specify the link to sdk work branch generated by sdk generation pipeline, and docker container will use it to clone sdk repo and checkout the work branch. **In scenario 2, please make sure there is no corresponding sdk repo under {local_work_folder}.**                                                                                                                                        | specification/agrifood/resource-manager/readme.md                           |
| { spec-link }             | **Only Required in Scenario 2 and no local repo mounted**. It's used to specify the link to spec repo, which can be PR link, repo Link or branch link. Then docker container will use it to clone spec repo and checkout the properly branch. **In scenario 2, please make sure there is no corresponding spec repo with path '/spec-repo' in docker container.**                                                                                 | specification/agrifood/resource-manager/readme.md                           |

Example Command:
Scenario 1:
1. Codes are generated by Autorest:
    ```shell
    docker run -it --privileged  -v /home/test/azure-rest-api-specs:/spec-repo -v /home/test/work-dir:/work-dir sdkgeneration.azurecr.io/sdk-generation:latest --readme="specification/agrifood/resource-manager/readme.md"
    ```
2. Codes are generated by TypeSpec:
    ```shell
    docker run -it --privileged  -v /home/test/azure-rest-api-specs:/spec-repo -v /home/test/work-dir:/work-dir sdkgeneration.azurecr.io/sdk-generation:latest
    ```

Scenario 2:
1. Codes are generated by Autorest:
    ```shell
    docker run -it --privileged -v /home/test/work-dir:/work-dir sdkgeneration.azurecr.io/sdk-generation:latest --readme="specification/agrifood/resource-manager/readme.md" --spec-link="https://github.com/Azure/azure-rest-api-specs/pull/19850" --sdk-work-branch="https://github.com/Azure/azure-sdk-for-js/tree/agrifood/dev/branch"
    ```
2. Codes are generated by TypeSpec:
    ```shell
    docker run -it --privileged -v /home/test/work-dir:/work-dir sdkgeneration.azurecr.io/sdk-generation:latest --spec-link="https://github.com/Azure/azure-rest-api-specs/pull/19850" --sdk-work-branch="https://github.com/Azure/azure-sdk-for-js/tree/agrifood/dev/branch"
    ```

After running command, docker container generates SDKs. When SDKs are generated, the docker container doesn't exit, and you can [open your local vscode and connect to docker container](./vscode-connect-docker-container.md) for further grow up development.
If you want to re-run task engine after grow up development or changing swagger, please run command in docker container:
1. Rerun task engine when codes are generated by Autorest:
    ```shell
    rerun-tasks --readme={relative_readme} --sdk={sdk_to_generate}
    ```
2. Rerun task engine when codes are generated by TypeSpec:
    ```shell
    rerun-tasks --typespec-project={relative_typespec_project} --sdk={sdk_to_generate}
    ```
rerun-tasks is a script, which invokes task engine to re-run tasks.

## RUN DOCKER CONTAINER IN PIPELINE
The docker image also can be used by SDK Generation Pipeline. Moreover, if the service team wants to integrate the docker image in their CI pipeline, the method of integration is the same here.

Before running docker command, pipeline must prepare the spec repo and sdk repo.

Command:
1. Generate codes with Autorest:
    ```shell
    docker run --privileged  -v {spec_repo_path}:/spec-repo -v {sdk_repo_path}:/sdk-repo -v {local_autorest_config}:/autorest.md -v {output_folder_path}:/tmp/output sdkgeneration.azurecr.io/sdk-generation:latest --readme={relative_readme}
    ```
2. Generate codes with TypeSpec:
    ```shell
    docker run --privileged  -v {spec_repo_path}:/spec-repo -v {sdk_repo_path}:/sdk-repo -v {output_folder_path}:/tmp/output sdkgeneration.azurecr.io/sdk-generation:latest --typespec-project={relative_typespec_project}
    ```

Parameter description:

| Parameter                   | Description                                                                                                                                                                                                                                                                                                                                                                                                                                       | Example                                                                   |
|-----------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------|
| { spec_repo_path }          | Required. It's used to point to the swagger folder.                                                                                                                                                                                                                                                                                                                                                                                               | /home/test/azure-rest-api-specs                                           |
| { sdk_repo_path }           | Required. It's used to point to the sdk repository.                                                                                                                                                                                                                                                                                                                                                                                               | /home/test/sdk-repos                                                      |
| { local_autorest_config }   | Optional. When you generate data-plane sdk, and there is no autorest configuration in sdk repository or you want to change the autorest configuration, you can set new autorest config in a file and mount it to the docker container. About the content of file, please refer to [document](https://github.com/Azure/azure-rest-api-specs/blob/dpg-doc/documentation/onboard-dpg-in-sdkautomation/add-autorest-configuration-in-spec-comment.md) | /home/test/autorest.md ([Example file](./autorest-config-file-sample.md)) |
| { relative_readme }         | Required. It's used to specify the readme.md file and docker image uses it to generate SDKs. it's the relative path from {path_to_local_spec_repo}                                                                                                                                                                                                                                                                                                | specification/agrifood/resource-manager/readme.md                         |
| { relative_typespec_project }   | Required. It's used to specify the typespec project folder and docker image uses it to generate SDKs. it's the relative path from {path_to_local_spec_repo}                                                                                                                                                                                                                                                                                           | specification/agrifood/resource-manager/readme.md                         |

Example Command:
1. Generate codes with Autorest:
    ```shell
    docker run --privileged -v /home/vsts/work/azure-rest-api-specs:/spec-repo -v /home/vsts/work/azure-sdk-for-js:/sdk-repo -v /home/vsts/work/output:/tmp/output sdkgeneration.azurecr.io/sdk-generation:latest --readme=specification/agrifood/resource-manager/readme.md
    ```
2. Generate codes with TypeSpec:
    ```shell
    docker run --privileged -v /home/vsts/work/azure-rest-api-specs:/spec-repo -v /home/vsts/work/azure-sdk-for-js:/sdk-repo -v /home/vsts/work/output:/tmp/output sdkgeneration.azurecr.io/sdk-generation:latest --typespec-project="specification/agrifood/Agrifood"
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
