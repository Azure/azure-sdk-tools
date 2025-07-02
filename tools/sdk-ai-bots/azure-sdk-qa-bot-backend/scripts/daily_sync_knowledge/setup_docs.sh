#!/bin/bash

set -e  # Exit on error

DOCS_ROOT="docs"
LOG_FILE="docs_update.log"

log_message() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

setup_typespec_docs() {
    local repo_dir="$DOCS_ROOT/typespec"
    log_message "Setting up TypeSpec docs..."
    
    if [ -d "$repo_dir" ]; then
        log_message "Updating existing TypeSpec docs..."
        cd "$repo_dir"
        git fetch origin main
        git reset --hard origin/main
        cd ../..
    else
        mkdir -p "$DOCS_ROOT"
        cd "$DOCS_ROOT"
        git clone --filter=blob:none --sparse https://github.com/microsoft/typespec.git
        cd typespec
        git config core.sparseCheckout true
        echo "website/src/content/docs/docs" > .git/info/sparse-checkout
        git checkout main
        cd ../..
    fi
}

setup_azure_typespec_docs() {
    local repo_dir="$DOCS_ROOT/typespec-azure"
    log_message "Setting up Azure TypeSpec docs..."
    
    if [ -d "$repo_dir" ]; then
        log_message "Updating existing Azure TypeSpec docs..."
        cd "$repo_dir"
        git fetch origin main
        git reset --hard origin/main
        cd ../..
    else
        mkdir -p "$DOCS_ROOT"
        cd "$DOCS_ROOT"
        git clone --filter=blob:none --sparse https://github.com/Azure/typespec-azure.git
        cd typespec-azure
        git config core.sparseCheckout true
        echo "website/src/content/docs/docs" > .git/info/sparse-checkout
        git checkout main
        cd ../..
    fi
}

setup_azure_rest_api_wiki() {
    local repo_dir="$DOCS_ROOT/azure-rest-api-specs-wiki"
    
    log_message "Setting up Azure REST API Specs Wiki docs..."
    
    if [ -d "$repo_dir" ]; then
        log_message "Updating existing Azure REST API Specs Wiki..."
        cd "$repo_dir"
        git fetch origin master  # Wiki repos typically use 'master' branch
        git reset --hard origin/master
        cd ../..
    else
        mkdir -p "$DOCS_ROOT"
        cd "$DOCS_ROOT"
        # Wiki repositories have a .wiki.git suffix
        git clone https://github.com/Azure/azure-rest-api-specs.wiki.git
        cd ..
    fi

    log_message "Azure REST API Specs Wiki setup completed."
}

setup_azure_python_sdk_docs() {
    local repo_dir="$DOCS_ROOT/azure-sdk-for-python"
    local wiki_repo_dir="$DOCS_ROOT/azure-sdk-for-python-wiki"
    
    log_message "Setting up Azure SDK for Python documentation..."
    
    # Clone/update the main repository to get the doc folder
    if [ -d "$repo_dir" ]; then
        log_message "Updating existing Azure SDK for Python repo..."
        cd "$repo_dir"
        git fetch origin main
        git reset --hard origin/main
        cd ../..
    else
        mkdir -p "$DOCS_ROOT"
        cd "$DOCS_ROOT"
        # Use sparse checkout to only get the doc directory
        git clone --filter=blob:none --sparse https://github.com/Azure/azure-sdk-for-python.git
        cd azure-sdk-for-python
        git config core.sparseCheckout true
        echo "/doc" > .git/info/sparse-checkout
        git checkout main
        cd ../..
    fi
    
    # Clone/update the wiki repository
    if [ -d "$wiki_repo_dir" ]; then
        log_message "Updating existing Azure SDK for Python wiki..."
        cd "$wiki_repo_dir"
        git fetch origin master
        git reset --hard origin/master
        cd ../..
    else
        mkdir -p "$DOCS_ROOT"
        cd "$DOCS_ROOT"
        git clone https://github.com/Azure/azure-sdk-for-python.wiki.git
        cd ..
    fi
    
    log_message "Azure SDK for Python documentation setup completed."
}

