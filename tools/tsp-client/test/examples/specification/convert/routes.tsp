import "@azure-tools/typespec-azure-core";
import "@typespec/rest";
import "./models.tsp";

using TypeSpec.Rest;
using TypeSpec.Http;

namespace AzureSphereManagementClient;

interface OperationsOperations {
  /**
   * List the operations for the provider
   */
  @route("/providers/Microsoft.AzureSphere/operations")
  @get
  list is Azure.Core.Foundations.Operation<{}, OperationListResult>;
}

interface CatalogsOperations {
  /**
   * List Catalog resources by subscription ID
   */
  @route("/subscriptions/{subscriptionId}/providers/Microsoft.AzureSphere/catalogs")
  @get
  listBySubscription is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;
    },
    CatalogListResult
  >;

  /**
   * List Catalog resources by resource group
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs")
  @get
  listByResourceGroup is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;
    },
    CatalogListResult
  >;

  /**
   * Get a Catalog
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}")
  @get
  get is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;
    },
    Catalog
  >;

  /**
   * Create a Catalog
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}")
  @put
  createOrUpdate is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Resource create parameters.
       */
      @body
      resource: Catalog;
    },
    Catalog
  >;

  /**
   * Update a Catalog
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}")
  @patch
  update is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * The resource properties to be updated.
       */
      @body
      properties: CatalogUpdate;
    },
    Catalog
  >;

  /**
   * Delete a Catalog
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}")
  @delete
  delete is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;
    },
    void
  >;

  /**
   * Counts devices in catalog.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/countDevices")
  @post
  countDevices is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;
    },
    CountDevicesResponse
  >;

  /**
   * Lists deployments for catalog.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/listDeployments")
  @post
  listDeployments is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Filter the result list using the given expression
       */
      @query("$filter")
      filter?: string;

      /**
       * The number of result items to return.
       */
      @query("$top")
      top?: int32;

      /**
       * The number of result items to skip.
       */
      @query("$skip")
      skip?: int32;

      /**
       * The maximum number of result items per page.
       */
      @query("$maxpagesize")
      maxpagesize?: int32;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;
    },
    DeploymentListResult
  >;

  /**
   * List the device groups for the catalog.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/listDeviceGroups")
  @post
  listDeviceGroups is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Filter the result list using the given expression
       */
      @query("$filter")
      filter?: string;

      /**
       * The number of result items to return.
       */
      @query("$top")
      top?: int32;

      /**
       * The number of result items to skip.
       */
      @query("$skip")
      skip?: int32;

      /**
       * The maximum number of result items per page.
       */
      @query("$maxpagesize")
      maxpagesize?: int32;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * List device groups for catalog.
       */
      @body
      listDeviceGroupsRequest: ListDeviceGroupsRequest;
    },
    DeviceGroupListResult
  >;

  /**
   * Lists device insights for catalog.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/listDeviceInsights")
  @post
  listDeviceInsights is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Filter the result list using the given expression
       */
      @query("$filter")
      filter?: string;

      /**
       * The number of result items to return.
       */
      @query("$top")
      top?: int32;

      /**
       * The number of result items to skip.
       */
      @query("$skip")
      skip?: int32;

      /**
       * The maximum number of result items per page.
       */
      @query("$maxpagesize")
      maxpagesize?: int32;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;
    },
    PagedDeviceInsight
  >;

  /**
   * Lists devices for catalog.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/listDevices")
  @post
  listDevices is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Filter the result list using the given expression
       */
      @query("$filter")
      filter?: string;

      /**
       * The number of result items to return.
       */
      @query("$top")
      top?: int32;

      /**
       * The number of result items to skip.
       */
      @query("$skip")
      skip?: int32;

      /**
       * The maximum number of result items per page.
       */
      @query("$maxpagesize")
      maxpagesize?: int32;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;
    },
    DeviceListResult
  >;

  /**
   * Creates an image. Use this action when the image ID is unknown.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/uploadImage")
  @post
  uploadImage is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Image upload request body.
       */
      @body
      uploadImageRequest: Image;
    },
    void
  >;
}

interface CertificatesOperations {
  /**
   * List Certificate resources by Catalog
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/certificates")
  @get
  listByCatalog is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Filter the result list using the given expression
       */
      @query("$filter")
      filter?: string;

