import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { compile, formatDiagnostic, listServices, NodeHost } from "@typespec/compiler";
import { getHeaderFieldName, getHttpOperation } from "@typespec/http";
import {
  Availability, getAddedOnVersions, getAvailabilityMap, getRenamedFrom,
} from "@typespec/versioning";
import {
  getArmCommonTypesVersion, getArmResources,
} from "@azure-tools/typespec-azure-resource-manager";
import {
  createTCGCContext, getAccessOverride,
} from "@azure-tools/typespec-client-generator-core";

const state = process.argv[2] ?? "expected";
assert.ok(["input", "expected"].includes(state), "Usage: node check-discussion-case.mjs [input|expected]");
const contract = JSON.parse(fs.readFileSync("discussion-contract.json", "utf8"));
const stable = "stable/2021-11-01/widget.json";
const preview = "preview/2024-10-01-preview/widget.json";
const collection = "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Widget/employees";
const instance = collection + "/{employeeName}";

function pointer(value, key) {
  assert.ok(key === "" || key.startsWith("/"), `Invalid JSON pointer: ${key}`);
  return key === "" ? value : key.slice(1).split("/").reduce(
    (current, segment) => current?.[segment.replaceAll("~1", "/").replaceAll("~0", "~")], value,
  );
}

function type(program, name, kind) {
  const [result, diagnostics] = program.resolveTypeReference(name);
  assert.equal(diagnostics.length, 0, `Cannot resolve ${name}`);
  assert.equal(result?.kind, kind, `${name} must be a reachable ${kind}.`);
  return result;
}

function assertCompiles(program) {
  assert.equal(program.hasError(), false,
    program.diagnostics.filter((item) => item.severity === "error")
      .map((item) => formatDiagnostic(item)).join("\n"));
}

function parameters(document, operation) {
  return (operation.parameters ?? []).map((parameter) => parameter.$ref?.startsWith("#/")
    ? pointer(document, parameter.$ref.slice(1)) : parameter).filter(Boolean);
}

function body(document, operation) {
  return parameters(document, operation).filter((parameter) => parameter.in === "body");
}

function jsonFiles(root) {
  return fs.readdirSync(root, { withFileTypes: true }).flatMap((entry) => {
    const file = path.join(root, entry.name);
    return entry.isDirectory() ? jsonFiles(file) : entry.name.endsWith(".json") ? [file] : [];
  });
}

function checkEmployee(program, documents) {
  if (contract.sourceCase === "105058") return;
  const namespace = type(program, "Microsoft.Widget", "Namespace");
  assert.ok(listServices(program).some((service) => service.type === namespace),
    "Microsoft.Widget must remain the compiled service.");
  assert.ok(namespace.decorators.some(({ decorator }) => decorator.name === "$armProviderNamespace"),
    "Keep the ARM provider namespace annotation.");
  const versions = type(program, "Microsoft.Widget.Versions", "Enum");
  for (const member of versions.members.values()) {
    assert.match(getArmCommonTypesVersion(program, member), /^v?5$/, "Keep ARM common-types v5.");
  }
  if (contract.sourceCase === "104026") {
    for (const document of documents.values()) {
      const schema = document.definitions.Employee;
      assert.deepEqual(Object.keys(schema.properties).sort(), ["someId", "someOtherProperty"]);
      const read = document.paths[instance]?.get;
      assert.ok(read, "The legacy instance GET route must exist.");
      const name = parameters(document, read).find((parameter) => parameter.name === "employeeName");
      assert.equal(name?.in, "path");
      assert.equal(name.required, true, "The routing key is required without becoming a JSON field.");
      assert.ok(document.paths[collection]?.get, "Keep the legacy list-by-resource-group operation.");
    }
    return;
  }
  const resource = getArmResources(program).find((item) => item.name === "Employee" &&
    item.armProviderNamespace === "Microsoft.Widget");
  assert.ok(resource, "Employee must remain a reachable ARM resource.");
  assert.equal(resource.kind, "Tracked");
  const properties = type(program, "Microsoft.Widget.EmployeeProperties", "Model");
  assert.equal(resource.typespecType.properties.get("properties")?.type, properties,
    "Employee must use the actual EmployeeProperties payload, not a detached decoy model.");
  for (const [name, scalar] of [["age", "int32"], ["city", "string"], ["profile", "bytes"]]) {
    const property = properties.properties.get(name);
    assert.equal(property?.type.name, scalar, `Preserve ${name}'s type.`);
    assert.equal(property.optional, true, `Preserve ${name}'s optionality.`);
  }
  const lifecycle = contract.sourceCase === "105083"
    ? ["read", "update", "delete"] : ["createOrUpdate", "read", "update", "delete"];
  for (const name of lifecycle) {
    assert.ok(resource.operations.lifecycle[name], `Preserve Employee's ${name} operation.`);
  }
  for (const document of documents.values()) {
    for (const method of ["get", "patch", "delete", ...(contract.sourceCase === "105083" ? [] : ["put"])]) {
      assert.ok(document.paths[instance]?.[method], `Keep ${method.toUpperCase()} ${instance} in every emitted version.`);
    }
    assert.ok(document.paths[collection]?.get, "Keep list-by-resource-group.");
    assert.ok(document.paths["/subscriptions/{subscriptionId}/providers/Microsoft.Widget/employees"]?.get,
      "Keep list-by-subscription.");
  }
}

