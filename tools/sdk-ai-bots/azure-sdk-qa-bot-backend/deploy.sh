#!/bin/bash
set -e

# Default deployment settings
DEFAULT_TAG=$(date "+%Y%m%d%H%M%S")  # Use timestamp as default tag
DEFAULT_ENVIRONMENT="dev"  # Default to dev environment

# Parse command-line arguments
while getopts "t:e:" flag; do
  case "${flag}" in
    t) IMAGE_TAG="${OPTARG}" ;;
    e) ENVIRONMENT="${OPTARG}" ;;
    *) ;;
  esac
done

# Set defaults if not provided
IMAGE_TAG=${IMAGE_TAG:-$DEFAULT_TAG}
ENVIRONMENT=${ENVIRONMENT:-$DEFAULT_ENVIRONMENT}

# Clean tag by removing any invalid characters for container registry
IMAGE_TAG=$(echo "$IMAGE_TAG" | tr '/' '-')

# Function to show usage
show_usage() {
    echo "Usage: $0 [-t image_tag] [-e environment]"
    echo "  -t: Image tag (default: timestamp)"
    echo "  -e: Environment - 'dev', 'preview', or 'prod' (default: dev)"
    echo ""
    echo "Examples:"
    echo "  $0 -e dev -t v1.0.0       # Deploy to dev environment"
    echo "  $0 -e preview -t v1.0.0   # Deploy to prod preview slot"
    echo "  $0 -e prod -t v1.0.0      # Deploy directly to prod"
    exit 1
}

# Validate environment
if [[ "$ENVIRONMENT" != "dev" && "$ENVIRONMENT" != "prod" && "$ENVIRONMENT" != "preview" ]]; then
    echo "Error: Invalid environment. Use 'dev', 'preview', or 'prod'"
    show_usage
fi

# Configure environment-specific settings
configure_environment() {
    case "$ENVIRONMENT" in
        "dev")
            RESOURCE_GROUP="azure-sdk-qa-bot-dev"
            ACR_NAME="azuresdkqabotdevcontainer"
            ACR_RESOURCE_GROUP="azure-sdk-qa-bot-dev"
            APP_NAME="azuresdkqabot-dev-server"
            IMAGE_NAME="azure-sdk-qa-bot-backend"
            echo "Configuring for DEV environment..."
            ;;
        "prod")
            RESOURCE_GROUP="azure-sdk-qa-bot"
            ACR_NAME="azuresdkqabotcontainer"
            ACR_RESOURCE_GROUP="azure-sdk-qa-bot"
            APP_NAME="azuresdkqabot-server"
            IMAGE_NAME="azure-sdk-qa-bot-backend"
            echo "Configuring for PRODUCTION environment..."
            ;;
        "preview")
            RESOURCE_GROUP="azure-sdk-qa-bot-test"
            ACR_NAME="azuresdkqabotcontainer"
            ACR_RESOURCE_GROUP="azure-sdk-qa-bot"
            APP_NAME="azuresdkqabot-test-server"
            IMAGE_NAME="azure-sdk-qa-bot-backend"
            echo "Configuring for PREVIEW environment..."
            ;;
    esac
}

# Deploy based on environment
deploy_application() {
    case "$ENVIRONMENT" in
        "dev")
            echo "Updating image in dev environment..."
            az webapp config container set --name ${APP_NAME} --resource-group ${RESOURCE_GROUP} --container-image-name ${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${IMAGE_TAG} --container-registry-url https://${ACR_LOGIN_SERVER}

            # Restart the webapp to apply changes
            az webapp restart --name ${APP_NAME} --resource-group ${RESOURCE_GROUP}
            echo "Dev deployment completed successfully!"
            ;;
        "preview")
            echo "Updating image in preview environment..."
            az webapp config container set --name ${APP_NAME} --resource-group ${RESOURCE_GROUP} --container-image-name ${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${IMAGE_TAG} --container-registry-url https://${ACR_LOGIN_SERVER}

            # Restart the webapp to apply changes
            az webapp restart --name ${APP_NAME} --resource-group ${RESOURCE_GROUP}
            echo "Preview deployment completed successfully!"
            ;;
        "prod")
            echo "Updating image in production environment..."
            az webapp config container set --name ${APP_NAME} --resource-group ${RESOURCE_GROUP} --container-image-name ${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${IMAGE_TAG} --container-registry-url https://${ACR_LOGIN_SERVER}

            # Restart the webapp to apply changes
            az webapp restart --name ${APP_NAME} --resource-group ${RESOURCE_GROUP}
            echo "Production deployment completed successfully!"
            ;;
    esac
}

# Configure environment settings
configure_environment

echo "Environment: $ENVIRONMENT"
echo "Using image tag: $IMAGE_TAG"

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo "Azure CLI is not installed. Please install it first."
    exit 1
fi

# Login to Azure
az login

echo "Logging into Azure Container Registry..."
az acr login --name $ACR_NAME --resource-group $ACR_RESOURCE_GROUP

# Get the login server name for the ACR
ACR_LOGIN_SERVER=$(az acr show --name $ACR_NAME --resource-group $ACR_RESOURCE_GROUP --query "loginServer" --output tsv)
echo "ACR Login Server: $ACR_LOGIN_SERVER"

# Handle production deployment (skip build, just check image exists)
if [[ "$ENVIRONMENT" == "prod" ]]; then
    echo "Production deployment: Checking if image exists in registry..."
    
    # Check if the image exists in the container registry
    if az acr repository show --name $ACR_NAME --repository $IMAGE_NAME --tag $IMAGE_TAG --resource-group $ACR_RESOURCE_GROUP >/dev/null 2>&1; then
        echo "Image ${IMAGE_NAME}:${IMAGE_TAG} found in registry. Proceeding with production deployment..."
    else
        echo "Error: Image ${IMAGE_NAME}:${IMAGE_TAG} not found in registry."
        echo "Please ensure the image has been built and tested in preview environment first."
        exit 1
    fi
    
    # Deploy to production
    deploy_application
    exit 0
fi

# For dev and preview environments: Clean up Docker resources to free up space
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

# Execute deployment
deploy_application

echo "Deployment completed successfully!"
