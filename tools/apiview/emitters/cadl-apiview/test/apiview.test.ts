import { resolvePath } from "@cadl-lang/compiler";
import { expectDiagnosticEmpty } from "@cadl-lang/compiler/testing";
import "@cadl-lang/versioning";
import { strictEqual } from "assert";
import { ApiViewEmitterOptions } from "../src/lib.js";
import { createApiViewTestRunner } from "./test-host.js";

describe("apiview: tests", () => {
  async function rawApiViewFor(code: string, options: ApiViewEmitterOptions): Promise<string> {
    const runner = await createApiViewTestRunner({withVersioning: true});

    const outPath = resolvePath("/apiview.json");

    const diagnostics = await runner.diagnose(code, {
      noEmit: false,
      emitters: { "@azure-tools/cadl-apiview": { ...options, "output-file": outPath } },
    });
    expectDiagnosticEmpty(diagnostics);

    return runner.fs.get(outPath)!;
  }

  it("describes enums", async () => {
    const output = await rawApiViewFor(`
    @versioned(Versions)
    @Cadl.serviceTitle("Enum Test")
    namespace Azure.Test {

      enum Versions {
        version1: "version1",
        version2: "version2"
      }

      enum SomeEnum {
        Plain,
        "Literal",
      }

      enum SomeStringEnum {
        A: "A",
        B: "B",
      }

      namespace BuildingBlocks {
        model Block is string;

        model Thing {
          someInt: SomeIntEnum;
        }
      }

      enum SomeIntEnum {
        A: 1,
        B: 2,
      }
    }
    `,
      {}
    );
    strictEqual(output, "TODO");
  });

  it("describes baseline cadl-sample", async () => {
    const output = await rawApiViewFor(baseline, {});
    strictEqual(output, "TODO");
  });
});

const baseline = `
import "@cadl-lang/rest";
import "@cadl-lang/versioning";
import "@azure-tools/cadl-azure-core";

using Cadl.Http;
using Cadl.Rest;
using Cadl.Versioning;
using Azure.Core;

@Cadl.serviceTitle("Contoso Widget Manager")
@serviceVersion("2022-05-15-preview")
@versioned(Contoso.WidgetManager.Versions)
@versionedDependency(
  [[Contoso.WidgetManager.Versions.v2022_08_31, Azure.Core.Versions.v1_0_Preview_1]]
)
@route("/api")
namespace Contoso.WidgetManager;

enum Versions {
  v2022_08_31: "2022-08-31",
}

// Models ////////////////////

@doc("The color of a widget.")
@knownValues(WidgetColorValues)
model WidgetColor is string;

enum WidgetColorValues {
  Black,
  White,
  Red,
  Green,
  Blue,
}

@doc("A widget.")
@resource("widgets")
model Widget {
  @key("widgetName")
  @doc("The widget name.")
  @visibility("read")
  name: string;

  @doc("The widget color.")
  color: WidgetColor;

  @doc("The ID of the widget's manufacturer.")
  manufacturerId: string;
}

@doc("The repair state of a widget.")
@knownValues(WidgetRepairStateValues)
model WidgetRepairState is string;

@lroStatus
enum WidgetRepairStateValues {
  Succeeded,
  Failed,
  Canceled,
  SentToManufacturer,
}

@doc("A submitted repair request for a widget.")
model WidgetRepairRequest {
  @doc("The state of the widget repair request.")
  requestState: WidgetRepairState;

  @doc("The date and time when the repair is scheduled to occur.")
  scheduledDateTime: zonedDateTime;

  @doc("The date and time when the request was created.")
  createdDateTime: zonedDateTime;

  @doc("The date and time when the request was updated.")
  updatedDateTime: zonedDateTime;

  @doc("The date and time when the request was completed.")
  completedDateTime: zonedDateTime;
}

@doc("Status of a widget repair request.")
@resource("repairs")
model WidgetRepairStatus {
  @key
  @doc("The ID of the repair request.")
  requestId: string;

  @doc("The widget being repaired.")
  widgetName: string;

  @doc("The state of the widget repair request.")
  requestState: WidgetRepairState;
}

@doc("A widget's part.")
@resource("parts")
@parentResource(Widget)
model WidgetPart {
  @key("widgetPartName")
  @doc("The name of the part.")
  @visibility("read")
  name: string;

  @doc("The ID to use for reordering the part.")
  partId: string;

  @doc("The ID of the part's manufacturer.")
  manufacturerId: string;
}

model WidgetPartReorderRequest {
  @doc("Identifies who signed off the reorder request.")
  signedOffBy: string;
}

@resource("manufacturers")
model Manufacturer {
  @key("manufacturerId")
  @doc("The manufacturer's unique ID.")
  id: string;

  @doc("The manufacturer's name.")
  name: string;

  @doc("The manufacturer's full address.")
  address: string;
}

model ListQueryParams {
  @doc("The number of items to return.")
  @query
  top?: int32;

  @doc("The number of items to skip.")
  @query
  skip?: int32;

  @doc("The maximum number of items per page.")
  @query
  maxPageSize?: int32;
}

// Operations ////////////////////

interface Widgets {
  // Widget Operations
  createOrUpdateWidget is LongRunningResourceCreateOrUpdate<Widget>;
  getWidget is ResourceRead<Widget>;
  deleteWidget is LongRunningResourceDelete<Widget>;
  listWidgets is ResourceList<
    Widget,
    {
      parameters: ListQueryParams;
    }
  >;

  // Repair Status Operations
  getRepairRequestStatus is ResourceRead<WidgetRepairStatus>;
  listRepairRequests is ResourceList<WidgetRepairStatus>;

  // Widget Actions
  @doc("Schedule a widget for repairs.")
  // @pollingOperation(Widgets.getRepairRequestStatus, { widgetName: ResponseProperty<"widgetName">, requestId: ResponseProperty<"requestId"> })
  // @finalOperation(Widgets.getWidget, { widgetName: ResponseProperty<"widgetName"> })
  scheduleRepairs is ResourceAction<
    Widget,
    WidgetRepairRequest,
    // WidgetRepairStatus & Cadl.Http.AcceptedResponse & Foundations.LongRunningStatusLocation
    Cadl.Http.AcceptedResponse & Foundations.LongRunningStatusLocation
  >;
}

interface WidgetParts {
  createWidgetPart is ResourceCreateWithServiceProvidedName<WidgetPart>;
  getWidgetPart is ResourceRead<WidgetPart>;
  deleteWidgetPart is ResourceDelete<WidgetPart>;
  listWidgetParts is ResourceList<WidgetPart>;

  @doc("Reorder all parts for the widget.")
  reorderParts is ResourceCollectionAction<
    WidgetPart,
    WidgetPartReorderRequest,
    Cadl.Http.AcceptedResponse & Foundations.LongRunningStatusLocation
  >;
}

interface Manufacturers {
  createManufacturer is ResourceCreateOrReplace<Manufacturer>;
  getManufacturer is ResourceRead<Manufacturer>;
  deleteManufacturer is LongRunningResourceDelete<Manufacturer>;
  listManufacturers is NonPagedResourceList<Manufacturer>;
}
`;
