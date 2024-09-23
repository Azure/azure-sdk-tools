# Azure Website

## Implementing Class
[AzureWebsiteStore](../../Azure.Sdk.Tools.SecretRotation.Stores.AzureAppService/AzureWebsiteStore.cs)

## Configuration Key
Azure Website

## Supported Functions
Secondary

## Parameters

| Name           | Type   | Description                                                     |
| -------------- | ------ | --------------------------------------------------------------- |
| subscriptionId | string | The website's Azure subscription id                             |
| resourceGroup  | string | The website's resource group name                               |
| website        | string | The website's resource name                                     |
| rotationAction | string | optional, one of ( `restartWebsite`, `none` ). defaults to none |
