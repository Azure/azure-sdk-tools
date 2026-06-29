import "@typespec/rest";
import "@typespec/http";
import "@azure-tools/typespec-azure-core";
import "@azure-tools/typespec-azure-resource-manager";

using TypeSpec.Rest;
using TypeSpec.Http;
using Azure.Core;
using Azure.ResourceManager;

namespace Microsoft.Contoso;

/** Employee resource */
model Employee is TrackedResource<EmployeeProperties> {
  ...ResourceNameParameter<Employee>;
}

/** Employee properties */
model EmployeeProperties {
  /** Age of employee */
  age?: int32;

  /** City of employee */
  city?: string;

  /** Profile of employee */
  @encode("base64url")
  profile?: bytes;

  /** The status of the last operation. */
  @visibility(Lifecycle.Read)
  provisioningState?: ProvisioningState;
}

/** The resource provisioning state. */
@lroStatus
union ProvisioningState {
  ResourceProvisioningState,

  /** The resource is being provisioned */
  Provisioning: "Provisioning",

  /** The resource is updating */
  Updating: "Updating",

  /** The resource is being deleted */
  Deleting: "Deleting",

  /** The resource create request has been accepted */
  Accepted: "Accepted",

  string,
}

model ResourceSku {
  /**
   * The name of the SKU. Required.
   *
   * Allowed values: Standard_S1, Free_F1, Premium_P1, Premium_P2
   */
  name: string;

  /**
   * Optional tier of this particular SKU. 'Standard' or 'Free'.
   *
   * `Basic` is deprecated, use `Standard` instead.
   */
  @renamedFrom(Versions.v2025_08_01_preview, "tier")
  tierNew?: WebPubSubSkuTier;

  /**
   * Not used. Retained for future use.
   */
  @visibility(Lifecycle.Read)
  size?: string;

  /**
   * Not used. Retained for future use.
   */
  @visibility(Lifecycle.Read)
  family?: string;

  /**
   * Optional, integer. The unit count of the resource.
   * 1 for Free_F1/Standard_S1/Premium_P1, 100 for Premium_P2 by default.
   *
   * If present, following values are allowed:
   *     Free_F1: 1;
   *     Standard_S1: 1,2,3,4,5,6,7,8,9,10,20,30,40,50,60,70,80,90,100;
   *     Premium_P1:  1,2,3,4,5,6,7,8,9,10,20,30,40,50,60,70,80,90,100;
   *     Premium_P2:  100,200,300,400,500,600,700,800,900,1000;
   */
  @typeChangedFrom(Versions.v2021_11_01, int32)
  capacity?: string;
}

@armResourceOperations
interface Employees {
  get is ArmResourceRead<Employee>;
  createOrUpdate is ArmResourceCreateOrReplaceAsync<Employee>;
  update is ArmResourcePatchSync<Employee, EmployeeProperties>;
  delete is ArmResourceDeleteWithoutOkAsync<Employee>;
  listByResourceGroup is ArmResourceListByParent<Employee>;
  listBySubscription is ArmListBySubscription<Employee>;
}
