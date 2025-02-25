# Bicep
The bicep templates in the [bicep](./bicep) folder are used to provision the PipelineWitness resource group, web app and storage account as well as the PipelineLogs resource group, storage account, kusto cluster and all of the resources required to make continuous ingestion from storage to kusto work.

#### Deployment permissions
The bicep templates contain RBAC assignments between the web app's managed identity and the storage accounts it will be accessing.  For the CI pipeline to be able to successfully set these RBAC assignments, it's service connection principal must be granted `Microsoft.Authorization/roleAssignments/write` to the storage resources.  This permission is included in the `Owner`, `User Access Administrator` and `Role Based Access Control Administrator` roles.


# Kusto
The scripts in the kusto folder are deployed to the staging and production databases during ci pipeline runs.  They are merged into a single `.kql` by [`deploy.ps1`](./deploy.ps1) using the [`Merge-KustoScripts.ps1`](./Merge-KustoScripts.ps1) script.  To extract objects from an existing kusto database into the kusto folder, use the [`Extract-KustoScripts.ps1`](./Extract-KustoScripts.ps1) script.
