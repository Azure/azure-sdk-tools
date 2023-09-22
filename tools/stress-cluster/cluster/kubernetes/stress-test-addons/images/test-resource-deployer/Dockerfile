FROM mcr.microsoft.com/powershell

RUN pwsh -c '$ErrorActionPreference = "Stop"; Install-Module Az -Force';
RUN apt-get update && apt-get -y install curl
RUN curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
RUN install -o root -g root -m 0755 kubectl /usr/local/bin/kubectl
RUN kubectl version --client

# For local testing, run prepare.ps1 before building the docker image
COPY ./docker_build/common /common
COPY test-resources-post.ps1 /scripts/stress-test/
COPY deploy-stress-test-resources.ps1 /scripts/stress-test/

CMD pwsh -c /scripts/stress-test/deploy-stress-test-resources.ps1
