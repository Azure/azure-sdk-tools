import "@typespec/http";
import "@typespec/rest";
import "@typespec/versioning";
import "@azure-tools/typespec-azure-core";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Versioning;
using Azure.Core;
using Azure.Core.Traits;

@useAuth(AadOauth2Auth<["https://management.azure.com/.default"]>)
@service(#{ title: "Azure Sample Widget Service" })
@versioned(Azure.Samples.CrossLanguage.Versions)
namespace Azure.Samples.CrossLanguage;

@doc("API versions for the Widget service.")
enum Versions {
  @doc("2024-01-01 stable release.")
  v2024_01_01: "2024-01-01",
}

// ---------------------------------------------------------------------------
// Models
// ---------------------------------------------------------------------------

@doc("The color of a widget.")
union WidgetColor {
  string,

  @doc("Red widget.")
  Red: "Red",

  @doc("Green widget.")
  Green: "Green",

  @doc("Blue widget.")
  Blue: "Blue",
}

@doc("A widget resource.")
@resource("widgets")
model Widget {
  @key("widgetId")
  @doc("The unique identifier for the widget.")
  @visibility(Lifecycle.Read)
  id: string;

  @doc("The display name of the widget.")
  name: string;

  @doc("The color of the widget.")
  color: WidgetColor;

  @doc("The description of the widget.")
  description?: string;

  @doc("The manufacturing date of the widget.")
  manufacturedOn?: utcDateTime;
}

@doc("Parameters for listing widgets.")
model WidgetListParams {
  @doc("Filter widgets by color.")
  @query
  color?: WidgetColor;

  @doc("Maximum number of results to return.")
  @query
  maxResults?: int32;
}

@doc("The result of a widget analysis operation.")
model WidgetAnalysisResult {
  @doc("The widget that was analyzed.")
  widgetId: string;

  @doc("Quality score from 0 to 100.")
  qualityScore: float32;

  @doc("Human readable summary of the analysis.")
  summary: string;
}

// ---------------------------------------------------------------------------
// Operations
// ---------------------------------------------------------------------------

@doc("Operations on Widget resources.")
interface Widgets {
  @doc("Get a widget by ID.")
  getWidget is ResourceRead<Widget>;

  @doc("Create or replace a widget.")
  createOrReplaceWidget is ResourceCreateOrReplace<Widget>;

  @doc("Update specific fields of a widget.")
  updateWidget is ResourceCreateOrUpdate<Widget>;

  @doc("Delete a widget.")
  deleteWidget is ResourceDelete<Widget>;

  @doc("List all widgets.")
  listWidgets is ResourceList<Widget, ListQueryParametersTrait<WidgetListParams>>;

  @doc("Analyze a widget for quality metrics.")
  @action("analyze")
  analyzeWidget is ResourceAction<Widget, {}, WidgetAnalysisResult>;
}
