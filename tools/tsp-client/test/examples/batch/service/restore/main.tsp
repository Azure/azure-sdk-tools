import "@typespec/rest";
import "@typespec/http";
import "@typespec/versioning";
import "@azure-tools/typespec-azure-core";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Versioning;
using Azure.Core;

/**
 * The key vault client performs cryptographic key operations and vault operations against the Key Vault service.
 */
@useAuth(
  OAuth2Auth<[
    {
      type: OAuth2FlowType.implicit,
      authorizationUrl: "https://login.microsoftonline.com/common/oauth2/authorize",
      scopes: ["https://vault.azure.net/.default"],
    }
  ]>
)
@service(#{ title: "KeyVaultClient" })
@versioned(Versions)
@server(
  "{vaultBaseUrl}",
  "The key vault client performs cryptographic key operations and vault operations against the Key Vault service.",
  {
    vaultBaseUrl: url,
  }
)
namespace KeyVault.RBAC;

/**
 * The available API versions.
 */
enum Versions {
  /**
   * The 2025-07-01 API version.
   */
  v2025_07_01: "2025-07-01",
}

/**
 * Full backup operation
 */
model FullBackupOperation {
  /**
   * Status of the backup operation.
   */
  status?: Foundations.OperationStatus;

  /**
   * The status details of backup operation.
   */
  statusDetails?: string;

  /**
   * Error encountered, if any, during the full backup operation.
   */
  #suppress "@azure-tools/typespec-azure-core/no-nullable" "Intentionally nullable model"
  error?: Foundations.Error;

  /**
   * The start time of the backup operation in UTC
   */
  @encode("unixTimestamp", int32)
  startTime?: utcDateTime;

  /**
   * The end time of the backup operation in UTC
   */
  #suppress "@azure-tools/typespec-azure-core/no-nullable" "Intentionally nullable attribute"
  @encode("unixTimestamp", int32)
  endTime?: utcDateTime | null;

  /**
   * Identifier for the full backup operation.
   */
  jobId?: string;

  /**
   * The Azure blob storage container Uri which contains the full backup
   */
  azureStorageBlobContainerUri?: string;
}

/**
 * Returns the status of full backup operation
 */
#suppress "@azure-tools/typespec-azure-core/use-standard-operations" "Foundations.Operation is necessary for Key Vault"
#suppress "@azure-tools/typespec-azure-core/use-standard-names" "Operation name is already established"
@route("/backup/{jobId}/pending")
@get
op fullBackupStatus is Foundations.Operation<
  {
    /**
     * The id returned as part of the backup request
     */
    @path
    jobId: string;
  },
  FullBackupOperation
>;
