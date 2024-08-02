import "@typespec/rest";
import "@typespec/http";
import "@azure-tools/typespec-azure-core";

using TypeSpec.Rest;
using TypeSpec.Http;

namespace AzureSphereManagementClient;

/**
 * The intended executor of the operation; as in Resource Based Access Control
 * (RBAC) and audit logs UX. Default value is "user,system"
 */
union Origin {
  string,
  user: "user",
  system: "system",
  `user,system`: "user,system",
}

/**
 * Enum. Indicates the action type. "Internal" refers to actions that are for
 * internal only APIs.
 */
union ActionType {
  string,
  Internal: "Internal",
}

/**
 * Provisioning state of resource.
 */
union ProvisioningState {
  string,

  /**
   * Resource has been created.
   */
  Succeeded: "Succeeded",

  /**
   * Resource creation failed.
   */
  Failed: "Failed",

  /**
   * Resource creation was canceled.
   */
  Canceled: "Canceled",

  /**
   * The resource is being provisioned
   */
  Provisioning: "Provisioning",

  /**
   * The resource is being updated
   */
  Updating: "Updating",

  /**
   * The resource is being deleted
   */
  Deleting: "Deleting",

  /**
   * The resource create request has been accepted
   */
  Accepted: "Accepted",
}

/**
 * The type of identity that created the resource.
 */
union CreatedByType {
  string,
  User: "User",
  Application: "Application",
  ManagedIdentity: "ManagedIdentity",
  Key: "Key",
}

/**
 * Certificate status values.
 */
union CertificateStatus {
  string,

  /**
   * Certificate is active
   */
  Active: "Active",

  /**
   * Certificate is inactive
   */
  Inactive: "Inactive",

  /**
   * Certificate has expired
   */
  Expired: "Expired",

  /**
   * Certificate has been revoked
   */
  Revoked: "Revoked",
}

/**
 * Regional data boundary values.
 */
union RegionalDataBoundary {
  string,

  /**
   * No data boundary
   */
  None: "None",

  /**
   * EU data boundary
   */
  EU: "EU",
}

/**
 * Image type values.
 */
union ImageType {
  string,

  /**
   * Invalid image.
   */
  InvalidImageType: "InvalidImageType",

  /**
   * One Bl image type
   */
  OneBl: "OneBl",

  /**
   * Pluton image type
   */
  PlutonRuntime: "PlutonRuntime",

  /**
   * Wifi firmware image type
   */
  WifiFirmware: "WifiFirmware",

  /**
   * Security monitor image type
   */
  SecurityMonitor: "SecurityMonitor",

  /**
   * Normal world loader image type
   */
  NormalWorldLoader: "NormalWorldLoader",

  /**
   * Normal world dtb image type
   */
  NormalWorldDtb: "NormalWorldDtb",

  /**
   * Normal world kernel image type
   */
  NormalWorldKernel: "NormalWorldKernel",

  /**
   * Root FS image type
   */
  RootFs: "RootFs",

  /**
   * Services image type
   */
  Services: "Services",

  /**
   * Applications image type
   */
  Applications: "Applications",

  /**
   * FW config image type
   */
  FwConfig: "FwConfig",

  /**
   * Boot manifest image type
   */
  BootManifest: "BootManifest",

  /**
   * Nwfs image type
   */
  Nwfs: "Nwfs",

  /**
   * Trusted key store image type
   */
  TrustedKeystore: "TrustedKeystore",

  /**
   * Policy image type
   */
  Policy: "Policy",

  /**
   * Customer board config image type
   */
  CustomerBoardConfig: "CustomerBoardConfig",

  /**
   * Update certificate store image type
   */
  UpdateCertStore: "UpdateCertStore",

  /**
   * Base system update manifest image type
   */
  BaseSystemUpdateManifest: "BaseSystemUpdateManifest",

  /**
   * Firmware update manifest image type
   */
  FirmwareUpdateManifest: "FirmwareUpdateManifest",

  /**
   * Customer update manifest image type
   */
  CustomerUpdateManifest: "CustomerUpdateManifest",

  /**
   * Recovery manifest image type
   */
  RecoveryManifest: "RecoveryManifest",

  /**
   * manifest set image type
   */
  ManifestSet: "ManifestSet",

  /**
   * Other image type
   */
  Other: "Other",
}

/**
 * OS feed type values.
 */
union OSFeedType {
  string,

  /**
   * Retail OS feed type.
   */
  Retail: "Retail",

