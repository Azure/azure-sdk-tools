// DONT specify @useDependency to get a diagnostic reported

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
  // @useDependency(Azure.Core.Versions.v1_0_Preview_2)
  `2022-08-30`,
}

/** A widget. */
@resource("widgets")
model Widget {
  /** The widget name. */
  @key("widgetName")
  @visibility("read")
  name: string;

  /** The widget color. */
  color: string;

  /** The ID of the widget's manufacturer. */
  manufacturerId: string;
}

alias ServiceTraits = SupportsRepeatableRequests &
  SupportsConditionalRequests &
  SupportsClientRequestId;

alias Operations = Azure.Core.ResourceOperations<ServiceTraits>;

interface Widgets {
    getWidget is Operations.ResourceRead<Widget>;
}