function checkMove(documents, input) {
  for (const file of [stable, preview]) {
    const document = documents.get(file);
    const operation = document.paths[instance + "/move"]?.post;
    assert.ok(operation, "Preserve the move action route.");
    assert.equal(operation.operationId, "Employees_Move", "Preserve the public operation identity.");
    const request = body(document, operation);
    if (input || file === stable) {
      assert.equal(request.length, 0, `${file} must retain the bodyless action.`);
    } else {
      assert.equal(request.length, 1);
      assert.equal(request[0].required, false, "The entire preview body must be optional.");
      assert.equal(request[0].schema.$ref, "#/definitions/MoveRequest");
      const model = document.definitions.MoveRequest;
      assert.equal(model.properties.city.type, "string");
      assert.ok(!model.required?.includes("city"), "City must also remain optional inside the body.");
    }
  }
}

function checkMultipart(program, documents, input) {
  const operation = type(program, "WidgetUpload.upload", "Operation");
  const [http, diagnostics] = getHttpOperation(program, operation);
  assert.equal(diagnostics.filter((item) => item.severity === "error").length, 0);
  assert.equal(http.path, "/employees/{employeeName}/profile");
  assert.equal(http.verb, "post");
  assert.equal(http.parameters.body?.bodyKind, "multipart");
  const parts = http.parameters.body.parts;
  assert.deepEqual(parts.map((part) => part.name).sort(), ["file", "metadata"]);
  const file = parts.find((part) => part.name === "file");
  assert.equal(file.body.type.kind, "Scalar");
  assert.equal(file.body.type.name, "bytes");
  const metadata = parts.find((part) => part.name === "metadata");
  assert.equal(metadata.body.type, type(program, "WidgetUpload.Employee", "Model"));
  const headers = [...operation.parameters.properties.values()].filter(
    (property) => getHeaderFieldName(program, property)?.toLowerCase() === "content-type",
  );
  assert.equal(headers.length, input ? 0 : 1,
    "The target needs an actual content-type parameter, not just inferred Swagger or a comment.");
  if (!input) {
    assert.equal(headers[0].type.kind, "String");
    assert.equal(headers[0].type.value, "multipart/form-data");
  }
  for (const document of documents.values()) {
    const upload = document.paths[http.path]?.post;
    assert.deepEqual(upload?.consumes, ["multipart/form-data"],
      "Inspect actual inference: the omitted-header input already consumes multipart/form-data.");
    const fields = parameters(document, upload).filter((parameter) => parameter.in === "formData");
    assert.deepEqual(fields.map((parameter) => parameter.name).sort(), ["file", "metadata"]);
    assert.equal(fields.find((parameter) => parameter.name === "file").type, "file");
  }
}

