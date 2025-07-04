#!/bin/bash
set -e

# Azure configuration - REPLACE THESE PLACEHOLDERS with your actual resource names
RESOURCE_GROUP="typespec_helper"
ACR_NAME="azuresdkqabot"
APP_NAME="azuresdkbot"
APP_SLOT_DEV_NAME="azuresdkbot-dev"
APP_SLOT_PREVIEW_NAME="preview"
IMAGE_NAME="azure-sdk-qa-bot-backend"

# Default deployment settings
DEFAULT_TAG=$(date "+%Y%m%d%H%M%S")  # Use timestamp as default tag
DEFAULT_MODE="slot"  # Default to slot deployment (options: slot, prod)

# Parse command-line arguments
while getopts "t:m:" flag; do
  case "${flag}" in
    t) IMAGE_TAG="${OPTARG}" ;;
    m) DEPLOY_MODE="${OPTARG}" ;;
    *) ;;
  esac
done

APP_SLOT_NAME=${APP_SLOT_DEV_NAME}
if [[ "$DEPLOY_MODE" == "preview" ]]; then
    APP_SLOT_NAME=${APP_SLOT_PREVIEW_NAME}
fi

# Handle production deployment if requested
if [[ "$DEPLOY_MODE" == "prod" ]]; then
    echo "Performing slot swap to deploy to production..."
    # Swap the deployment slot with production
    az webapp deployment slot swap --resource-group ${RESOURCE_GROUP} --name ${APP_NAME} --slot ${APP_SLOT_NAME}
    echo "Production deployment completed successfully!"
    exit 0
fi

# Set defaults if not provided
IMAGE_TAG=${IMAGE_TAG:-$DEFAULT_TAG}
DEPLOY_MODE=${DEPLOY_MODE:-$DEFAULT_MODE}

# Clean tag by removing any invalid characters for container registry
IMAGE_TAG=$(echo "$IMAGE_TAG" | tr '/' '-')

echo "Using image tag: $IMAGE_TAG"
echo "Deployment mode: $DEPLOY_MODE"

# Validate deployment mode
if [[ "$DEPLOY_MODE" != "preview" && "$DEPLOY_MODE" != "slot" && "$DEPLOY_MODE" != "prod" ]]; then
    echo "Error: Invalid deployment mode. Use 'slot' or 'prod'"
    echo "Usage: $0 [-t image_tag] [-m deployment_mode]"
    echo "  -t: Image tag (default: latest)"
    echo "  -m: Deployment mode - 'slot' for slot deployment only, 'prod' for production deployment with slot swap (default: slot)"
    exit 1
fi

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo "Azure CLI is not installed. Please install it first."
    exit 1
fi

# Login to Azure
az login

echo "Logging into Azure Container Registry..."
az acr login --name $ACR_NAME

# Get the login server name for the ACR
ACR_LOGIN_SERVER=$(az acr show --name $ACR_NAME --resource-group $RESOURCE_GROUP --query "loginServer" --output tsv)
echo "ACR Login Server: $ACR_LOGIN_SERVER"

# Clean up Docker resources to free up space
echo "Cleaning up Docker resources to free up disk space..."
docker system prune -f
docker image prune -a -f

# Verify disk space after cleanup
echo "Disk space after cleanup:"
df -h

# Build and tag the Docker image
echo "Building and tagging Docker image..."
docker build -t ${IMAGE_NAME}:${IMAGE_TAG} .
docker tag ${IMAGE_NAME}:${IMAGE_TAG} ${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${IMAGE_TAG}

# Push the image to ACR
echo "Pushing image to Azure Container Registry..."
docker push ${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${IMAGE_TAG}

# Update webapp image tag in the deployment slot
echo "Updating image in deployment slot ${APP_SLOT_NAME}..."
az webapp config container set --name ${APP_NAME} --slot ${APP_SLOT_NAME} --resource-group ${RESOURCE_GROUP} --container-image-name ${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${IMAGE_TAG} --container-registry-url https://${ACR_LOGIN_SERVER}

# Restart the webapp to apply changes
az webapp restart  --name azuresdkbot --slot azuresdkbot-dev --resource-group typespec_helper
echo "Slot deployment completed successfully!"