# Use Vscode to Connect Docker Container
It's not easy to develop within docker container. However, vscode provides us a way to connect to docker container, and you can use it to write codes easily.

## Prerequisites
- Install vscode in your computer.
- Install extension **Remote - Containers**.
  ![vsocde connects docker containers](images/extension-remote-containers.png)

## Steps
Please follow the following steps to connect your vscode to docker container.
1. Press `F1` and select `Remote-Containers: Attach to Running Container`.
2. Select your running docker image, and attach to it.
3. After vscode connects to docker container, open folder `/work-dir/{sdk-repository}`. 
   1. For .Net, you can only open the generated SDK namespace folder, such as `Azure.Verticals.AgriFood.Farming`.

Then you can write your codes in vscode.

## FAQ
1. Vscode C# extension cannot load the project correctly.

    Answer: Vscode C# extension is based on OmniSharp, which sometimes make us confused. To resolve it:
    1. Run `dotnet build` to rebuild the project
    2. In vscode, press `ctrl + shift + p` and then type `Restart Omnisharp`.

2. Vscode cannot load java project correctly.

    Answer: Java repository is very large and vscode extension for Java needs to spend much time to load the project. You can check the progress in the terminal of `Java Build Status`.