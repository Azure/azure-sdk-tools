import "@typespec/rest";
import "@typespec/http";
import "@azure-tools/typespec-azure-core";
import "@azure-tools/typespec-azure-resource-manager";

using TypeSpec.Rest;
using TypeSpec.Http;
using Azure.Core;
using Azure.ResourceManager;

namespace Microsoft.Widget;

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

  /** The absolute URL of the employee's profile page. */
  profileUri?: string;

  /** The status of the last operation. */
  @visibility(Lifecycle.Read)
  provisioningState?: ProvisioningState;
}

/** Details for relocating an employee to another city. */
model EmployeeRelocateContent {
  /** The city to which the employee will be relocated. */
  city: string;

  /** The reason for relocating the employee. */
  reason?: string;
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

  string,
}

@armResourceOperations
interface Employees {
  get is ArmResourceRead<Employee>;
  createOrUpdate is ArmResourceCreateOrReplaceAsync<Employee>;
  update is ArmResourcePatchSync<Employee, EmployeeProperties>;
  delete is ArmResourceDeleteWithoutOkAsync<Employee>;
  listByResourceGroup is ArmResourceListByParent<Employee,
  Parameters = {
      /**
       * The number of items to be included in the result.
       */
      @query("$top")
      $top?: int32;

      /**
       * The filter to apply.
       */
      @query("$filter")
      $filter?: string;
    },>;
  listBySubscription is ArmListBySubscription<Employee>;

  relocate is ArmResourceActionSync<
    Employee,
    EmployeeRelocateContent,
    Employee
  >;
}
