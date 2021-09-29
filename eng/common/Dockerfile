FROM mcr.microsoft.com/powershell

RUN pwsh -c Install-Module Az -Force
RUN pwsh -c Install-Module Az.Accounts -Force
RUN mkdir -p /common

COPY ./ /common

CMD pwsh -c ./common/TestResources/deploy-stress-test.ps1