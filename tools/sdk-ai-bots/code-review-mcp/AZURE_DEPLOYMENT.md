# Deploying Code Review MCP Server to Azure

This guide explains how to expose the code_review API as an MCP server on Azure App Service with Microsoft Entra ID (Azure AD) authentication.

## Architecture Overview

```
┌─────────────────┐
│ GitHub Copilot  │
│   (VS Code)     │
└────────┬────────┘
         │
         │ MCP Protocol
         │ (stdio/SSE)
         ▼
┌─────────────────────────────────┐
│  Code Review MCP Server         │
│  (Azure App Service)            │
│  - Python FastAPI/HTTP          │
│  - Microsoft Entra ID Auth      │
└────────┬────────────────────────┘
         │
         │ HTTP + Bearer Token
         │
         ▼
┌─────────────────────────────────┐
│ azure-sdk-qa-bot-backend        │
│ (Existing Azure App Service)    │
│ /code_review endpoint           │
└─────────────────────────────────┘
```

## Prerequisites

1. Azure subscription with appropriate permissions
2. Azure CLI installed and logged in (`az login`)
3. Docker installed (for containerization)
4. Existing `azure-sdk-qa-bot-backend` deployed with Microsoft authentication

## Deployment Steps

### Option 1: Azure App Service with HTTP Endpoint (Recommended)

This approach deploys an HTTP-based MCP server that GitHub Copilot can connect to via SSE (Server-Sent Events).

#### Step 1: Create Azure Resources

```bash
# Set variables (for dev environment)
RESOURCE_GROUP="azure-sdk-qa-bot-dev"
LOCATION="westus2"
APP_SERVICE_PLAN="azuresdkqabot-dev-plan"
APP_NAME="azure-sdk-code-review-mcp-dev"
ACR_NAME="azuresdkqabotdevcontainer"
BACKEND_URL="https://azuresdkqabot-dev-server-hrcrckaad5gcedcv.westus2-01.azurewebsites.net"

# Create App Service (if using new plan)
az appservice plan create \
  --name $APP_SERVICE_PLAN \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --is-linux \
  --sku B1

# Create Web App
az webapp create \
  --resource-group $RESOURCE_GROUP \
  --plan $APP_SERVICE_PLAN \
  --name $APP_NAME \
  --deployment-container-image-name $ACR_NAME.azurecr.io/code-review-mcp:latest
```

#### Step 2: Configure Microsoft Entra ID Authentication

```bash
# Create App Registration for the MCP Server
APP_ID=$(az ad app create \
  --display-name "Azure SDK Code Review MCP" \
  --sign-in-audience AzureADMyOrg \
  --query appId -o tsv)

echo "App ID: $APP_ID"

# Create service principal
az ad sp create --id $APP_ID

# Expose API with scope
az ad app update --id $APP_ID --identifier-uris "api://$APP_ID"

# Add API scope
az ad app update --id $APP_ID --set api.oauth2PermissionScopes='[{
  "adminConsentDescription": "Access Code Review MCP API",
  "adminConsentDisplayName": "Access Code Review API",
  "id": "'$(uuidgen)'",
  "isEnabled": true,
  "type": "User",
  "userConsentDescription": "Access Code Review MCP API",
  "userConsentDisplayName": "Access Code Review API",
  "value": "access_as_user"
}]'

# Configure App Service Authentication
az webapp auth microsoft update \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --client-id $APP_ID \
  --issuer "https://login.microsoftonline.com/$(az account show --query tenantId -o tsv)/v2.0" \
  --allowed-audiences "api://$APP_ID" \
  --yes
```

#### Step 3: Configure App Service Settings

```bash
# Set environment variables with dev backend URL
az webapp config appsettings set \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --settings \
    CODE_REVIEW_API_URL="$BACKEND_URL/code_review" \
    BACKEND_CLIENT_ID="api://azure-sdk-qa-bot-dev" \
    WEBSITES_PORT="8000"

# Enable system-assigned managed identity
az webapp identity assign \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME
```

#### Step 4: Build and Push Docker Image

