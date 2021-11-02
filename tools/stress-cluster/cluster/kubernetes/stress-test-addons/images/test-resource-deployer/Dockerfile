FROM mcr.microsoft.com/powershell

RUN pwsh -c Install-Module Az -Force;

# For local testing, run prepare.ps1 before building the docker image
COPY ./docker_build/common /common
COPY test-resources-post.ps1 /scripts/stress-test/
COPY deploy-stress-test-resources.ps1 /scripts/stress-test/

CMD pwsh -c ./common/TestResources/deploy-stress-test.ps1