  /**
   * Retail evaluation OS feed type.
   */
  RetailEval: "RetailEval",
}

/**
 * Update policy values.
 */
union UpdatePolicy {
  string,

  /**
   * Update all policy.
   */
  UpdateAll: "UpdateAll",

  /**
   * No update for 3rd party app policy.
   */
  No3rdPartyAppUpdates: "No3rdPartyAppUpdates",
}

/**
 * Allow crash dumps values.
 */
union AllowCrashDumpCollection {
  string,

  /**
   * Crash dump collection enabled
   */
  Enabled: "Enabled",

  /**
   * Crash dump collection disabled
   */
  Disabled: "Disabled",
}

/**
 * Capability image type
 */
union CapabilityType {
  string,

  /**
   * Application development capability
   */
  ApplicationDevelopment: "ApplicationDevelopment",

  /**
   * Field servicing capability
   */
  FieldServicing: "FieldServicing",
}

/**
 * A list of REST API operations supported by an Azure Resource Provider. It
 * contains an URL link to get the next set of results.
 */
model OperationListResult is Azure.Core.Page<Operation>;

/**
 * Details of a REST API operation, returned from the Resource Provider Operations
 * API
 */
model Operation {
  /**
   * The name of the operation, as per Resource-Based Access Control (RBAC).
   * Examples: "Microsoft.Compute/virtualMachines/write",
   * "Microsoft.Compute/virtualMachines/capture/action"
   */
  @visibility("read")
  name?: string;

  /**
   * Whether the operation applies to data-plane. This is "true" for data-plane
   * operations and "false" for ARM/control-plane operations.
   */
  @visibility("read")
  isDataAction?: boolean;

  /**
   * Localized display information for this particular operation.
   */
  display?: OperationDisplay;

  /**
   * The intended executor of the operation; as in Resource Based Access Control
   * (RBAC) and audit logs UX. Default value is "user,system"
   */
  @visibility("read")
  origin?: Origin;

  /**
   * Enum. Indicates the action type. "Internal" refers to actions that are for
   * internal only APIs.
   */
  @visibility("read")
  actionType?: ActionType;
}

/**
 * Localized display information for this particular operation.
 */
model OperationDisplay {
  /**
   * The localized friendly form of the resource provider name, e.g. "Microsoft
   * Monitoring Insights" or "Microsoft Compute".
   */
  @visibility("read")
  provider?: string;

  /**
   * The localized friendly name of the resource type related to this operation.
   * E.g. "Virtual Machines" or "Job Schedule Collections".
   */
  @visibility("read")
  resource?: string;

  /**
   * The concise, localized friendly name for the operation; suitable for dropdowns.
   * E.g. "Create or Update Virtual Machine", "Restart Virtual Machine".
   */
  @visibility("read")
  operation?: string;

  /**
   * The short, localized friendly description of the operation; suitable for tool
   * tips and detailed views.
   */
  @visibility("read")
  description?: string;
}

/**
 * Common error response for all Azure Resource Manager APIs to return error
 * details for failed operations. (This also follows the OData error response
 * format.).
 */
@error
model ErrorResponse {
  /**
   * The error object.
   */
  error?: ErrorDetail;
}

/**
 * The error detail.
 */
model ErrorDetail {
  /**
   * The error code.
   */
  @visibility("read")
  code?: string;

  /**
   * The error message.
   */
  @visibility("read")
  message?: string;

  /**
   * The error target.
   */
  @visibility("read")
  target?: string;

  /**
   * The error details.
   */
  @visibility("read")
  @OpenAPI.extension("x-ms-identifiers", ["message", "target"])
  details?: ErrorDetail[];

  /**
   * The error additional info.
   */
  @visibility("read")
  @OpenAPI.extension("x-ms-identifiers", [])
  additionalInfo?: ErrorAdditionalInfo[];
}

/**
 * The resource management error additional info.
 */
model ErrorAdditionalInfo {
  /**
   * The additional info type.
   */
  @visibility("read")
  type?: string;

  /**
   * The additional info.
   */
  @visibility("read")
  info?: Record<unknown>;
}

/**
 * The response of a Catalog list operation.
 */
model CatalogListResult is Azure.Core.Page<Catalog>;

/**
 * An Azure Sphere catalog
 */
model Catalog extends TrackedResource {
  /**
   * The resource-specific properties for this resource.
   */
  @visibility("read", "create")
  properties?: CatalogProperties;
}

