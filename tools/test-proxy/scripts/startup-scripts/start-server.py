import subprocess
import argparse
import json
import sys
import os

CONTAINER_NAME = "ambitious_azsdk_test_proxy"
# this image should be pinned when merged to main in your repo.
IMAGE_SOURCE = "azsdkengsys.azurecr.io/engsys/testproxy-lin:1035186"

def get_proxy_container():
    unparsed_result = subprocess.check_output(["docker", "container", "ls", "-a", "--format", "\"{{ json . }}\"", "--filter", "name={}".format(CONTAINER_NAME)])

    if unparsed_result:
        decoded = unparsed_result.decode('UTF-8').strip().replace("\\\"", "")[1:-1]
        obj_result = json.loads(decoded)
    else:
        obj_result = None

    return obj_result

def docker_interact(mode, root):
    try:
        subprocess.check_output(["docker", "--version"])
    except:
        print("An invocation of docker --version failed. This indicates that docker is not properly installed or running.")
        print("Please check your docker invocation and try running the script again.")
        sys.exit(1)

    resolved_root = os.path.abspath(root)

    if mode == "start":
        proxy_container = get_proxy_container()

        # if we already have one, we just need to check the state
        if proxy_container:
            if proxy_container["State"] == "running":
                print("Discovered an already running instance of the test-proxy!. Exiting")
                sys.exit(0)
        
        # else we need to create it
        else:
            print("Attempting creation of Docker host {}".format(CONTAINER_NAME))
            subprocess.check_call(["docker", "container", "create", "-v", "{}:/srv/testproxy/".format(resolved_root), "-p", "5001:5001", "-p", "5000:5000", "--name", CONTAINER_NAME, IMAGE_SOURCE ])

        print("Attempting start of Docker host {}".format(CONTAINER_NAME))
        subprocess.check_call(["docker", "container", "start", CONTAINER_NAME])
    
    if mode == "stop":
        proxy_container = get_proxy_container()

        if proxy_container:
            if proxy_container["State"] == "running":
                print("Found a running instance of $CONTAINER_NAME, shutting it down.")
                subprocess.check_call(["docker", "container", "stop", CONTAINER_NAME])

if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Used to define simple interface with docker CLI to handle start/stop of the test-proxy server."
    )
    
    parser.add_argument(
        "-m",
        "--mode",
        dest="mode",
        help="",
        default=".",
    )

    parser.add_argument(
        "-t",
        "--target",
        dest="target_folder",
        help="",
        default=".",
    )
   
    args = parser.parse_args()
    docker_interact(args.mode, args.target_folder)
