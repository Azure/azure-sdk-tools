# ADO Service Connection Parameter

## Implementing Class
[ServiceConnectionParameterStore](../../Azure.Sdk.Tools.SecretRotation.Stores.AzureDevOps/ServiceConnectionParameterStore.cs)

## Configuration Key
ADO Service Connection Parameter

## Supported Functions
Secondary

## Parameters

| Name          | Type   | Description                                                                                                                                                           |
| ------------- | ------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| accountName   | string | The name of the Azure DevOps organization. e.g.  For `https://dev.azure.com/azure-sdk`, use `azure-sdk`                                                               |
| projectName   | string | The name of the Azure DevOps project that contains the service connection                                                                                             |
| connectionId  | string | The GUID of the service connection to configure                                                                                                                       |
| parameterName | string | The name of the parameter on the service connection.|

## Notes
The `parameterName` string is internal to the Azure DevOps connection provider and isn't visible in the UI. You may need to inspect a POST request in the ADO UI to get the correct parameter name.
