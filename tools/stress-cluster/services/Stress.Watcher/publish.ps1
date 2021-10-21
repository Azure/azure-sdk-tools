param(
    [string] $Tag = "latest"
)

docker build . -t azsdkengsys.azurecr.io/stress/stresswatcher:$Tag

az acr login -n azsdkengsys
docker push azsdkengsys.azurecr.io/stress/stresswatcher:$Tag