```bash
# Build Docker image
cd tools/sdk-ai-bots/code-review-mcp
docker build -t code-review-mcp:latest -f Dockerfile.http .

# Login to ACR
az acr login --name $ACR_NAME

# Tag and push
docker tag code-review-mcp:latest $ACR_NAME.azurecr.io/code-review-mcp:latest
docker push $ACR_NAME.azurecr.io/code-review-mcp:latest
```

#### Step 5: Configure ACR Access

```bash
# Get managed identity principal ID
PRINCIPAL_ID=$(az webapp identity show \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --query principalId -o tsv)

# Grant AcrPull role
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role AcrPull \
  --scope $(az acr show --name $ACR_NAME --query id -o tsv)

# Configure Web App to use managed identity for ACR
az webapp config set \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --generic-configurations '{"acrUseManagedIdentityCreds": true}'
```

#### Step 6: Restart and Verify

```bash
# Restart the app
az webapp restart \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME

# Check logs
az webapp log tail \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME

# Test the endpoint
curl https://$APP_NAME.azurewebsites.net/health
```

### Option 2: Azure Container Instances (For Testing)

For simpler testing without App Service:

```bash
# Create container instance
az container create \
  --resource-group $RESOURCE_GROUP \
  --name code-review-mcp \
  --image $ACR_NAME.azurecr.io/code-review-mcp:latest \
  --cpu 1 \
  --memory 1 \
  --registry-login-server $ACR_NAME.azurecr.io \
  --registry-username $(az acr credential show --name $ACR_NAME --query username -o tsv) \
  --registry-password $(az acr credential show --name $ACR_NAME --query passwords[0].value -o tsv) \
  --environment-variables \
    CODE_REVIEW_API_URL=https://$BACKEND_ENDPOINT/code_review \
  --ports 8000 \
  --dns-name-label azure-sdk-code-review-mcp \
  --location $LOCATION
```

## Configuring GitHub Copilot to Use the MCP Server

### For HTTP/SSE-based Server

Add to your VS Code `settings.json`:

```json
{
  "github.copilot.chat.mcp.servers": {
    "azure-sdk-code-review": {
      "type": "sse",
      "url": "https://azure-sdk-code-review-mcp.azurewebsites.net/sse",
      "authentication": {
        "type": "bearer",
        "token": {
          "command": "az",
          "args": ["account", "get-access-token", "--resource", "api://YOUR-APP-ID", "--query", "accessToken", "-o", "tsv"]
        }
      }
    }
  }
}
```

Replace `YOUR-APP-ID` with the actual App ID from Step 2.

## Monitoring and Troubleshooting

### View Logs

```bash
# Stream logs
az webapp log tail \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME

# Download logs
az webapp log download \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --log-file logs.zip
```

### Check Health

```bash
# Health endpoint
curl https://$APP_NAME.azurewebsites.net/health

# MCP endpoint
curl https://$APP_NAME.azurewebsites.net/mcp \
  -H "Authorization: Bearer $(az account get-access-token --resource api://$APP_ID --query accessToken -o tsv)"
```

### Common Issues

1. **Authentication Failures**
   - Verify App Registration is configured correctly
   - Check that client ID matches in settings
   - Ensure user has access to the resource

2. **Connection to Backend Fails**
   - Verify backend endpoint URL
   - Check managed identity has permissions
   - Verify backend accepts the token scope

3. **Container Startup Issues**
   - Check environment variables
   - Verify ACR access permissions
   - Review application logs

## Security Considerations

1. **Network Security**
   - Consider using VNet integration for backend communication
   - Enable Private Endpoints for enhanced security

2. **Authentication**
   - Use managed identity for Azure resource access
   - Implement proper token validation
   - Consider API Management for rate limiting

3. **Secrets Management**
   - Store sensitive data in Azure Key Vault
   - Use managed identity to access Key Vault
   - Never hardcode credentials

## Cost Optimization

- Use Basic tier (B1) for development
- Scale to Standard tier (S1+) for production
- Enable autoscaling based on load
- Consider Azure Container Apps for better cost efficiency

## Next Steps

1. Set up CI/CD pipeline for automated deployments
2. Configure custom domain and SSL certificate
3. Implement monitoring with Application Insights
4. Add rate limiting and throttling
5. Set up alerts for errors and performance issues