function checkPatch(program, documents, input) {
  for (const document of documents.values()) {
    const operation = document.paths[instance]?.patch;
    assert.ok(operation, "Keep the PATCH operation.");
    const request = body(document, operation);
    assert.equal(request.length, 1);
    assert.equal(request[0].schema.$ref, "#/definitions/EmployeeUpdate");
    const update = document.definitions.EmployeeUpdate;
    const name = input ? "EmployeePropertiesUpdate" : "EmployeeUpdateProperties";
    assert.equal(update.properties.properties.$ref, "#/definitions/" + name);
    assert.equal(update.properties.tags.type, "object");
    assert.equal(update.properties.tags.additionalProperties.type, "string");
    assert.ok(!update.required?.includes("tags"));
    assert.ok(!update.required?.includes("properties"));
    assert.deepEqual(Object.keys(document.definitions[name].properties ?? {}), []);
    assert.equal(document.definitions[input ? "EmployeeUpdateProperties" : "EmployeePropertiesUpdate"], undefined);
  }
  if (!input) {
    const namespace = type(program, "Microsoft.Widget", "Namespace");
    assert.equal(namespace.models.has("EmployeePropertiesUpdate"), false, "Remove the drifted source model.");
  }
}

function checkAccess(program) {
  const context = createTCGCContext(program, "@azure-tools/typespec-csharp", { mutateNamespace: false });
  const older = type(program, "Microsoft.Widget.EmployeeClientDetailsBeforePreview", "Model");
  const newer = type(program, "Microsoft.Widget.EmployeeClientDetails", "Model");
  assert.equal(getAccessOverride(context, older), "internal");
  assert.equal(getAccessOverride(context, newer), "public");
  const versions = type(program, "Microsoft.Widget.Versions", "Enum");
  const versionNames = new Map([...versions.members.values()].map((member) => [member.value, member.name]));
  const available = (model, version) =>
    [Availability.Added, Availability.Available].includes(
      getAvailabilityMap(program, model)?.get(versionNames.get(version)));
  assert.equal(available(older, "2021-11-01"), true);
  assert.equal(available(older, "2024-10-01-preview"), false);
  assert.equal(available(newer, "2021-11-01"), false);
  assert.equal(available(newer, "2024-10-01-preview"), true);
  assert.ok(getRenamedFrom(program, older)?.some((rename) =>
    rename.oldName === "EmployeeClientDetails" && rename.version.value === "2024-10-01-preview"),
  "The stable projection must retain the public model name EmployeeClientDetails.");
  for (const model of [older, newer]) {
    assert.equal(model.properties.get("value")?.type.name, "string");
    assert.equal(model.properties.get("value").optional, true);
  }
}

async function checkPrivateCandidate(program) {
  const canonical = (file) => process.platform === "win32"
    ? path.resolve(file).toLowerCase() : path.resolve(file);
  const imported = new Set([...program.sourceFiles.keys()].map(canonical));
  for (const name of ["private-preview.tsp", "private-employee.tsp"]) {
    assert.ok(fs.existsSync(name), `Keep the isolated ${name} authoring candidate.`);
    assert.equal(imported.has(canonical(name)), false,
      `${name} must not be imported, even transitively, into the public entry.`);
  }
  const versions = type(program, "Microsoft.Widget.Versions", "Enum");
  assert.deepEqual([...versions.members.values()].map((member) => member.value), ["2021-11-01"]);
  const candidate = await compile(NodeHost, path.resolve("private-preview.tsp"), { noEmit: true });
  assertCompiles(candidate);
  const privateVersions = type(candidate, "Microsoft.Widget.Versions", "Enum");
  assert.deepEqual([...privateVersions.members.values()].map((member) => member.value),
    ["2021-11-01", "2024-10-01-preview"]);
  const create = type(candidate, "Microsoft.Widget.Employees.createOrUpdate", "Operation");
  assert.deepEqual(getAddedOnVersions(candidate, create)?.map((version) => version.value),
    ["2024-10-01-preview"], "Do not silently promote the unready private create operation.");
}

function checkUnchanged() {
  const names = fs.readdirSync(".").filter((name) => name.endsWith(".tsp")).sort();
  assert.deepEqual(names, Object.keys(contract.unchangedTypeSpec).sort(),
    "The diagnosis must not add or remove TypeSpec files.");
  for (const [name, hash] of Object.entries(contract.unchangedTypeSpec)) {
    const source = fs.readFileSync(name, "utf8").replaceAll("\r\n", "\n");
    assert.equal(createHash("sha256").update(source).digest("hex"), hash,
      `${name} changed during a no-edit constructor diagnosis.`);
  }
}

