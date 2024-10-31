import "@typespec/http";
import "@typespec/rest";
import "@typespec/versioning";
import "@azure-tools/typespec-azure-core";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Versioning;
using Azure.Core;
using Azure.Core.Traits;

@useAuth(
  ApiKeyAuth<ApiKeyLocation.header, "api-key"> | OAuth2Auth<[
    {
      type: OAuth2FlowType.implicit,
      authorizationUrl: "https://login.contoso.com/common/oauth2/v2.0/authorize",
      scopes: ["https://widget.contoso.com/.default"],
    }
  ]>
)
@service({
  title: "Contoso Widget Manager",
})
@server(
  "{endpoint}/widget",
  "Contoso Widget APIs",
  {
    /** 
Supported Widget Services endpoints (protocol and hostname, for example:
https://westus.api.widget.contoso.com).
 */
    endpoint: string,
  }
)
@versioned(Contoso.WidgetManager.Versions)
namespace Contoso.WidgetManager;

/** The Contoso Widget Manager service version. */
enum Versions {
  /** Version 2022-08-31 */
  @useDependency(Azure.Core.Versions.v1_0_Preview_2)
  `2022-08-30`,
}

// Models ////////////////////

/** The color of a widget. */
union WidgetColor {
  string,

  /** Black Widget Color */
  Black: "Black",

  /** White Widget Color */
  White: "White",

  /** Red Widget Color */
  Red: "Red",

  /** Green Widget Color */
  Green: "Green",

  /** Blue Widget Color */
  Blue: "Blue",
}

/** A widget. */
@resource("widgets")
model Widget {
  /** The widget name. */
  @key("widgetName")
  @visibility("read")
  name: string;

  /** The widget color. */
  color: WidgetColor;

  /** The ID of the widget's manufacturer. */
  manufacturerId: string;

  ...EtagProperty;
}

/** The repair state of a widget. */
@lroStatus
union WidgetRepairState {
  string,

  /** Widget repairs succeeded. */
  Succeeded: "Succeeded",

  /** Widget repairs failed. */
  Failed: "Failed",

  /** Widget repairs were canceled. */
  Canceled: "Canceled",

  /** Widget was sent to the manufacturer. */
  SentToManufacturer: "SentToManufacturer",
}

/** A submitted repair request for a widget. */
model WidgetRepairRequest {
  /** The state of the widget repair request. */
  requestState: WidgetRepairState;

  /** The date and time when the repair is scheduled to occur. */
  scheduledDateTime: utcDateTime;

  /** The date and time when the request was created. */
  createdDateTime: utcDateTime;

  /** The date and time when the request was updated. */
  updatedDateTime: utcDateTime;

  /** The date and time when the request was completed. */
  completedDateTime: utcDateTime;
}

/** The parameters for a widget status request */
model WidgetRepairStatusParams {
  /** The ID of the widget being repaired. */
  @path
  widgetId: string;
}

/** A widget's part. */
@resource("parts")
@parentResource(Widget)
model WidgetPart {
  /** The name of the part. */
  @key("widgetPartName")
  @visibility("read")
  name: string;

  /** The ID to use for reordering the part. */
  partId: string;

  /** The ID of the part's manufacturer. */
  manufacturerId: string;

  ...EtagProperty;
}

/** The details of a reorder request for a WidgetPart. */
model WidgetPartReorderRequest {
  /** Identifies who signed off the reorder request. */
  signedOffBy: string;
}

// An example of a singleton resource
/** Provides analytics about the use and maintenance of a Widget. */
@resource("analytics")
@parentResource(Widget)
model WidgetAnalytics {
  /** The identifier for the analytics object.  There is only one named 'current'. */
  @key("analyticsId")
  @visibility("read")
  id: "current";

  /** The number of uses of the widget. */
  useCount: int64;

  /** The number of times the widget was repaired. */
  repairCount: int64;
}

/** A manufacturer of widgets. */
@resource("manufacturers")
model Manufacturer {
  /** The manufacturer's unique ID. */
  @key("manufacturerId")
  @visibility("read")
  id: string;

  /** The manufacturer's name. */
  name: string;

  /** The manufacturer's full address. */
  address: string;

  ...EtagProperty;
}

