This is a simple web app to redirect users to either [openapihub](https://openapihub.azure-devex-tools.com) if they are on corp net, or [openapi hub access docs](https://aka.ms/openapihub-landing) if they are off corp net.
This site is configured to load from `https://portal.azure-devex-tools.com` via the [app service domain config](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/8c0e0d10-c2d6-42b8-b1d9-e7132b6f18a3/resourceGroups/openapi-platform-preview/providers/Microsoft.Web/sites/hub-redirect-app/domainsandsslv2) and the [azure-devex-tools DNS zone](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/8c0e0d10-c2d6-42b8-b1d9-e7132b6f18a3/resourceGroups/openapi-platform-preview/providers/Microsoft.Network/dnszones/azure-devex-tools.com/overview).

To update this app:

1. Update `app.py` and/or `index.html`
1. Enable basic authentication under General Settings [here](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/8c0e0d10-c2d6-42b8-b1d9-e7132b6f18a3/resourceGroups/openapi-platform-preview/providers/Microsoft.Web/sites/hub-redirect-app/configuration)
1. Run the following commands from this directory:

```
az login
az account set -s 'OpenAPI Platform - Preview'

# Install dependencies and archive app contents
pip install flask
pwsh -c Compress-Archive -path '*' -DestinationPath app.zip

# Deploy zip file to azure app service
az webapp deploy --resource-group openapi-platform-preview --name hub-redirect-app --src-path app.zip --type zip
```