setup_microsoft_api_guidelines() {
    local repo_dir="$DOCS_ROOT/api-guidelines"
    
    log_message "Setting up Microsoft API Guidelines docs..."
    
    if [ -d "$repo_dir" ]; then
        log_message "Updating existing Microsoft API Guidelines repo..."
        cd "$repo_dir"
        git fetch origin vNext
        git reset --hard origin/vNext
        cd ../..
    else
        mkdir -p "$DOCS_ROOT"
        cd "$DOCS_ROOT"
        git clone --filter=blob:none --sparse https://github.com/microsoft/api-guidelines.git
        cd api-guidelines
        git config core.sparseCheckout true
        echo "/azure" > .git/info/sparse-checkout
        git checkout vNext
        cd ../..
    fi
    
    log_message "Microsoft API Guidelines documentation setup completed."
}

setup_azure_resource_manager_rpc() {
    local repo_dir="$DOCS_ROOT/resource-provider-contract"
    
    log_message "Setting up Azure Resource Manager RPC docs..."
    
    if [ -d "$repo_dir" ]; then
        log_message "Updating existing Azure Resource Manager RPC repo..."
        cd "$repo_dir"
        git fetch origin master
        git reset --hard origin/master
        cd ../..
    else
        mkdir -p "$DOCS_ROOT"
        cd "$DOCS_ROOT"
        git clone --filter=blob:none --sparse git@github-microsoft:cloud-and-ai-microsoft/resource-provider-contract.git
        cd resource-provider-contract
        git config core.sparseCheckout true
        echo "/v1.0" > .git/info/sparse-checkout
        git checkout master
        cd ../..
    fi
    
    log_message "Azure Resource Manager RPC documentation setup completed."
}

setup_azure_sdk_eng_docs() {
    local auth_token="$1"
    local repo_dir="$DOCS_ROOT/azure-sdk-docs-eng.ms"  # Match actual repo name
    
    if [ -z "$auth_token" ]; then
        log_message "Error: Authentication token required for Azure DevOps access"
        return 1
    fi

    log_message "Setting up Azure SDK Engineering docs..."
    
    if [ -d "$repo_dir" ]; then
        log_message "Updating existing Azure SDK Engineering repo..."
        cd "$repo_dir"
        git fetch origin main
        git reset --hard origin/main
        cd ../..
    else
        mkdir -p "$DOCS_ROOT"
        cd "$DOCS_ROOT"
        echo "https://$auth_token@dev.azure.com/azure-sdk/internal/_git/azure-sdk-docs-eng.ms"
        git clone --filter=blob:none --sparse https://$auth_token@dev.azure.com/azure-sdk/internal/_git/azure-sdk-docs-eng.ms
        cd azure-sdk-docs-eng.ms
        git config core.sparseCheckout true
        echo "/docs" > .git/info/sparse-checkout
        git checkout main
        cd ../..
    fi

    log_message "Azure SDK Engineering documentation setup completed."
}

setup_azure_sdk_guidelines() {
    local repo_dir="$DOCS_ROOT/azure-sdk"
    
    log_message "Setting up Azure SDK Guidelines docs..."
    
    if [ -d "$repo_dir" ]; then
        log_message "Updating existing Azure SDK Guidelines repo..."
        cd "$repo_dir"
        git fetch origin main
        git reset --hard origin/main
        cd ../..
    else
        mkdir -p "$DOCS_ROOT"
        cd "$DOCS_ROOT"
        git clone --filter=blob:none --sparse https://github.com/Azure/azure-sdk.git
        cd azure-sdk
        git config core.sparseCheckout true
        echo "/docs" > .git/info/sparse-checkout
        git checkout main
        cd ../..
    fi

    log_message "Azure SDK Guidelines documentation setup completed."
}

main() {
    log_message "Starting documentation update process..."
    
    # Ensure clean state by removing existing docs
    if [ -d "$DOCS_ROOT" ]; then
        log_message "Cleaning up existing documentation..."
        rm -rf "$DOCS_ROOT"
    fi
    
    # Create fresh docs directory
    mkdir -p "$DOCS_ROOT"

    # Setup all repositories
    setup_typespec_docs
    setup_azure_typespec_docs
    setup_azure_rest_api_wiki
    setup_azure_python_sdk_docs
    setup_microsoft_api_guidelines
    setup_azure_resource_manager_rpc
    setup_azure_sdk_eng_docs "YOUR_AUTH_TOKEN"
    setup_azure_sdk_guidelines

    log_message "Documentation setup completed successfully!"

    go run setup_knowledge.go
    if [ $? -ne 0 ]; then
        log_message "Failed to run setup_knowledge.go"
        exit 1
    fi
    log_message "Knowledge setup completed successfully!"
}

main "$@"