// Operations ////////////////////

alias ServiceTraits = SupportsRepeatableRequests &
  SupportsConditionalRequests &
  SupportsClientRequestId;

alias Operations = Azure.Core.ResourceOperations<ServiceTraits>;

interface Widgets {
  // Operation Status
  /** Gets status of a Widget operation. */
  @sharedRoute
  getWidgetOperationStatus is Operations.GetResourceOperationStatus<Widget>;
  /** Gets status of a Widget delete operation. */
  @sharedRoute
  getWidgetDeleteOperationStatus is Operations.GetResourceOperationStatus<Widget, never>;

  // Widget Operations
  /** Creates or updates a Widget asynchronously */
  @pollingOperation(Widgets.getWidgetOperationStatus)
  createOrUpdateWidget is Operations.LongRunningResourceCreateOrUpdate<Widget>;

  /** Get a Widget */
  getWidget is Operations.ResourceRead<Widget>;

  /** Delete a Widget asynchronously. */
  @pollingOperation(Widgets.getWidgetDeleteOperationStatus)
  deleteWidget is Operations.LongRunningResourceDelete<Widget>;

  /** List Widget resources */
  listWidgets is Operations.ResourceList<
    Widget,
    ListQueryParametersTrait<StandardListQueryParameters & SelectQueryParameter>
  >;

  // Widget Analytics
  /** Get a WidgetAnalytics */
  getAnalytics is Operations.ResourceRead<WidgetAnalytics>;

  /** Creates or updates a WidgetAnalytics */
  updateAnalytics is Operations.ResourceCreateOrUpdate<WidgetAnalytics>;

  // Widget Repair Operations
  /** Get the status of a WidgetRepairRequest. */
  #suppress "@azure-tools/typespec-azure-core/use-standard-operations" "This is a custom operation status endpoint."
  @route("/widgets/{widgetId}/repairs/{operationId}")
  getRepairStatus is Foundations.GetOperationStatus<WidgetRepairStatusParams, WidgetRepairRequest>;

  /** Schedule a widget for repairs. */
  @pollingOperation(Widgets.getWidgetOperationStatus)
  scheduleRepairs is Operations.LongRunningResourceAction<
    Widget,
    WidgetRepairRequest,
    WidgetRepairRequest & RequestIdResponseHeader
  >;
}

interface WidgetParts {
  /** Gets status of a WidgetPart operation. */
  getWidgetPartOperationStatus is Operations.GetResourceOperationStatus<WidgetPart>;

  /** Creates a WidgetPart */
  createWidgetPart is Operations.ResourceCreateWithServiceProvidedName<WidgetPart>;

  /** Get a WidgetPart */
  getWidgetPart is Operations.ResourceRead<WidgetPart>;

  /** Delete a WidgetPart */
  deleteWidgetPart is Operations.ResourceDelete<WidgetPart>;

  /** List WidgetPart resources */
  listWidgetParts is Operations.ResourceList<WidgetPart>;

  /** Reorder all parts for the widget. */
  @pollingOperation(WidgetParts.getWidgetPartOperationStatus)
  reorderParts is Operations.LongRunningResourceCollectionAction<
    WidgetPart,
    WidgetPartReorderRequest,
    never
  >;
}

interface Manufacturers {
  /** Gets status of a Manufacturer operation. */
  getManufacturerOperationStatus is Operations.GetResourceOperationStatus<Manufacturer>;

  /** Creates or replaces a Manufacturer */
  createManufacturer is Operations.ResourceCreateOrReplace<Manufacturer>;

  /** Get a Manufacturer */
  getManufacturer is Operations.ResourceRead<Manufacturer>;

  /** Delete a Manufacturer asynchronously. */
  @pollingOperation(Manufacturers.getManufacturerOperationStatus)
  deleteManufacturer is Operations.LongRunningResourceDelete<Manufacturer>;

  /** List Manufacturer resources */
  listManufacturers is Operations.ResourceList<Manufacturer>;
}

// A "global" RPC operation
/** Responds with status information about the overall service. */
@route("service-status")
op serviceStatus is RpcOperation<
  {},
  {
    statusString: string;
  },
  ServiceTraits
>;
