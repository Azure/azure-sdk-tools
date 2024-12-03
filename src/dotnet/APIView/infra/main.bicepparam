using './main.bicep'

param environmentName = readEnvironmentVariable('AZURE_ENV_NAME', 'localdev')
param location = readEnvironmentVariable('AZURE_LOCATION', 'westus2')
param principalId = readEnvironmentVariable('AZURE_PRINCIPAL_ID', '<default principal id>')
