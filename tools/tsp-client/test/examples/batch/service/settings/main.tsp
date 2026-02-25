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
 * A Key Vault account setting.
 */
model Setting {
  /**
   * The account setting to be updated
   */
  name: string;

  /**
   * The value of the pool setting.
   */
  value: string;
}

/**
 * Retrieves the setting object of a specified setting name.
 */
#suppress "@azure-tools/typespec-azure-core/use-standard-operations" "Foundations.Operation is necessary for Key Vault"
@summary("Get specified account setting object.")
@route("/settings/{settingName}")
@get
op getSetting is Foundations.Operation<
  {
    /**
     * The name of the account setting. Must be a valid settings option.
     */
    @path
    settingName: string;
  },
  Setting
>;
