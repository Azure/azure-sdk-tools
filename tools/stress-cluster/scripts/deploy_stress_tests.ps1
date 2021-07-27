[CmdletBinding(DefaultParameterSetName = 'Default')]
param(
    [string]$searchDirectory,
    [hashtable]$filters,
    [string]$environment,
    [string]$uploadToRegistry,
    [string]$repository,

    [Parameter(Mandatory=$true)]
    [string]$deployId,

    [Parameter(ParameterSetName = 'DoLogin', Mandatory = $true)]
    [string]$subscription,

    [Parameter(ParameterSetName = 'DoLogin', Mandatory = $true)]
    [string]$clusterName,

    [Parameter(ParameterSetName = 'DoLogin', Mandatory = $true)]
    [string]$clusterGroup
)

. $PSScriptRoot/find_all_stress_packages.ps1

# Powershell does not (at time of writing) treat exit codes from external binaries
# as cause for stopping execution, so do this via a wrapper function.
# See https://github.com/PowerShell/PowerShell-RFC/pull/277
function run() {
    Write-Output "" "==> $args" ""
    $command, $arguments = $args
    & $command $arguments
    if (!$?) {
        $code = $LASTEXITCODE
        Write-Output "Command '$args' failed with code: $code"
        exit $code
    }
}

$runFunctionInit=[scriptblock]::create(@"
function run() {
    $function:run
}
"@)

function login([string]$subscription, [string]$clusterName, [string]$clusterGroup, [string]$uploadToRegistry) {
    Write-Output "Logging in to subscription, cluster and container registry"
    az account show
    if (!$?) {
        run az login --allow-no-subscriptions
    }

    run az aks get-credentials `
        -n "$clusterName" `
        -g "$clusterGroup" `
        --subscription "$subscription" `
        --overwrite-existing

    if ($uploadToRegistry) {
        run az acr login -n $uploadToRegistry
    }
}

function deployStressTests(
    [string]$searchDirectory = '.',
    [hashtable]$filters = @{},
    [string]$environment = 'test',
    [string]$uploadToRegistry,
    [string]$repository = 'images',
    [string]$deployId,
    [string]$subscription,
    [string]$clusterName,
    [string]$clusterGroup
) {
    if ($PSCmdlet.ParameterSetName -eq 'DoLogin') {
        login $subscription $clusterName $clusterGroup $uploadToRegistry
    }

    run helm repo add stress-test-charts https://stresstestcharts.blob.core.windows.net/helm/
    run helm repo update

    findStressPackages $searchDirectory $filters | % {
        $args = $_, $deployId, $environment, $uploadToRegistry, $repository
        Write-Output "Deploying stress test at '$($_.Directory)'"
        deployStressPackage @args
    }

    Write-Output "Releases deployed by $deployId"
    run helm list --all-namespaces -l deployId=$deployId
}

function deployStressPackage(
    [object]$pkg,
    [string]$deployId,
    [string]$environment,
    [string]$uploadToRegistry,
    [string]$repository
) {
    if ($uploadToRegistry) {
        run helm dependency update $pkg.Directory

        Get-ChildItem "$($pkg.Directory)/Dockerfile*" | % {
            # Infer docker image name from parent directory name, if file is named `Dockerfile`
            # or from suffix, is file is named like `Dockerfile.myimage` (for multiple dockerfiles).
            $prefix, $imageName = $_.Name.Split(".")
            if (!$imageName) {
                $imageName = $_.Directory.Name
            }
            $imageTag = "$uploadToRegistry.azurecr.io/$($repository.ToLower())/$($imageName):$deployId"
            Write-Output "Building and pushing stress test docker image '$imageTag'"
            run docker build -t $imageTag -f $_.FullName $_.DirectoryName
            run docker push $imageTag
        }
    }

    Write-Output "Creating namespace $($pkg.Namespace) if it does not exist..."
    kubectl create namespace $pkg.Namespace --dry-run=client -o yaml | kubectl apply -f -

    Write-Output "Installing or upgrading stress test $($pkg.ReleaseName) from $($pkg.Directory)"
    run helm upgrade $pkg.ReleaseName $pkg.Directory `
        -n $pkg.Namespace `
        --install `
        --set stress-test-addons.env=$environment
    
    # Helm 3 stores release information in kubernetes secrets. The only way to add extra labels around
    # specific releases (thereby enabling filtering on `helm list`) is to label the underlying secret resources.
    # There is not currently support for setting these labels via the helm cli.
    $helmReleaseConfig = kubectl get secrets `
        -n $pkg.Namespace `
        -l status!=superseded,name=$($pkg.ReleaseName) `
        -o jsonpath='{.items[0].metadata.name}'

    run kubectl label secret -n $pkg.Namespace --overwrite $helmReleaseConfig deployId=$deployId
}

deployStressTests @PSBoundParameters
