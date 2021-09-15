param(
    [string] $Tag = "latest",
    [bool] $Login = $true
)

docker build . -t stresswatcher
docker tag stresswatcher stresstestregistry.azurecr.io/services/stresswatcher:$Tag
docker tag stresswatcher stressprodregistry.azurecr.io/services/stresswatcher:$Tag

az acr login -n stresstestregistry
docker push stresstestregistry.azurecr.io/services/stresswatcher:$Tag

az acr login -n stressprodregistry
docker push stressprodregistry.azurecr.io/services/stresswatcher:$Tag