      /**
       * The number of result items to return.
       */
      @query("$top")
      top?: int32;

      /**
       * The number of result items to skip.
       */
      @query("$skip")
      skip?: int32;

      /**
       * The maximum number of result items per page.
       */
      @query("$maxpagesize")
      maxpagesize?: int32;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;
    },
    CertificateListResult
  >;

  /**
   * Get a Certificate
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/certificates/{serialNumber}")
  @get
  get is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Serial number of the certificate. Use '.default' to get current active
       * certificate.
       */
      @path
      serialNumber: string;
    },
    Certificate
  >;

  /**
   * Retrieves cert chain.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/certificates/{serialNumber}/retrieveCertChain")
  @post
  retrieveCertChain is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Serial number of the certificate. Use '.default' to get current active
       * certificate.
       */
      @path
      serialNumber: string;
    },
    CertificateChainResponse
  >;

  /**
   * Gets the proof of possession nonce.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/certificates/{serialNumber}/retrieveProofOfPossessionNonce")
  @post
  retrieveProofOfPossessionNonce is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Serial number of the certificate. Use '.default' to get current active
       * certificate.
       */
      @path
      serialNumber: string;

      /**
       * Proof of possession nonce request body
       */
      @body
      proofOfPossessionNonceRequest: ProofOfPossessionNonceRequest;
    },
    ProofOfPossessionNonceResponse
  >;
}

interface ImagesOperations {
  /**
   * List Image resources by Catalog
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/images")
  @get
  listByCatalog is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Filter the result list using the given expression
       */
      @query("$filter")
      filter?: string;

      /**
       * The number of result items to return.
       */
      @query("$top")
      top?: int32;

      /**
       * The number of result items to skip.
       */
      @query("$skip")
      skip?: int32;

      /**
       * The maximum number of result items per page.
       */
      @query("$maxpagesize")
      maxpagesize?: int32;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;
    },
    ImageListResult
  >;

  /**
   * Get a Image
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/images/{imageName}")
  @get
  get is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Image name. Use an image GUID for GA versions of the API.
       */
      @path
      imageName: string;
    },
    Image
  >;

  /**
   * Create a Image
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/images/{imageName}")
  @put
  createOrUpdate is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Image name. Use an image GUID for GA versions of the API.
       */
      @path
      imageName: string;

      /**
       * Resource create parameters.
       */
      @body
      resource: Image;
    },
    Image
  >;

  /**
   * Delete a Image
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/images/{imageName}")
  @delete
  delete is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Image name. Use an image GUID for GA versions of the API.
       */
      @path
      imageName: string;
    },
    void
  >;
}

