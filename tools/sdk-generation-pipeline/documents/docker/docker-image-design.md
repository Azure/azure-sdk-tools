# ARCHITECTURE DIAGRAM
![docker design](docker-image-design.md)

The core of the docker image is task engine, which contains four tasks: Init Task, Generate and Build Task, Mock Test Task. There is a configuration file in each sdk repository, and it defines which task should be executed. To serve different users/pipeline, we provide different docker commands.  Also, after the tasks are executed, there are some outputs, such as generated codes, task execution result, which can be used by following steps in pipeline or service team.

# TASK ENGINE
There are mainly four tasks defined in task engine: Init Task, Generate and Build Task, Mock Test Task, Live Test Task. Task engine executes these tasks based on a configuration file in sdk repository, and you can find [the schema of configuration file here](../task-engine/schema/CodegenToSdkConfigSchema.json), and [the example](../task-engine/README.md).

As the docker image will be used in different scenarios, we hope to extract the most common part into the docker image, and the specific parts will be removed.

About the schemas of input/output of each task, please refer to [schemas](../task-engine/schema).

# DOCKER IMAGE LAYERS
The docker image will be based on Ubuntu, and it also contains all the development environment for different languages of sdk. So the overall structure of layers is the following:

![layer](images/docker-image-layers.drawio.png)
