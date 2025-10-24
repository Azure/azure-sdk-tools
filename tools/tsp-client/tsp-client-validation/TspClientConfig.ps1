# TSP-Client Validation Pipeline Configuration
# This file contains the mapping of languages to their Azure DevOps pipeline IDs

$PipelineConfig = @{
    "Python" = @{
        PipelineId = 7519
        Repository = "azure-sdk-for-python"
        PRPattern = "Azure/azure-sdk-for-python/pull/"
        ValidationName = "SDK Validation - Python"
    }
    "NET" = @{
        PipelineId = 7516
        Repository = "azure-sdk-for-net" 
        PRPattern = "Azure/azure-sdk-for-net/pull/"
        ValidationName = "SDK Validation - .NET"
    }
    "Java" = @{
        PipelineId = 7515
        Repository = "azure-sdk-for-java"
        PRPattern = "Azure/azure-sdk-for-java/pull/"
        ValidationName = "SDK Validation - Java"
    }
    "JS" = @{
        PipelineId = 7518
        Repository = "azure-sdk-for-js"
        PRPattern = "Azure/azure-sdk-for-js/pull/"
        ValidationName = "SDK Validation - JavaScript"
    }
    "Go" = @{
        PipelineId = 7517
        Repository = "azure-sdk-for-go"
        PRPattern = "Azure/azure-sdk-for-go/pull/"
        ValidationName = "SDK Validation - Go"
    }
}

# Azure DevOps Configuration
$AzureDevOpsConfig = @{
    Organization = "https://dev.azure.com/azure-sdk"
    ProjectId = "29ec6040-b234-4e31-b139-33dc4287b756"
    ProjectName = "public"
}

# Expected branch naming pattern for sync branches
$SyncBranchPattern = "sync-eng/common-update-tsp-client-{0}"

# Export the configuration
Export-ModuleMember -Variable PipelineConfig, AzureDevOpsConfig, SyncBranchPattern