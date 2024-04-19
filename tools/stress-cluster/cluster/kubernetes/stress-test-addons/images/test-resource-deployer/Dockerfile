FROM mcr.microsoft.com/cbl-mariner/base/core:2.0

RUN curl -sSL -O https://packages.microsoft.com/config/rhel/7/packages-microsoft-prod.rpm
RUN rpm -i packages-microsoft-prod.rpm
RUN rm packages-microsoft-prod.rpm
RUN yum install powershell -y
RUN pwsh -c '$ErrorActionPreference = "Stop"; Install-Module Az -Force';
RUN curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
RUN install -o root -g root -m 0755 kubectl /usr/local/bin/kubectl
RUN kubectl version --client

# For local testing, run prepare.ps1 before building the docker image
COPY ./docker_build/common /common
COPY test-resources-post.ps1 /scripts/stress-test/
COPY deploy-stress-test-resources.ps1 /scripts/stress-test/

CMD pwsh -c /scripts/stress-test/deploy-stress-test-resources.ps1