interface ProductsOperations {
  /**
   * List Product resources by Catalog
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products")
  @get
  listByCatalog is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;
    },
    ProductListResult
  >;

  /**
   * Get a Product. '.default' and '.unassigned' are system defined values and
   * cannot be used for product name.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}")
  @get
  get is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;
    },
    Product
  >;

  /**
   * Create a Product. '.default' and '.unassigned' are system defined values and
   * cannot be used for product name.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}")
  @put
  createOrUpdate is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;

      /**
       * Resource create parameters.
       */
      @body
      resource: Product;
    },
    Product
  >;

  /**
   * Update a Product. '.default' and '.unassigned' are system defined values and
   * cannot be used for product name.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}")
  @patch
  update is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;

      /**
       * The resource properties to be updated.
       */
      @body
      properties: ProductUpdate;
    },
    Product | void
  >;

  /**
   * Delete a Product. '.default' and '.unassigned' are system defined values and
   * cannot be used for product name'
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}")
  @delete
  delete is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;
    },
    void
  >;

  /**
   * Counts devices in product. '.default' and '.unassigned' are system defined
   * values and cannot be used for product name.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}/countDevices")
  @post
  countDevices is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;
    },
    CountDevicesResponse
  >;

  /**
   * Generates default device groups for the product. '.default' and '.unassigned'
   * are system defined values and cannot be used for product name.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}/generateDefaultDeviceGroups")
  @post
  generateDefaultDeviceGroups is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;
    },
    DeviceGroupListResult
  >;
}

interface DeviceGroupsOperations {
  /**
   * List DeviceGroup resources by Product. '.default' and '.unassigned' are system
   * defined values and cannot be used for product name.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}/deviceGroups")
  @get
  listByProduct is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Filter the result list using the given expression
       */
      @query("$filter")
      filter?: string;

      /**
       * The number of result items to return.
       */
      @query("$top")
      top?: int32;

      /**
       * The number of result items to skip.
       */
      @query("$skip")
      skip?: int32;

      /**
       * The maximum number of result items per page.
       */
      @query("$maxpagesize")
      maxpagesize?: int32;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;
    },
    DeviceGroupListResult
  >;

  /**
   * Get a DeviceGroup. '.default' and '.unassigned' are system defined values and
   * cannot be used for product or device group name.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}/deviceGroups/{deviceGroupName}")
  @get
  get is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;

      /**
       * Name of device group.
       */
      @pattern("^[A-Za-z0-9]{1,2}$|^[A-Za-z0-9][A-Za-z0-9\\s]{1,48}[A-Za-z0-9]$|^\\.default$|^\\.unassigned$")
      @path
      deviceGroupName: string;
    },
    DeviceGroup
  >;

  /**
   * Create a DeviceGroup. '.default' and '.unassigned' are system defined values
   * and cannot be used for product or device group name.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}/deviceGroups/{deviceGroupName}")
  @put
  createOrUpdate is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;

      /**
       * Name of device group.
       */
      @pattern("^[A-Za-z0-9]{1,2}$|^[A-Za-z0-9][A-Za-z0-9\\s]{1,48}[A-Za-z0-9]$|^\\.default$|^\\.unassigned$")
      @path
      deviceGroupName: string;

      /**
       * Resource create parameters.
       */
      @body
      resource: DeviceGroup;
    },
    DeviceGroup
  >;

  /**
   * Update a DeviceGroup. '.default' and '.unassigned' are system defined values
   * and cannot be used for product or device group name.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}/deviceGroups/{deviceGroupName}")
  @patch
  update is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;

      /**
       * Name of device group.
       */
      @pattern("^[A-Za-z0-9]{1,2}$|^[A-Za-z0-9][A-Za-z0-9\\s]{1,48}[A-Za-z0-9]$|^\\.default$|^\\.unassigned$")
      @path
      deviceGroupName: string;

      /**
       * The resource properties to be updated.
       */
      @body
      properties: DeviceGroupUpdate;
    },
    DeviceGroup | void
  >;

  /**
   * Delete a DeviceGroup. '.default' and '.unassigned' are system defined values
   * and cannot be used for product or device group name.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}/deviceGroups/{deviceGroupName}")
  @delete
  delete is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;

      /**
       * Name of device group.
       */
      @pattern("^[A-Za-z0-9]{1,2}$|^[A-Za-z0-9][A-Za-z0-9\\s]{1,48}[A-Za-z0-9]$|^\\.default$|^\\.unassigned$")
      @path
      deviceGroupName: string;
    },
    void
  >;

  /**
   * Bulk claims the devices. Use '.unassigned' or '.default' for the device group
   * and product names when bulk claiming devices to a catalog only.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}/deviceGroups/{deviceGroupName}/claimDevices")
  @post
  claimDevices is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;

      /**
       * Name of device group.
       */
      @pattern("^[A-Za-z0-9]{1,2}$|^[A-Za-z0-9][A-Za-z0-9\\s]{1,48}[A-Za-z0-9]$|^\\.default$|^\\.unassigned$")
      @path
      deviceGroupName: string;

      /**
       * Bulk claim devices request body.
       */
      @body
      claimDevicesRequest: ClaimDevicesRequest;
    },
    void
  >;

  /**
   * Counts devices in device group. '.default' and '.unassigned' are system defined
   * values and cannot be used for product or device group name.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}/deviceGroups/{deviceGroupName}/countDevices")
  @post
  countDevices is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;

      /**
       * Name of device group.
       */
      @pattern("^[A-Za-z0-9]{1,2}$|^[A-Za-z0-9][A-Za-z0-9\\s]{1,48}[A-Za-z0-9]$|^\\.default$|^\\.unassigned$")
      @path
      deviceGroupName: string;
    },
    CountDevicesResponse
  >;
}