const output = fs.mkdtempSync(path.join(os.tmpdir(), "discussion-contract-"));
try {
  // A fresh emission prevents stale or hand-written Swagger from satisfying the grader.
  const program = await compile(NodeHost, path.resolve("main.tsp"), {
    emit: ["@azure-tools/typespec-autorest"],
    outputDir: output,
    options: {
      "@azure-tools/typespec-autorest": {
        "emitter-output-dir": output,
        "output-file": "{version-status}/{version}/widget.json",
        "arm-types-dir": path.resolve("common-types"),
        "use-read-only-status-schema": true,
      },
    },
  });
  const errors = [...new Set(program.diagnostics.filter((item) => item.severity === "error")
    .map((item) => item.code))].sort();
  if (state === "input") {
    assert.deepEqual(errors, [...contract.inputErrorCodes].sort(),
      "Input diagnostics must match the declared reproduction, without unrelated errors.");
    if (errors.length) {
      console.log(`${contract.id}: confirmed intentional input diagnostics: ${errors.join(", ")}.`);
      process.exitCode = 0;
    }
  }
  if (state !== "input" || errors.length === 0) {
    assertCompiles(program);
    const files = jsonFiles(output);
    const documents = new Map(files.map((file) => [
      path.relative(output, file).split(path.sep).join("/"), JSON.parse(fs.readFileSync(file, "utf8")),
    ]));
    const expectedOutputs = state === "input" && contract.sourceCase === "105083"
      ? [preview, stable] : contract.expectedOutputs;
    assert.deepEqual([...documents.keys()].sort(), [...expectedOutputs].sort(),
      "Emit exactly the intended API versions, including public/private exclusion.");
    for (const check of contract.checks.filter((item) => (item.state ?? "expected") === state)) {
      const document = documents.get(check.file);
      assert.ok(document, `Missing emitted document ${check.file}`);
      const actual = pointer(document, check.pointer);
      const label = `${check.file}${check.pointer}`;
      if (check.absent) assert.equal(actual, undefined, `${label} must be absent.`);
      else if (Object.hasOwn(check, "equals")) assert.deepEqual(actual, check.equals, label);
      else if (Object.hasOwn(check, "includes")) assert.ok(actual?.includes(check.includes), label);
      else assert.notEqual(actual, undefined, `${label} must exist.`);
    }
    const input = state === "input";
    if (!input || ["104008", "105058", "105071"].includes(contract.sourceCase)) {
      if (contract.sourceCase === "104008") checkMove(documents, input);
      if (contract.sourceCase === "105058") checkMultipart(program, documents, input);
      if (contract.sourceCase === "105071") checkPatch(program, documents, input);
    }
    if (!input) {
      checkEmployee(program, documents);
      if (contract.sourceCase === "105086") checkAccess(program);
      if (contract.sourceCase === "105083") await checkPrivateCandidate(program);
      if (contract.sourceCase === "104044") {
        const namespace = type(program, "Microsoft.Widget", "Namespace");
        assert.equal(namespace.decorators.some(({ decorator }) => decorator.name === "$server"), false,
          "Remove the explicit @server rather than replacing it with another one.");
      }
      if (contract.sourceCase === "105080") {
        const model = type(program, "Microsoft.Widget.EmployeeProfile", "Model");
        assert.equal(model.properties.get("kind")?.type,
          type(program, "Microsoft.Widget.EmployeeProfileKind", "Union"),
        "The actual discriminator must use the extensible union, not an unused companion type.");
        assert.equal(model.node.directives.some((directive) => directive.arguments.some(
          (argument) => argument.value === "@azure-tools/typespec-azure-core/no-string-discriminator",
        )), false, "Remove the unsupported discriminator suppression.");
      }
      if (contract.sourceCase === "105098") {
        const pet = type(program, "Microsoft.Widget.Pet", "Model");
        assert.equal(pet.properties.get("kind")?.type, type(program, "Microsoft.Widget.PetKind", "Union"));
        assert.deepEqual(pet.derivedModels.map((model) => model.name), ["Dog"],
          "Dog is the only currently authorized successful leaf.");
        for (const document of documents.values()) {
          assert.match(document.paths["/employeePets/{id}"].get.responses.default.schema.$ref,
            /#\/definitions\/ErrorResponse$/, "Keep the original error response.");
          assert.equal(document.definitions.PetKind["x-ms-enum"].modelAsString, true);
        }
      }
    }
    if (contract.mode === "diagnosis") checkUnchanged();
    console.log(`${contract.id}: ${state} compiled contract passed.`);
  }
} finally {
  fs.rmSync(output, { recursive: true, force: true });
}