/**
 * Catalog properties
 */
model CatalogProperties {
  /**
   * The Azure Sphere tenant ID associated with the catalog.
   */
  @visibility("read")
  tenantId?: string;

  /**
   * The status of the last operation.
   */
  @visibility("read")
  provisioningState?: ProvisioningState;
}

/**
 * The resource model definition for an Azure Resource Manager tracked top level
 * resource which has 'tags' and a 'location'
 */
model TrackedResource extends Resource {
  /**
   * Resource tags.
   */
  @visibility("read", "create", "update")
  tags?: Record<string>;

  /**
   * The geo-location where the resource lives
   */
  @visibility("read", "create")
  location: string;
}

/**
 * Common fields that are returned in the response for all Azure Resource Manager
 * resources
 */
model Resource {
  /**
   * Fully qualified resource ID for the resource. Ex -
   * /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{resourceType}/{resourceName}
   */
  @visibility("read")
  id?: string;

  /**
   * The name of the resource
   */
  @visibility("read")
  name?: string;

  /**
   * The type of the resource. E.g. "Microsoft.Compute/virtualMachines" or
   * "Microsoft.Storage/storageAccounts"
   */
  @visibility("read")
  type?: string;

  /**
   * Azure Resource Manager metadata containing createdBy and modifiedBy information.
   */
  @visibility("read")
  systemData?: SystemData;
}

/**
 * Metadata pertaining to creation and last modification of the resource.
 */
model SystemData {
  /**
   * The identity that created the resource.
   */
  createdBy?: string;

  /**
   * The type of identity that created the resource.
   */
  createdByType?: CreatedByType;

  /**
   * The timestamp of resource creation (UTC).
   */
  // FIXME: (utcDateTime) Please double check that this is the correct type for your scenario.
  createdAt?: utcDateTime;

  /**
   * The identity that last modified the resource.
   */
  lastModifiedBy?: string;

  /**
   * The type of identity that last modified the resource.
   */
  lastModifiedByType?: CreatedByType;

  /**
   * The timestamp of resource last modification (UTC)
   */
  // FIXME: (utcDateTime) Please double check that this is the correct type for your scenario.
  lastModifiedAt?: utcDateTime;
}

/**
 * The type used for update operations of the Catalog.
 */
model CatalogUpdate {
  /**
   * Resource tags.
   */
  tags?: Record<string>;
}

/**
 * The response of a Certificate list operation.
 */
model CertificateListResult is Azure.Core.Page<Certificate>;

/**
 * An certificate resource belonging to a catalog resource.
 */
model Certificate extends ProxyResource {
  /**
   * The resource-specific properties for this resource.
   */
  @visibility("read", "create")
  properties?: CertificateProperties;
}

/**
 * The properties of certificate
 */
model CertificateProperties {
  /**
   * The certificate as a UTF-8 encoded base 64 string.
   */
  @visibility("read")
  certificate?: string;

  /**
   * The certificate status.
   */
  @visibility("read")
  status?: CertificateStatus;

  /**
   * The certificate subject.
   */
  @visibility("read")
  subject?: string;

  /**
   * The certificate thumbprint.
   */
  @visibility("read")
  thumbprint?: string;

  /**
   * The certificate expiry date.
   */
  @visibility("read")
  // FIXME: (utcDateTime) Please double check that this is the correct type for your scenario.
  expiryUtc?: utcDateTime;

  /**
   * The certificate not before date.
   */
  @visibility("read")
  // FIXME: (utcDateTime) Please double check that this is the correct type for your scenario.
  notBeforeUtc?: utcDateTime;

  /**
   * The status of the last operation.
   */
  @visibility("read")
  provisioningState?: ProvisioningState;
}

/**
 * The resource model definition for a Azure Resource Manager proxy resource. It
 * will not have tags and a location
 */
model ProxyResource extends Resource {}

/**
 * The certificate chain response.
 */
model CertificateChainResponse {
  /**
   * The certificate chain.
   */
  @visibility("read")
  certificateChain?: string;
}

/**
 * Request for the proof of possession nonce
 */
model ProofOfPossessionNonceRequest {
  /**
   * The proof of possession nonce
   */
  proofOfPossessionNonce: string;
}

/**
 * Result of the action to generate a proof of possession nonce
 */
model ProofOfPossessionNonceResponse extends CertificateProperties {}

/**
 * Response to the action call for count devices in a catalog.
 */
