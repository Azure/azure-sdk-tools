# Quick Start: Deploy Code Review MCP Server to Azure

This is a simplified guide for deploying the Code Review MCP Server to Azure for the **dev environment**.

## Prerequisites

✅ Azure CLI installed and logged in  
✅ Docker installed  
✅ Access to the `azure-sdk-qa-bot-dev` resource group  
✅ Backend service already running at: https://azuresdkqabot-dev-serve-codereview-ahefg8gpdxhngah0.westus2-01.azurewebsites.net

## One-Command Deployment

```bash
cd tools/sdk-ai-bots/code-review-mcp
chmod +x deploy-azure.sh
./deploy-azure.sh
```

That's it! The script will:
1. ✅ Build the Docker image
2. ✅ Push to Azure Container Registry
3. ✅ Create/update App Service
4. ✅ Configure authentication with managed identity
5. ✅ Deploy and verify

## What Gets Created

- **App Service**: `azure-sdk-code-review-mcp-dev`
- **Location**: West US 2
- **Resource Group**: `azure-sdk-qa-bot-dev`
- **Backend**: `https://azuresdkqabot-dev-serve-codereview-ahefg8gpdxhngah0.westus2-01.azurewebsites.net/code_review`

## After Deployment

### 1. Test the Deployment

```bash
# Get your app URL
APP_URL=$(az webapp show \
  --resource-group azure-sdk-qa-bot-dev \
  --name azure-sdk-code-review-mcp-dev \
  --query defaultHostName -o tsv)

# Test health endpoint
curl https://$APP_URL/health

# Test MCP endpoint
curl https://$APP_URL/mcp
```

### 2. Configure GitHub Copilot

Add this to your VS Code `settings.json`:

```json
{
  "github.copilot.chat.mcp.servers": {
    "azure-sdk-code-review": {
      "type": "sse",
      "url": "https://azure-sdk-code-review-mcp-dev.azurewebsites.net/sse",
      "authentication": {
        "type": "bearer",
        "token": {
          "command": "az",
          "args": [
            "account",
            "get-access-token",
            "--resource",
            "api://azure-sdk-qa-bot-dev",
            "--query",
            "accessToken",
            "-o",
            "tsv"
          ]
        }
      }
    }
  }
}
```

### 3. Use in GitHub Copilot

In VS Code, ask Copilot:

```
@workspace Use the review_sdk_code tool to check this Go code:

type storageClient struct {
    name string
}
```

## Viewing Logs

```bash
# Stream live logs
az webapp log tail \
  --resource-group azure-sdk-qa-bot-dev \
  --name azure-sdk-code-review-mcp-dev

# Or view in Azure Portal
# https://portal.azure.com -> azure-sdk-code-review-mcp-dev -> Log stream
```

## Troubleshooting

### Health Check Fails
```bash
# Check if app is running
az webapp show \
  --resource-group azure-sdk-qa-bot-dev \
  --name azure-sdk-code-review-mcp-dev \
  --query state

# Restart if needed
az webapp restart \
  --resource-group azure-sdk-qa-bot-dev \
  --name azure-sdk-code-review-mcp-dev
```

### Authentication Issues
```bash
# Verify managed identity is enabled
az webapp identity show \
  --resource-group azure-sdk-qa-bot-dev \
  --name azure-sdk-code-review-mcp-dev

# Check app settings
az webapp config appsettings list \
  --resource-group azure-sdk-qa-bot-dev \
  --name azure-sdk-code-review-mcp-dev \
  --query "[?name=='CODE_REVIEW_API_URL' || name=='BACKEND_CLIENT_ID']"
```

### Backend Connection Issues
```bash
# Test backend directly
curl https://azuresdkqabot-dev-serve-codereview-ahefg8gpdxhngah0.westus2-01.azurewebsites.net/code_review \
  -X POST \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $(az account get-access-token --resource api://azure-sdk-qa-bot-dev --query accessToken -o tsv)" \
  -d '{
    "language": "go",
    "code": "type Client struct {}",
    "file_path": "sdk/storage/client.go"
  }'
```

## Re-deploying

To update the code and redeploy:

```bash
# Make your changes, then run
./deploy-azure.sh

# The script will rebuild and redeploy automatically
```

## Cost

Running on Azure App Service Basic (B1) tier:
- **~$13/month** for the App Service
- **Minimal storage costs** for container registry
- **No egress charges** (same region communication)

To save costs when not in use:
```bash
# Stop the app
az webapp stop \
  --resource-group azure-sdk-qa-bot-dev \
  --name azure-sdk-code-review-mcp-dev

# Start when needed
az webapp start \
  --resource-group azure-sdk-qa-bot-dev \
  --name azure-sdk-code-review-mcp-dev
```

## Next Steps

- [ ] Test with various code samples
- [ ] Monitor usage in Application Insights
- [ ] Set up alerts for errors
- [ ] Consider adding rate limiting if needed

## Support

For issues:
1. Check logs: `az webapp log tail ...`
2. Verify backend is responding
3. Check managed identity permissions
4. Review Application Insights for errors