interface DeploymentsOperations {
  /**
   * List Deployment resources by DeviceGroup. '.default' and '.unassigned' are
   * system defined values and cannot be used for product or device group name.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}/deviceGroups/{deviceGroupName}/deployments")
  @get
  listByDeviceGroup is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Filter the result list using the given expression
       */
      @query("$filter")
      filter?: string;

      /**
       * The number of result items to return.
       */
      @query("$top")
      top?: int32;

      /**
       * The number of result items to skip.
       */
      @query("$skip")
      skip?: int32;

      /**
       * The maximum number of result items per page.
       */
      @query("$maxpagesize")
      maxpagesize?: int32;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;

      /**
       * Name of device group.
       */
      @pattern("^[A-Za-z0-9]{1,2}$|^[A-Za-z0-9][A-Za-z0-9\\s]{1,48}[A-Za-z0-9]$|^\\.default$|^\\.unassigned$")
      @path
      deviceGroupName: string;
    },
    DeploymentListResult
  >;

  /**
   * Get a Deployment. '.default' and '.unassigned' are system defined values and
   * cannot be used for product or device group name.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}/deviceGroups/{deviceGroupName}/deployments/{deploymentName}")
  @get
  get is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;

      /**
       * Name of device group.
       */
      @pattern("^[A-Za-z0-9]{1,2}$|^[A-Za-z0-9][A-Za-z0-9\\s]{1,48}[A-Za-z0-9]$|^\\.default$|^\\.unassigned$")
      @path
      deviceGroupName: string;

      /**
       * Deployment name. Use .default for deployment creation and to get the current
       * deployment for the associated device group.
       */
      @path
      deploymentName: string;
    },
    Deployment
  >;

  /**
   * Create a Deployment. '.default' and '.unassigned' are system defined values and
   * cannot be used for product or device group name.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}/deviceGroups/{deviceGroupName}/deployments/{deploymentName}")
  @put
  createOrUpdate is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;

      /**
       * Name of device group.
       */
      @pattern("^[A-Za-z0-9]{1,2}$|^[A-Za-z0-9][A-Za-z0-9\\s]{1,48}[A-Za-z0-9]$|^\\.default$|^\\.unassigned$")
      @path
      deviceGroupName: string;

      /**
       * Deployment name. Use .default for deployment creation and to get the current
       * deployment for the associated device group.
       */
      @path
      deploymentName: string;

      /**
       * Resource create parameters.
       */
      @body
      resource: Deployment;
    },
    Deployment
  >;

  /**
   * Delete a Deployment. '.default' and '.unassigned' are system defined values and
   * cannot be used for product or device group name.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}/deviceGroups/{deviceGroupName}/deployments/{deploymentName}")
  @delete
  delete is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;

      /**
       * Name of device group.
       */
      @pattern("^[A-Za-z0-9]{1,2}$|^[A-Za-z0-9][A-Za-z0-9\\s]{1,48}[A-Za-z0-9]$|^\\.default$|^\\.unassigned$")
      @path
      deviceGroupName: string;

      /**
       * Deployment name. Use .default for deployment creation and to get the current
       * deployment for the associated device group.
       */
      @path
      deploymentName: string;
    },
    void
  >;
}

