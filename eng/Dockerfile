FROM mcr.microsoft.com/powershell

RUN pwsh -c Install-Module Az -Force; \
    pwsh -c Install-Module Az.Accounts -Force;

COPY ./common /common

CMD pwsh -c ./common/TestResources/deploy-stress-test.ps1