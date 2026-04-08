import "./main.tsp";
import "@azure-tools/typespec-client-generator-core";

using Azure.Samples.CrossLanguage;
using Azure.ClientGenerator.Core;

// ---------------------------------------------------------------------------
// Python client customizations
// ---------------------------------------------------------------------------

@@clientName(Widgets, "WidgetClient", "python");
@@clientName(Widget, "WidgetItem", "python");

// ---------------------------------------------------------------------------
// JavaScript / TypeScript client customizations
// ---------------------------------------------------------------------------

@@clientName(Widgets, "WidgetsClient", "javascript");

// ---------------------------------------------------------------------------
// Java client customizations
// ---------------------------------------------------------------------------

@@clientName(Widgets, "WidgetsClient", "java");

// ---------------------------------------------------------------------------
// C# (.NET) client customizations
// ---------------------------------------------------------------------------

@@clientName(Widgets, "WidgetServiceClient", "csharp");