interface DevicesOperations {
  /**
   * List Device resources by DeviceGroup. '.default' and '.unassigned' are system
   * defined values and cannot be used for product or device group name.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}/deviceGroups/{deviceGroupName}/devices")
  @get
  listByDeviceGroup is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;

      /**
       * Name of device group.
       */
      @pattern("^[A-Za-z0-9]{1,2}$|^[A-Za-z0-9][A-Za-z0-9\\s]{1,48}[A-Za-z0-9]$|^\\.default$|^\\.unassigned$")
      @path
      deviceGroupName: string;
    },
    DeviceListResult
  >;

  /**
   * Get a Device. Use '.unassigned' or '.default' for the device group and product
   * names when a device does not belong to a device group and product.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}/deviceGroups/{deviceGroupName}/devices/{deviceName}")
  @get
  get is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;

      /**
       * Name of device group.
       */
      @pattern("^[A-Za-z0-9]{1,2}$|^[A-Za-z0-9][A-Za-z0-9\\s]{1,48}[A-Za-z0-9]$|^\\.default$|^\\.unassigned$")
      @path
      deviceGroupName: string;

      /**
       * Device name
       */
      @pattern("^[a-zA-Z0-9-]{128}$")
      @path
      deviceName: string;
    },
    Device
  >;

  /**
   * Create a Device. Use '.unassigned' or '.default' for the device group and
   * product names to claim a device to the catalog only.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}/deviceGroups/{deviceGroupName}/devices/{deviceName}")
  @put
  createOrUpdate is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;

      /**
       * Name of device group.
       */
      @pattern("^[A-Za-z0-9]{1,2}$|^[A-Za-z0-9][A-Za-z0-9\\s]{1,48}[A-Za-z0-9]$|^\\.default$|^\\.unassigned$")
      @path
      deviceGroupName: string;

      /**
       * Device name
       */
      @pattern("^[a-zA-Z0-9-]{128}$")
      @path
      deviceName: string;

      /**
       * Resource create parameters.
       */
      @body
      resource: Device;
    },
    Device
  >;

  /**
   * Update a Device. Use '.unassigned' or '.default' for the device group and
   * product names to move a device to the catalog level.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}/deviceGroups/{deviceGroupName}/devices/{deviceName}")
  @patch
  update is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;

      /**
       * Name of device group.
       */
      @pattern("^[A-Za-z0-9]{1,2}$|^[A-Za-z0-9][A-Za-z0-9\\s]{1,48}[A-Za-z0-9]$|^\\.default$|^\\.unassigned$")
      @path
      deviceGroupName: string;

      /**
       * Device name
       */
      @pattern("^[a-zA-Z0-9-]{128}$")
      @path
      deviceName: string;

      /**
       * The resource properties to be updated.
       */
      @body
      properties: DeviceUpdate;
    },
    Device | void
  >;

  /**
   * Delete a Device
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}/deviceGroups/{deviceGroupName}/devices/{deviceName}")
  @delete
  delete is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;

      /**
       * Name of device group.
       */
      @pattern("^[A-Za-z0-9]{1,2}$|^[A-Za-z0-9][A-Za-z0-9\\s]{1,48}[A-Za-z0-9]$|^\\.default$|^\\.unassigned$")
      @path
      deviceGroupName: string;

      /**
       * Device name
       */
      @pattern("^[a-zA-Z0-9-]{128}$")
      @path
      deviceName: string;
    },
    void
  >;

  /**
   * Generates the capability image for the device. Use '.unassigned' or '.default'
   * for the device group and product names to generate the image for a device that
   * does not belong to a specific device group and product.
   */
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.AzureSphere/catalogs/{catalogName}/products/{productName}/deviceGroups/{deviceGroupName}/devices/{deviceName}/generateCapabilityImage")
  @post
  generateCapabilityImage is Azure.Core.Foundations.Operation<
    {
      /**
       * The ID of the target subscription.
       */
      @minLength(1)
      @path
      subscriptionId: string;

      /**
       * The name of the resource group. The name is case insensitive.
       */
      @maxLength(90)
      @minLength(1)
      @path
      resourceGroupName: string;

      /**
       * Name of catalog
       */
      @pattern("^[A-Za-z0-9_-]{1,50}$")
      @path
      catalogName: string;

      /**
       * Name of product.
       */
      @pattern("^[\\w][\\w\\s]{1,48}[\\w]$|^\\.default$|^\\.unassigned$")
      @path
      productName: string;

      /**
       * Name of device group.
       */
      @pattern("^[A-Za-z0-9]{1,2}$|^[A-Za-z0-9][A-Za-z0-9\\s]{1,48}[A-Za-z0-9]$|^\\.default$|^\\.unassigned$")
      @path
      deviceGroupName: string;

      /**
       * Device name
       */
      @pattern("^[a-zA-Z0-9-]{128}$")
      @path
      deviceName: string;

      /**
       * Generate capability image request body.
       */
      @body
      generateDeviceCapabilityRequest: GenerateCapabilityImageRequest;
    },
    SignedCapabilityImageResponse | void
  >;
}
