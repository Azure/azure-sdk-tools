#!/bin/bash
set -euo pipefail

# Azure SDK QA Bot MCP Server Deployment Script
#
# This script automates the deployment of the Azure SDK QA Bot MCP Server to Azure App Service
#
# Usage:
#   ./deploy-azure.sh -e dev|preview|prod [-r resource-group] [-n app-name]

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Default values
ENVIRONMENT="dev"
RESOURCE_GROUP="azure-sdk-qa-bot-dev"
APP_NAME="azure-sdk-code-review-mcp"
ACR_NAME="azuresdkqabotdevcontainer"
LOCATION="westus2"
APP_SERVICE_PLAN="azuresdkqabot-dev-plan"

# Function to print colored messages
print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to show usage
usage() {
    cat << EOF
Usage: $0 [OPTIONS]

Deploy Azure SDK QA Bot MCP Server to Azure App Service (Dev Environment)

Optional:
    -r    Resource group name (default: azure-sdk-qa-bot-dev)
    -n    App name (default: azure-sdk-qa-bot-mcp)
    -a    ACR name (default: azuresdkqabotdevcontainer)
    -l    Location (default: westus2)
    -p    App Service Plan (default: azuresdkqabot-dev-plan)
    -h    Show this help message

Example:
    $0
    $0 -n my-custom-app-name

EOF
    exit 1
}

# Parse command line arguments
while getopts "r:n:a:l:p:h" opt; do
    case $opt in
        r) RESOURCE_GROUP="$OPTARG" ;;
        n) APP_NAME="$OPTARG" ;;
        a) ACR_NAME="$OPTARG" ;;
        l) LOCATION="$OPTARG" ;;
        p) APP_SERVICE_PLAN="$OPTARG" ;;
        h) usage ;;
        *) usage ;;
    esac
done

# Dev environment only (demo)
ENVIRONMENT="dev"

# Append environment to app name
APP_NAME="${APP_NAME}-${ENVIRONMENT}"

print_info "Deployment Configuration:"
echo "  Environment: $ENVIRONMENT"
echo "  Resource Group: $RESOURCE_GROUP"
echo "  App Name: $APP_NAME"
echo "  ACR Name: $ACR_NAME"
echo "  Location: $LOCATION"
echo ""

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    print_error "Azure CLI is not installed. Please install it first."
    exit 1
fi

# Check if logged in
if ! az account show &> /dev/null; then
    print_error "Not logged in to Azure. Please run 'az login' first."
    exit 1
fi

print_info "Checking Azure subscription..."
SUBSCRIPTION_NAME=$(az account show --query name -o tsv)
print_info "Using subscription: $SUBSCRIPTION_NAME"

# Set backend URL for dev environment
BACKEND_URL="https://azuresdkqabot-dev-serve-codereview-ahefg8gpdxhngah0.westus2-01.azurewebsites.net"
BACKEND_CLIENT_ID="api://azure-sdk-qa-bot-dev"

print_info "Backend URL: $BACKEND_URL/code_review"

# Build Docker image
print_info "Building Docker image..."
IMAGE_TAG="${ENVIRONMENT}_$(git rev-parse --short HEAD)"
docker build -t azure-sdk-qa-bot-mcp:$IMAGE_TAG -f Dockerfile.http .

if [ $? -ne 0 ]; then
    print_error "Docker build failed"
    exit 1
fi

print_info "Docker image built: azure-sdk-qa-bot-mcp:$IMAGE_TAG"

# Login to ACR
print_info "Logging in to Azure Container Registry..."
az acr login --name $ACR_NAME

if [ $? -ne 0 ]; then
    print_error "Failed to login to ACR"
    exit 1
fi

# Tag and push image
print_info "Pushing image to ACR..."
docker tag azure-sdk-qa-bot-mcp:$IMAGE_TAG $ACR_NAME.azurecr.io/azure-sdk-qa-bot-mcp:$IMAGE_TAG
docker tag azure-sdk-qa-bot-mcp:$IMAGE_TAG $ACR_NAME.azurecr.io/azure-sdk-qa-bot-mcp:${ENVIRONMENT}-latest
docker push $ACR_NAME.azurecr.io/azure-sdk-qa-bot-mcp:$IMAGE_TAG
docker push $ACR_NAME.azurecr.io/azure-sdk-qa-bot-mcp:${ENVIRONMENT}-latest

print_info "Image pushed to ACR"

# Check if App Service exists
print_info "Checking if App Service exists..."
if az webapp show --resource-group $RESOURCE_GROUP --name $APP_NAME &> /dev/null; then
    print_info "App Service exists, updating configuration..."
    
    # Update container image
    az webapp config container set \
        --resource-group $RESOURCE_GROUP \
        --name $APP_NAME \
        --docker-custom-image-name $ACR_NAME.azurecr.io/azure-sdk-qa-bot-mcp:$IMAGE_TAG
    
