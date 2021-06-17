# Currently assumes that:
#   az login
#   az acr login --name azsdkengsys
# has been executed. a public container registry will resolve this going forward.
param(
    [ValidateSet("start", "stop")]    
    [String]
    $mode
)

try {
    docker info
}
catch {
    Write-Host $_
    Write-Error "A invocation of docker info failed. This indicates that docker is not properly installed or running."
    Write-Error "Please check your docker invocation and try again"
}

# need relative path to repo root. as we need to mount it
$repoRoot = "<TODO>"

$DOCKER_RUN = "run -v $repoRoot:/etc/testproxy -p 5001:5001 -p 5000:5000 azsdkengsys.azurecr.io/engsys/ubuntu_testproxy_server:90164"
$DOCKER_CONTAINER_QUERY = "container ls -a --format -filter `"{{ json . }}`" `"label=blah`" -filter `"name=blah`""
$DOCKER_CONTAINER_CREATE = ""
$DOCKER_CONTAINER_RUN = ""
$DOCKER_CONTAINER_STOP = ""

if ($mode -eq "start"){
    # check set of visible containers filtered by the expression
    $proxyContainers = docker $DOCKER_CONTAINER_QUERY | ConvertFrom-Json

    # if there is no container, create it
    # check status of container. if it's running, we are done
    # if it's not running, start it.
}

if ($mode -eq "stop"){
    $proxyContainers = docker $DOCKER_CONTAINER_QUERY | ConvertFrom-Json
    # if there is no container, we're done
    # if there is a container, and it's stopped, we are done
    # if it's running. stop it.

}
