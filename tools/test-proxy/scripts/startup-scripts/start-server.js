const { exec, execSync } = require('child_process');
const { exit } = require('process');
const path = require('path');

var CONTAINER_NAME = "ambitious_azsdk_test_proxy"
// this image should be pinned when merged to main in your repo.
var IMAGE_SOURCE = "azsdkengsys.azurecr.io/engsys/testproxy-lin:1035186"

function getProxyContainer(){
    var result = execSync('docker container ls -a --format "{{ json . }}" --filter name=' + CONTAINER_NAME, (error, stdout, stderr) => {
        if (error) {
          console.error('Unable to execute docker container ls invocation.');
          console.error(error.message);
          exit(1);
        }
      
        if (stderr) {
          console.error('Something went wrong during docker container ls invocation.');
          console.error(stderr);
          exit(1);
        }
        
        return stdout;
    });

    if (result.toString()) {
        return JSON.parse(result.toString());
    }
    else {
        return undefined;
    }
}

function dockerInteract(mode, root){
    execSync('docker --version', (error, stdout, stderr) => {
        if (error || stderr) {
            console.error('A invocation of docker --version failed. This indicates that docker is not properly installed or running.');
            console.error('Please check your docker invocation and try running the script again.');

            exit(1);
        }
    });

    var resolvedPath = path.resolve(root)
    var proxyContainer = getProxyContainer();

    if(mode == "start"){
        // # if we already have one, we just need to check the state
        if(proxyContainer){
            if(proxyContainer.State == 'running'){
                console.log('Discovered an already running instance of the test-proxy!. Exiting.');
                exit(0);
            }
        }
        // else we need to create it
        else {
            console.log("Attempting creation of Docker host " + CONTAINER_NAME);
            console.log('docker container create -v ' + resolvedPath + ':/srv/testproxy/ -p 5001:5001 -p 5000:5000 --name ' + CONTAINER_NAME + ' ' + IMAGE_SOURCE);
            execSync('docker container create -v ' + resolvedPath + ':/srv/testproxy/ -p 5001:5001 -p 5000:5000 --name ' + CONTAINER_NAME + ' ' + IMAGE_SOURCE, (error, stdout, stderr) => {
                if (error) {
                    console.error('Creation of the test-proxy failed with docker error.');
                    console.error(error)
        
                    exit(1);
                }
            });
        }
        
        console.log('Attempting start of Docker host ' + CONTAINER_NAME)
        execSync('docker container start ' + CONTAINER_NAME, (error, stdout, stderr) => {
            if (error) {
                console.error('Start of the test-proxy failed with docker error.');
                console.error(error)
    
                exit(1);
            }
        });
    }

    if(mode == 'stop'){
        if (proxyContainer){
            if(proxyContainer.State == 'running'){
                console.log('Found a running instance of ' + CONTAINER_NAME + ', shutting it down.')

                execSync('docker container stop ' + CONTAINER_NAME, (error, stdout, stderr) => {
                    if (error) {
                        console.error("Stop of the test-proxy failed with docker error.");
                        console.error(error)
            
                        exit(1);
                    }
                });
            }
        }
    }
}



let args = process.argv.slice(2);

// simple error checking

if(args.length != 2){
    console.error("Unable to invoke. Expecting two arguments in order of [node start-server.js <MODE> <ROOT>]")
}

dockerInteract(args[0], args[1]);