else
    print_info "Creating new App Service..."
    
    # Check if App Service Plan exists
    if ! az appservice plan show --resource-group $RESOURCE_GROUP --name $APP_SERVICE_PLAN &> /dev/null; then
        print_info "Creating App Service Plan..."
        az appservice plan create \
            --name $APP_SERVICE_PLAN \
            --resource-group $RESOURCE_GROUP \
            --location $LOCATION \
            --is-linux \
            --sku B1
    fi
    
    # Create Web App
    az webapp create \
        --resource-group $RESOURCE_GROUP \
        --plan $APP_SERVICE_PLAN \
        --name $APP_NAME \
        --deployment-container-image-name $ACR_NAME.azurecr.io/azure-sdk-qa-bot-mcp:$IMAGE_TAG
    
    # Enable managed identity
    print_info "Enabling managed identity..."
    az webapp identity assign \
        --resource-group $RESOURCE_GROUP \
        --name $APP_NAME
    
    # Get managed identity principal ID
    PRINCIPAL_ID=$(az webapp identity show \
        --resource-group $RESOURCE_GROUP \
        --name $APP_NAME \
        --query principalId -o tsv)
    
    print_info "Waiting for managed identity to propagate in Azure AD..."
    sleep 30
    
    # Grant ACR pull access with retry
    print_info "Granting ACR pull access to managed identity..."
    ACR_ID=$(az acr show --name $ACR_NAME --query id -o tsv)
    
    MAX_RETRIES=5
    RETRY_COUNT=0
    while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
        if az role assignment create \
            --assignee $PRINCIPAL_ID \
            --role AcrPull \
            --scope $ACR_ID 2>/dev/null; then
            print_info "Successfully granted ACR pull access"
            break
        else
            RETRY_COUNT=$((RETRY_COUNT + 1))
            if [ $RETRY_COUNT -lt $MAX_RETRIES ]; then
                print_warn "Failed to grant access (attempt $RETRY_COUNT/$MAX_RETRIES), retrying in 15 seconds..."
                sleep 15
            else
                print_error "Failed to grant ACR pull access after $MAX_RETRIES attempts"
                print_warn "You may need to manually grant the role assignment:"
                print_warn "  az role assignment create --assignee $PRINCIPAL_ID --role AcrPull --scope $ACR_ID"
                exit 1
            fi
        fi
    done
    
    # Configure to use managed identity for ACR
    az webapp config set \
        --resource-group $RESOURCE_GROUP \
        --name $APP_NAME \
        --generic-configurations '{"acrUseManagedIdentityCreds": true}'
fi

# Update app settings
print_info "Updating app settings..."

# Get the user-assigned managed identity client ID if it exists
AZURE_CLIENT_ID=$(az webapp identity show \
    --resource-group $RESOURCE_GROUP \
    --name $APP_NAME \
    --query 'userAssignedIdentities.*.clientId' -o tsv 2>/dev/null || echo "")

APP_SETTINGS=(
    "CODE_REVIEW_API_URL=${BACKEND_URL}/code_review"
    "COMPLETION_API_URL=${BACKEND_URL}/completion"
    "BACKEND_CLIENT_ID=$BACKEND_CLIENT_ID"
    "WEBSITES_PORT=8000"
    "DOCKER_REGISTRY_SERVER_URL=https://$ACR_NAME.azurecr.io"
)

# Add managed identity client ID if present
if [ -n "$AZURE_CLIENT_ID" ]; then
    print_info "Found user-assigned managed identity: $AZURE_CLIENT_ID"
    APP_SETTINGS+=("AZURE_CLIENT_ID=$AZURE_CLIENT_ID")
fi

az webapp config appsettings set \
    --resource-group $RESOURCE_GROUP \
    --name $APP_NAME \
    --settings "${APP_SETTINGS[@]}"

# Configure logging
print_info "Configuring application logging..."
az webapp log config \
    --resource-group $RESOURCE_GROUP \
    --name $APP_NAME \
    --application-logging filesystem \
    --level information

# Restart the app
print_info "Restarting application..."
az webapp restart \
    --resource-group $RESOURCE_GROUP \
    --name $APP_NAME

# Wait for app to be ready
print_info "Waiting for application to be ready..."
sleep 10

# Get app URL
APP_URL=$(az webapp show \
    --resource-group $RESOURCE_GROUP \
    --name $APP_NAME \
    --query defaultHostName -o tsv)

# Test health endpoint
print_info "Testing health endpoint..."
HEALTH_CHECK=$(curl -s https://$APP_URL/health || echo "failed")

if [[ "$HEALTH_CHECK" == *"healthy"* ]]; then
    print_info "✅ Deployment successful!"
    echo ""
    echo "App URL: https://$APP_URL"
    echo "Health Check: https://$APP_URL/health"
    echo "MCP Endpoint: https://$APP_URL/mcp"
    echo ""
    print_info "View logs with:"
    echo "  az webapp log tail --resource-group $RESOURCE_GROUP --name $APP_NAME"
    echo ""
    print_info "To configure GitHub Copilot, add this to your settings.json:"
    echo ""
    echo '{
  "github.copilot.chat.mcp.servers": {
    "azure-sdk-qa-bot": {
      "type": "sse",
      "url": "https://'$APP_URL'/sse",
      "authentication": {
        "type": "bearer",
        "token": {
          "command": "az",
          "args": ["account", "get-access-token", "--resource", "'$BACKEND_CLIENT_ID'", "--query", "accessToken", "-o", "tsv"]
        }
      }
    }
  }
}'
else
    print_warn "Health check failed. The application may still be starting up."
    print_info "Check logs with:"
    echo "  az webapp log tail --resource-group $RESOURCE_GROUP --name $APP_NAME"
fi

print_info "Deployment completed!"