model CountDevicesResponse extends CountElementsResponse {}

/**
 * Response of the count for elements.
 */
model CountElementsResponse {
  /**
   * Number of children resources in parent resource.
   */
  value: int32;
}

/**
 * The response of a Image list operation.
 */
model ImageListResult is Azure.Core.Page<Image>;

/**
 * An image resource belonging to a catalog resource.
 */
model Image extends ProxyResource {
  /**
   * The resource-specific properties for this resource.
   */
  @visibility("read", "create")
  properties?: ImageProperties;
}

/**
 * The properties of image
 */
model ImageProperties {
  /**
   * Image as a UTF-8 encoded base 64 string on image create. This field contains
   * the image URI on image reads.
   */
  @visibility("read", "create")
  image?: string;

  /**
   * Image ID
   */
  @visibility("read", "create")
  imageId?: string;

  /**
   * Image name
   */
  @visibility("read")
  imageName?: string;

  /**
   * Regional data boundary for an image
   */
  @visibility("read", "create")
  regionalDataBoundary?: RegionalDataBoundary;

  /**
   * Location the image
   */
  @visibility("read")
  uri?: string;

  /**
   * The image description.
   */
  @visibility("read")
  description?: string;

  /**
   * The image component id.
   */
  @visibility("read")
  componentId?: string;

  /**
   * The image type.
   */
  @visibility("read")
  imageType?: ImageType;

  /**
   * The status of the last operation.
   */
  @visibility("read")
  provisioningState?: ProvisioningState;
}

/**
 * The response of a Deployment list operation.
 */
model DeploymentListResult is Azure.Core.Page<Deployment>;

/**
 * An deployment resource belonging to a device group resource.
 */
model Deployment extends ProxyResource {
  /**
   * The resource-specific properties for this resource.
   */
  @visibility("read", "create")
  properties?: DeploymentProperties;
}

/**
 * The properties of deployment
 */
model DeploymentProperties {
  /**
   * Deployment ID
   */
  @visibility("read", "create")
  deploymentId?: string;

  /**
   * Images deployed
   */
  @visibility("read", "create")
  deployedImages?: Image[];

  /**
   * Deployment date UTC
   */
  @visibility("read")
  // FIXME: (utcDateTime) Please double check that this is the correct type for your scenario.
  deploymentDateUtc?: utcDateTime;

  /**
   * The status of the last operation.
   */
  @visibility("read")
  provisioningState?: ProvisioningState;
}

/**
 * Request of the action to list device groups for a catalog.
 */
model ListDeviceGroupsRequest {
  /**
   * Device Group name.
   */
  deviceGroupName?: string;
}

/**
 * The response of a DeviceGroup list operation.
 */
model DeviceGroupListResult is Azure.Core.Page<DeviceGroup>;

/**
 * An device group resource belonging to a product resource.
 */
model DeviceGroup extends ProxyResource {
  /**
   * The resource-specific properties for this resource.
   */
  @visibility("read", "create")
  properties?: DeviceGroupProperties;
}

/**
 * The properties of deviceGroup
 */
model DeviceGroupProperties {
  /**
   * Description of the device group.
   */
  description?: string;

  /**
   * Operating system feed type of the device group.
   */
  osFeedType?: OSFeedType;

  /**
   * Update policy of the device group.
   */
  updatePolicy?: UpdatePolicy;

  /**
   * Flag to define if the user allows for crash dump collection.
   */
  allowCrashDumpsCollection?: AllowCrashDumpCollection;

  /**
   * Regional data boundary for the device group.
   */
  regionalDataBoundary?: RegionalDataBoundary;

  /**
   * Deployment status for the device group.
   */
  @visibility("read")
  hasDeployment?: boolean;

  /**
   * The status of the last operation.
   */
  @visibility("read")
  provisioningState?: ProvisioningState;
}

/**
 * Paged collection of DeviceInsight items
 */
model PagedDeviceInsight is Azure.Core.Page<DeviceInsight>;

/**
 * Device insight report.
 */
model DeviceInsight {
  /**
   * Device ID
   */
  deviceId: string;

  /**
   * Event description
   */
  description: string;

  /**
   * Event start timestamp
   */
  // FIXME: (utcDateTime) Please double check that this is the correct type for your scenario.
  startTimestampUtc: utcDateTime;

  /**
   * Event end timestamp
   */
  // FIXME: (utcDateTime) Please double check that this is the correct type for your scenario.
  endTimestampUtc: utcDateTime;

  /**
   * Event category
   */
  eventCategory: string;

  /**
   * Event class
   */
  eventClass: string;

  /**
   * Event type
   */
  eventType: string;

  /**
   * Event count
   */
  eventCount: int32;
}

