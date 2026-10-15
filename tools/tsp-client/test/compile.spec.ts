import { joinPaths } from "@typespec/compiler";
import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { stringify } from "yaml";
import { Logger } from "../src/log.js";
import { compileTsp } from "../src/typespec.js";

const emitterPackage = "@azure-tools/typespec-autorest";
const service = `
import "@typespec/http";
using TypeSpec.Http;

@service(#{ title: "Widget Service" })
namespace WidgetService;

model Widget {
  id: string;
}

@get
@route("/widgets/{id}")
op getWidget(@path id: string): Widget;
`;

describe("SDK generation lint policy", () => {
  let projectDir: string;
  let configText: string;

  beforeEach(async () => {
    projectDir = await mkdtemp(joinPaths(process.cwd(), ".tsp-client-compile-"));
    configText = stringify({
      "warn-as-error": true,
      linter: {
        extends: ["@azure-tools/specs-typespec-rulesets/data-plane"],
        enable: {
          "@azure-tools/specs-typespec-rulesets/authoring-only": true,
        },
      },
      options: {
        [emitterPackage]: {
          "emitter-output-dir": "{output-dir}/generated",
          "output-file": "widgets.json",
        },
      },
    });
    await writeFile(joinPaths(projectDir, "tspconfig.yaml"), configText);
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    await rm(projectDir, { recursive: true, force: true });
  });

  async function compileService(source: string) {
    const entrypoint = joinPaths(projectDir, "client.tsp");
    await writeFile(entrypoint, source);
    return compileTsp({
      emitterPackage,
      outputPath: projectDir,
      resolvedMainFilePath: entrypoint,
    });
  }

  it("emits a copied service without its authoring ruleset dependencies", async () => {
    const result = await compileService(service);

    expect(result.success).toBe(true);
    const document = JSON.parse(
      await readFile(joinPaths(projectDir, "generated/widgets.json"), "utf8"),
    );
    expect(document.paths["/widgets/{id}"].get.operationId).toBe("GetWidget");
    expect(await readFile(joinPaths(projectDir, "tspconfig.yaml"), "utf8")).toBe(configText);
  });

  it("still fails on compiler errors", async () => {
    const errors = vi.spyOn(Logger, "error").mockImplementation(() => {});

    const result = await compileService(`${service}\nmodel Invalid { value: MissingType; }`);

    expect(result.success).toBe(false);
    expect(errors).toHaveBeenCalledWith(expect.stringContaining("invalid-ref"));
  });

  it("still fails on library validation errors", async () => {
    const errors = vi.spyOn(Logger, "error").mockImplementation(() => {});

    const result = await compileService(service.replace("/widgets/{id}", "/widgets/{missing}"));

    expect(result.success).toBe(false);
    expect(errors).toHaveBeenCalledWith(
      expect.stringContaining("@typespec/http/missing-uri-param"),
    );
  });
});