/**
 * The response of a Device list operation.
 */
model DeviceListResult is Azure.Core.Page<Device>;

/**
 * An device resource belonging to a device group resource.
 */
model Device extends ProxyResource {
  /**
   * The resource-specific properties for this resource.
   */
  @visibility("read", "create")
  properties?: DeviceProperties;
}

/**
 * The properties of device
 */
model DeviceProperties {
  /**
   * Device ID
   */
  @visibility("read", "create")
  deviceId?: string;

  /**
   * SKU of the chip
   */
  @visibility("read")
  chipSku?: string;

  /**
   * OS version available for installation when update requested
   */
  @visibility("read")
  lastAvailableOsVersion?: string;

  /**
   * OS version running on device when update requested
   */
  @visibility("read")
  lastInstalledOsVersion?: string;

  /**
   * Time when update requested and new OS version available
   */
  @visibility("read")
  // FIXME: (utcDateTime) Please double check that this is the correct type for your scenario.
  lastOsUpdateUtc?: utcDateTime;

  /**
   * Time when update was last requested
   */
  @visibility("read")
  // FIXME: (utcDateTime) Please double check that this is the correct type for your scenario.
  lastUpdateRequestUtc?: utcDateTime;

  /**
   * The status of the last operation.
   */
  @visibility("read")
  provisioningState?: ProvisioningState;
}

/**
 * The response of a Product list operation.
 */
model ProductListResult is Azure.Core.Page<Product>;

/**
 * An product resource belonging to a catalog resource.
 */
model Product extends ProxyResource {
  /**
   * The resource-specific properties for this resource.
   */
  @visibility("read", "create")
  properties?: ProductProperties;
}

/**
 * The properties of product
 */
model ProductProperties {
  /**
   * Description of the product
   */
  description?: string;

  /**
   * The status of the last operation.
   */
  @visibility("read")
  provisioningState?: ProvisioningState;
}

/**
 * The type used for update operations of the Product.
 */
model ProductUpdate {
  /**
   * The updatable properties of the Product.
   */
  properties?: ProductUpdateProperties;
}

/**
 * The updatable properties of the Product.
 */
model ProductUpdateProperties {
  /**
   * Description of the product
   */
  description?: string;
}

/**
 * The type used for update operations of the DeviceGroup.
 */
model DeviceGroupUpdate {
  /**
   * The updatable properties of the DeviceGroup.
   */
  properties?: DeviceGroupUpdateProperties;
}

/**
 * The updatable properties of the DeviceGroup.
 */
model DeviceGroupUpdateProperties {
  /**
   * Description of the device group.
   */
  description?: string;

  /**
   * Operating system feed type of the device group.
   */
  osFeedType?: OSFeedType;

  /**
   * Update policy of the device group.
   */
  updatePolicy?: UpdatePolicy;

  /**
   * Flag to define if the user allows for crash dump collection.
   */
  allowCrashDumpsCollection?: AllowCrashDumpCollection;

  /**
   * Regional data boundary for the device group.
   */
  regionalDataBoundary?: RegionalDataBoundary;
}

/**
 * Request to the action call to bulk claim devices.
 */
model ClaimDevicesRequest {
  /**
   * Device identifiers of the devices to be claimed.
   */
  deviceIdentifiers: string[];
}

/**
 * The type used for update operations of the Device.
 */
model DeviceUpdate {
  /**
   * The updatable properties of the Device.
   */
  properties?: DeviceUpdateProperties;
}

/**
 * The updatable properties of the Device.
 */
model DeviceUpdateProperties {
  /**
   * Device group id
   */
  deviceGroupId?: string;
}

/**
 * Request of the action to create a signed device capability image
 */
model GenerateCapabilityImageRequest {
  /**
   * List of capabilities to create
   */
  capabilities: CapabilityType[];
}

/**
 * Signed device capability image response
 */
model SignedCapabilityImageResponse {
  /**
   * The signed device capability image as a UTF-8 encoded base 64 string.
   */
  @visibility("read")
  image?: string;
}

/**
 * Response to the action call for count devices in a catalog (preview API).
 */
model CountDeviceResponse extends CountElementsResponse {}
