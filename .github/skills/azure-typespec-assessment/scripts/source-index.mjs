import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { readRevisionFile, unifiedDiff } from "./git-evidence.mjs";

// Raw revision text is process-local: never serialize whole files into model evidence.
const revisionSources = new WeakMap();

function id(prefix, value) {
  return `${prefix}-${crypto.createHash("sha256").update(value).digest("hex").slice(0, 16)}`;
}

export function parseUnifiedHunks(diff) {
  const hunks = [];
  const lines = diff.split(/\r?\n/);
  for (let index = 0; index < lines.length; index += 1) {
    const match = /^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@/.exec(lines[index]);
    if (!match) continue;
    const body = [];
    for (index += 1; index < lines.length && !lines[index].startsWith("@@ "); index += 1) {
      if (!lines[index].startsWith("diff --git")) body.push(lines[index]);
    }
    index -= 1;
    const baseStart = Number(match[1]);
    const baseCount = Number(match[2] ?? 1);
    const currentStart = Number(match[3]);
    const currentCount = Number(match[4] ?? 1);
    const key = `${baseStart}:${baseCount}:${currentStart}:${currentCount}:${body.join("\n")}`;
    hunks.push({
      id: id("hunk", key),
      base: { startLine: baseStart, endLine: baseStart + Math.max(0, baseCount - 1) },
      current: { startLine: currentStart, endLine: currentStart + Math.max(0, currentCount - 1) },
      lines: body,
    });
  }
  return hunks;
}

function declarations(content, hunks, revision, file, linkFactory) {
  if (!content) return [];
  const lines = content.split(/\r?\n/);
  const pattern = /^\s*(?:extern\s+)?(model|scalar|enum|union|alias|interface|op)\s+([A-Za-z_]\w*)/;
  const results = [];
  const spans = [];
  const relatedHunks = (startLine, endLine = startLine) => hunks.filter((hunk) => {
    const range = hunk[revision];
    return range && range.endLine >= startLine && range.startLine <= endLine;
  });
  const enclosingInterface = (line) => {
    for (let previous = line - 1; previous >= 0; previous -= 1) {
      const match = /^\s*interface\s+([A-Za-z_]\w*)/.exec(lines[previous]);
      if (match) return match[1];
    }
    return undefined;
  };
  for (let line = 0; line < lines.length; line += 1) {
    const match = pattern.exec(lines[line]);
    if (!match) continue;
    let end = line;
    let depth = 0;
    do {
      depth += (lines[end].match(/{/g) ?? []).length;
      depth -= (lines[end].match(/}/g) ?? []).length;
      end += 1;
    } while (end < lines.length && depth > 0);
    const startLine = line + 1;
    const endLine = Math.max(startLine, end);
    const related = hunks.filter((hunk) => {
      const range = hunk[revision];
      return range && range.endLine >= startLine && range.startLine <= endLine;
    });
    if (!related.length) continue;
    const decoratorStart = Math.max(0, line - 8);
    const decorators = lines
      .slice(decoratorStart, line)
      .filter((item) => item.trim().startsWith("@"))
      .map((item) => item.trim());
    const qualifiedName = match[2];
    spans.push({
      kind: match[1] === "op" ? "operation" : match[1],
      name: qualifiedName,
      startLine,
      endLine,
    });
    results.push({
      id: id("declaration", `${file}:${revision}:${match[1]}:${qualifiedName}:${startLine}`),
      kind: match[1] === "op" ? "operation" : match[1],
      qualifiedName,
      decorators,
      versionedMembers: decorators.filter((item) => /^@(added|removed|renamedFrom|typeChangedFrom)\b/.test(item)),
      hunkIds: related.map((item) => item.id),
      source: {
        revision,
        startLine,
        endLine,
        link: linkFactory?.(file, revision, startLine, endLine),
      },
    });
  }
  for (let line = 0; line < lines.length; line += 1) {
    const member = /^\s*([A-Za-z_]\w*)\s+(?:is\b|\()/.exec(lines[line]);
    const lineNumber = line + 1;
    const owner = member
      ? spans.find((span) =>
          span.kind === "interface" &&
          span.startLine < lineNumber &&
          span.endLine >= lineNumber,
        )?.name ?? enclosingInterface(line)
      : undefined;
    if (!member || !owner) continue;
    const related = relatedHunks(lineNumber);
    if (!related.length) continue;
    const qualifiedName = `${owner}.${member[1]}`;
    results.push({
      id: id("declaration", `${file}:${revision}:operation:${qualifiedName}:${lineNumber}`),
      kind: "operation",
      qualifiedName,
      decorators: [],
      versionedMembers: [],
      hunkIds: related.map((item) => item.id),
      source: {
        revision,
        startLine: lineNumber,
        endLine: lineNumber,
        link: linkFactory?.(file, revision, lineNumber, lineNumber),
      },
    });
  }
  for (let line = 0; line < lines.length; line += 1) {
    const property = /^\s*([A-Za-z_]\w*)\??\s*:/.exec(lines[line]);
    if (!property) continue;
    const lineNumber = line + 1;
    const related = relatedHunks(lineNumber);
    if (!related.length) continue;
    const owner = spans
      .filter((span) =>
        span.kind === "model" &&
        span.startLine < lineNumber &&
        span.endLine >= lineNumber,
      )
      .sort((left, right) => right.startLine - left.startLine)[0];
    if (!owner) continue;
    const qualifiedName = `${owner.name}.${property[1]}`;
    results.push({
      id: id("declaration", `${file}:${revision}:property:${qualifiedName}:${lineNumber}`),
      kind: "property",
      qualifiedName,
      decorators: [],
      versionedMembers: [],
      hunkIds: related.map((item) => item.id),
      source: {
        revision,
        startLine: lineNumber,
        endLine: lineNumber,
        link: linkFactory?.(file, revision, lineNumber, lineNumber),
      },
    });
  }
  return results;
}

export function buildSourceIndex({
  repo,
  mergeBase,
  headCommit,
  changedFiles,
  remoteUrl,
  readFile = (revision, file) => readRevisionFile(repo, revision, file),
  diffFile = (file) => unifiedDiff(repo, mergeBase, file),
}) {
  const texts = new Map();
  const sourceChanges = changedFiles.map((file) => {
    const base = readFile(mergeBase, file.path);
    const current = readFile("working", file.path);
    const head = readFile(headCommit, file.path);
    texts.set(file.path, { base, current });
    let diff = diffFile(file.path);
    if (!diff && base === null && current !== null) {
      const added = current
        .split(/\r?\n/)
        .slice(0, current.endsWith("\n") ? -1 : undefined)
        .map((line) => `+${line}`)
        .join("\n");
      const lineCount = added ? added.split("\n").length : 0;
      diff = `--- /dev/null\n+++ b/${file.path}\n@@ -0,0 +1,${lineCount} @@\n${added}\n`;
    }
    const hunks = parseUnifiedHunks(diff).map((hunk) => ({
      ...hunk,
      id: id("hunk", `${file.path}:${hunk.id}`),
    }));
    const github = /github\.com[/:]([^/]+)\/([^/.]+)(?:\.git)?$/.exec(remoteUrl ?? "");
    const linkFactory = github
      ? (sourcePath, revision, start, end) => {
          if (revision === "current" && current !== head) return undefined;
          const commit = revision === "base" ? mergeBase : headCommit;
          return `https://github.com/${github[1]}/${github[2]}/blob/${commit}/${sourcePath}#L${start}-L${end}`;
        }
      : undefined;
    const sourceId = id("source", `${file.path}:${file.status}:${diff}`);
    return {
      id: sourceId,
      path: file.path,
      status: file.status,
      origins: file.origins,
      hunks,
      declarations: [
        ...declarations(base, hunks, "base", file.path, linkFactory),
        ...declarations(current, hunks, "current", file.path, linkFactory),
      ],
    };
  });
  const result = {
    schemaVersion: 1,
    analysis: {
      status: "not-run",
      authority: "typespec-compiler",
      blockers: [],
    },
    sourceChanges,
  };
  revisionSources.set(result, texts);
  return result;
}

function normalizedRelative(root, file) {
  return path.relative(root, file).replaceAll("\\", "/");
}

function semanticQualifiedName(type, kind) {
  const name = (value) =>
    typeof value === "string" || typeof value === "number" ? String(value) : undefined;
  const ownName = name(type.name);
  if (!ownName) return undefined;
  const owner = kind === "operation"
    ? name(type.interface?.name)
    : kind === "property"
      ? name(type.model?.name)
      : kind === "enum-member"
        ? name(type.enum?.name)
        : kind === "union-variant"
          ? name(type.union?.name)
          : undefined;
  return owner ? `${owner}.${ownName}` : ownName;
}

function compilerReferences(type) {
  const references = new Set();
  const visited = new WeakSet();
  const visit = (value, depth = 0) => {
    if (!value || typeof value !== "object" || visited.has(value) || depth > 8) return;
    visited.add(value);
    if (typeof value.kind === "string" && value !== type) {
      const name = semanticQualifiedName(
        value,
        value.kind === "ModelProperty" ? "property" :
          value.kind === "Operation" ? "operation" :
            value.kind.toLowerCase(),
      );
      if (name) references.add(name);
    }
    const children = [];
    switch (value.kind) {
      case "Operation":
        children.push(value.parameters, value.returnType);
        break;
      case "Model":
        children.push(value.baseModel, value.sourceModel, value.indexer?.value);
        if (typeof value.properties?.values === "function") {
          children.push(...value.properties.values());
        }
        break;
      case "ModelProperty":
        children.push(value.type);
        break;
      case "Union":
        if (typeof value.variants?.values === "function") {
          children.push(...value.variants.values());
        }
        break;
      case "UnionVariant":
        children.push(value.type);
        break;
      case "Tuple":
        children.push(...(value.values ?? []));
        break;
      default:
        break;
    }
    children.filter(Boolean).forEach((child) => visit(child, depth + 1));
  };
  visit(type);
  return [...references].sort();
}

function compilerDeclaration(type, kind, revision, root, source, compiler) {
  const location = compiler.getSourceLocation(type);
  if (!location?.file?.path || !Number.isInteger(location.pos) || !Number.isInteger(location.end)) {
    return undefined;
  }
  const file = normalizedRelative(root, location.file.path);
  if (file !== source.path) return undefined;
  const start = location.file.getLineAndCharacterOfPosition(location.pos);
  const end = location.file.getLineAndCharacterOfPosition(Math.max(location.pos, location.end - 1));
  const startLine = start.line + 1;
  const endLine = end.line + 1;
  const hunkIds = (source.hunks ?? [])
    .filter((hunk) => {
      const range = hunk[revision];
      return range && range.endLine >= startLine && range.startLine <= endLine;
    })
    .map((hunk) => hunk.id);
  if (!hunkIds.length) return undefined;
  const qualifiedName = semanticQualifiedName(type, kind);
  if (!qualifiedName) return undefined;
  return {
    id: id("declaration", `${file}:${revision}:${kind}:${qualifiedName}:${startLine}`),
    kind,
    qualifiedName,
    decorators: [],
    versionedMembers: [],
    hunkIds,
    compilerEvidence: {
      kind: "semantic-type",
      compilerTypeKind: type.kind,
      sourceFile: file,
      position: { start: location.pos, end: location.end },
      referencedNames: compilerReferences(type),
    },
    source: { revision, startLine, endLine },
  };
}

async function compilerModule(worktree) {
  const entry = path.join(worktree, "node_modules", "@typespec", "compiler", "dist", "src", "index.js");
  if (!fs.existsSync(entry)) throw new Error(`TypeSpec compiler not found: ${entry}`);
  const compiler = await import(pathToFileURL(entry).href);
  // Older compilers may not expose the AST entry point. Preserve semantic analysis
  // and explicitly block only documentation evidence in that case.
  const astEntry = path.join(path.dirname(entry), "ast", "index.js");
  const ast = fs.existsSync(astEntry)
    ? await import(pathToFileURL(astEntry).href)
    : undefined;
  const packageJson = JSON.parse(fs.readFileSync(
    path.join(worktree, "node_modules", "@typespec", "compiler", "package.json"), "utf8",
  ));
  return { ...compiler, ...ast, compilerVersion: packageJson.version };
}

function documentDeclarations(script, program, compiler, source, revision) {
  if (!compiler.SyntaxKind || typeof compiler.visitChildren !== "function") {
    throw new Error("This TypeSpec compiler does not expose the required AST traversal API.");
  }
  const kinds = {
    NamespaceStatement: "namespace", ModelStatement: "model", ModelProperty: "property",
    OperationStatement: "operation", InterfaceStatement: "interface", ScalarStatement: "scalar",
    EnumStatement: "enum", EnumMember: "enum-member", UnionStatement: "union",
    UnionVariant: "union-variant", AliasStatement: "alias",
    ScalarConstructor: "scalar-constructor",
  };
  const records = [];
  const file = script.file;
  // Hunk context is not a change: only +/- positions establish document ownership.
  // Pair equality below also excludes untouched siblings when a whole file is diffed.
  const edits = source.hunks.map((hunk) => {
    let line = hunk[revision].startLine;
    const changed = [];
    for (const text of hunk.lines) {
      const marker = text[0];
      if (marker === (revision === "base" ? "-" : "+")) changed.push(line);
      if (marker === " " || marker === (revision === "base" ? "-" : "+")) line += 1;
    }
    return { id: hunk.id, lines: changed };
  });
  const visit = (node, owner = "") => {
    if (node.kind === compiler.SyntaxKind.TypeSpecScript) {
      let scope = owner;
      for (const statement of node.statements) {
        visit(statement, scope);
        let namespace = statement;
        const names = [];
        while (namespace?.kind === compiler.SyntaxKind.NamespaceStatement &&
          !Array.isArray(namespace.statements)) {
          names.push(namespace.id.sv);
          namespace = namespace.statements;
        }
        if (!namespace && names.length) scope = [owner, ...names].filter(Boolean).join(".");
      }
      return;
    }
    const kind = kinds[compiler.SyntaxKind[node.kind]];
    const name = node.id?.sv;
    const qualifiedName = kind && name ? [owner, name].filter(Boolean).join(".") : owner;
    const decorators = node.kind === compiler.SyntaxKind.AugmentDecoratorStatement
      ? [node]
      : node.decorators ?? [];
    const targetName = (target) => target?.sv ??
      (target?.base && target?.id ? `${targetName(target.base)}.${target.id.sv}` : "");
    const candidates = decorators.filter((decorator) =>
      ["doc", "TypeSpec.doc"].includes(targetName(decorator.target)));
    if (candidates.length) {
      const startLine = file.getLineAndCharacterOfPosition(node.pos).line + 1;
      const endLine = file.getLineAndCharacterOfPosition(Math.max(node.pos, node.end - 1)).line + 1;
      let doc = null;
      let blocker;
      if (candidates.length && (!kind || !name)) {
        blocker = "Cannot establish a supported named declaration context for this @doc.";
      } else if (candidates.length) {
        const type = program.checker.getTypeForNode(node);
        const applications = (type.decorators ?? []).filter((item) => item.decorator === compiler.$doc);
        const docs = candidates.filter((candidate) =>
          applications.some((application) => application.node === candidate));
        if (docs.length !== candidates.length) {
          blocker = "Cannot establish that the documentation decorator resolves to TypeSpec @doc.";
        } else if (docs.length !== 1 || docs[0].arguments.length !== 1 ||
          docs[0].arguments[0].kind !== compiler.SyntaxKind.StringLiteral) {
          blocker = "Only a single literal TypeSpec @doc argument is supported; dynamic or formatted documentation cannot be evaluated.";
        } else {
          doc = docs[0].arguments[0].value;
        }
      }
      records.push({
        key: kind && name ? `${kind}:${qualifiedName}` : `unsupported:${qualifiedName}:${node.pos}`,
        qualifiedName: qualifiedName || compiler.SyntaxKind[node.kind],
        kind: kind ?? "unsupported", doc, blocker,
        declaration: file.text.slice(node.pos, node.end),
        source: { path: source.path, revision, startLine, endLine },
        hunkIds: edits.filter((hunk) =>
          hunk.lines.some((line) => line >= startLine && line <= endLine)).map((hunk) => hunk.id),
      });
    }
    compiler.visitChildren(node, (child) => { visit(child, qualifiedName); });
  };
  visit(script);
  return records;
}

function changedDocumentEvidence(source, revisions) {
  const byRevision = {};
  for (const revision of ["base", "current"]) {
    byRevision[revision] = new Map();
    for (const record of revisions[revision] ?? []) {
      if (byRevision[revision].has(record.key)) {
        throw new Error(`Ambiguous documentation declaration: ${record.qualifiedName}`);
      }
      byRevision[revision].set(record.key, record);
    }
  }
  const documents = [];
  for (const [key, after] of byRevision.current) {
    const before = byRevision.base.get(key);
    if (after.doc === null && !after.blocker) continue;
    if (before?.declaration === after.declaration) continue;
    const hunkIds = [...new Set([...(before?.hunkIds ?? []), ...after.hunkIds])].sort();
    if (!hunkIds.length) continue;
    const snapshot = (record) => record?.doc !== null && record?.doc !== undefined ? {
      doc: record.doc, declaration: record.declaration, source: record.source,
    } : null;
    documents.push({
      id: id("document", `${source.path}:${key}`),
      sourceChangeId: source.id,
      qualifiedName: after.qualifiedName,
      kind: after.kind,
      before: snapshot(before),
      after: snapshot(after),
      hunkIds,
      blocker: after.blocker ?? before?.blocker,
    });
  }
  return documents.sort((left, right) => left.id.localeCompare(right.id));
}

export async function addCompilerEvidence({
  sourceIndex,
  baseWorktree,
  currentWorktree,
  projects,
  loadCompiler = compilerModule,
}) {
  // Only changed @doc/declaration pairs are serialized. All AST siblings are kept
  // locally for pairing, then discarded; deleted docs are deliberately not coverage findings.
  const documentRevisions = new Map(sourceIndex.sourceChanges.map((source) => [source.id, {}]));
  const documentBlockers = new Map(sourceIndex.sourceChanges.map((source) => [source.id, []]));
  const declarations = new Map(sourceIndex.sourceChanges.map((source) => [source.id, []]));
  const referencedDeclarations = {};
  const resourceModels = {};
  const operationProjectionStats = [];
  const blockers = [];
  const versions = new Set();
  for (const [revision, worktree] of [["base", baseWorktree], ["current", currentWorktree]]) {
    let compiler;
    try {
      compiler = await loadCompiler(worktree);
      versions.add(compiler.compilerVersion ?? "unknown");
    } catch (error) {
      blockers.push({ revision, message: error.message });
      continue;
    }
    for (const project of projects) {
      try {
        const program = await compiler.compile(
          compiler.NodeHost,
          path.join(worktree, project),
          { noEmit: true },
        );
        const diagnostics = program.diagnostics.filter((item) => item.severity === "error");
        if (diagnostics.length) {
          blockers.push({
            revision,
            project,
            message: `TypeSpec program has ${diagnostics.length} error diagnostic(s).`,
          });
          continue;
        }
        for (const source of sourceIndex.sourceChanges) {
          const script = [...program.sourceFiles.values()].find((item) =>
            normalizedRelative(worktree, item.file.path) === source.path);
          if (!script) continue;
          const raw = revisionSources.get(sourceIndex)?.get(source.path)?.[revision];
          if (raw === null) continue;
          if (raw !== undefined && raw !== script.file.text) {
            documentBlockers.get(source.id).push({
              revision, message: "Compiled source differs from the captured changed revision.",
            });
            continue;
          }
          try {
            documentRevisions.get(source.id)[revision] =
              documentDeclarations(script, program, compiler, source, revision);
          } catch (error) {
            documentBlockers.get(source.id).push({ revision, message: error.message });
          }
        }
        const operationTypes = [];
        const listeners = {};
        for (const [event, kind] of [
          ["namespace", "namespace"],
          ["interface", "interface"],
          ["operation", "operation"],
          ["model", "model"],
          ["modelProperty", "property"],
          ["scalar", "scalar"],
          ["enum", "enum"],
          ["enumMember", "enum-member"],
          ["union", "union"],
          ["unionVariant", "union-variant"],
        ]) {
          listeners[event] = (type) => {
            if (kind === "operation") operationTypes.push(type);
            if (kind === "model") {
              const decoratorName = (decorator) =>
                decorator.decorator?.name ??
                decorator.definition?.name ??
                decorator.node?.target?.sv;
              const decorators = (type.decorators ?? [])
                .map(decoratorName)
                .filter(Boolean);
              if (decorators.includes("$feature") || decorators.includes("$parentResource")) {
                const location = compiler.getSourceLocation(type);
                const qualifiedName = semanticQualifiedName(type, "model");
                if (qualifiedName && location?.file?.path) {
                  const model = {
                    name: qualifiedName,
                    revision,
                    project,
                    sourcePath: normalizedRelative(worktree, location.file.path),
                    baseModel: type.baseModel?.name,
                    decorators,
                    parentResource: (type.decorators ?? [])
                      .find((decorator) => decoratorName(decorator) === "$parentResource")
                      ?.args?.[0]?.value?.name,
                  };
                  resourceModels[id("resource-model", `${project}:${revision}:${qualifiedName}`)] = model;
                }
              }
            }
            for (const source of sourceIndex.sourceChanges) {
              const declaration = compilerDeclaration(
                type,
                kind,
                revision,
                worktree,
                source,
                compiler,
              );
              if (declaration) declarations.get(source.id).push(declaration);
            }
          };
        }
        compiler.navigateProgram(program, listeners);
        const changedNames = new Set(
          [...declarations.values()]
            .flat()
            .filter((declaration) => declaration.source.revision === revision)
            .flatMap((declaration) => [
              declaration.qualifiedName,
              declaration.qualifiedName.split(".").at(0),
            ]),
        );
        let matchedOperationCount = 0;
        for (const operation of operationTypes) {
          const referencedNames = compilerReferences(operation);
          if (!referencedNames.some((name) =>
            changedNames.has(name) || changedNames.has(name.split(".").at(0)))) {
            continue;
          }
          matchedOperationCount += 1;
          const qualifiedName = semanticQualifiedName(operation, "operation");
          const location = compiler.getSourceLocation(operation);
          if (!qualifiedName || !location?.file?.path) continue;
          const file = normalizedRelative(worktree, location.file.path);
          const start = location.file.getLineAndCharacterOfPosition(location.pos);
          const end = location.file.getLineAndCharacterOfPosition(
            Math.max(location.pos, location.end - 1),
          );
          const declaration = {
            id: id("referenced-declaration", `${project}:${revision}:${qualifiedName}:${file}`),
            kind: "operation",
            qualifiedName,
            revision,
            project,
            compilerEvidence: {
              kind: "operation-projection",
              referencedNames,
            },
            source: {
              revision,
              path: file,
              startLine: start.line + 1,
              endLine: end.line + 1,
            },
          };
          referencedDeclarations[declaration.id] = declaration;
        }
        operationProjectionStats.push({
          revision,
          project,
          operationCount: operationTypes.length,
          changedNameCount: changedNames.size,
          matchedOperationCount,
        });
      } catch (error) {
        blockers.push({ revision, project, message: error.message });
      }
      sourceIndex.referencedDeclarations = referencedDeclarations;
      sourceIndex.resourceModels = resourceModels;
    }
  }
  for (const source of sourceIndex.sourceChanges) {
    const compiled = declarations.get(source.id);
    if (compiled.length) {
      source.declarations = [...new Map(compiled.map((item) => [item.id, item])).values()]
        .sort((left, right) => left.source.startLine - right.source.startLine);
    }
    const revisions = documentRevisions.get(source.id);
    const evidenceBlockers = documentBlockers.get(source.id);
    for (const revision of ["base", "current"]) {
      const raw = revisionSources.get(sourceIndex)?.get(source.path)?.[revision];
      const absent = raw === null || (raw === undefined &&
        (revision === "base" ? source.status === "added" : source.status === "deleted"));
      if (absent) revisions[revision] = [];
      if (!revisions[revision]) evidenceBlockers.push({
        revision, message: "Changed source was not available in a successfully compiled TypeSpec program.",
      });
    }
    let documents = [];
    if (!evidenceBlockers.length) {
      try {
        documents = changedDocumentEvidence(source, revisions);
      } catch (error) {
        evidenceBlockers.push({ message: error.message });
      }
    }
    source.documentEvidence = {
      status: evidenceBlockers.length ? "blocked" : "ready",
      blockers: evidenceBlockers,
      documents,
    };
  }
  sourceIndex.analysis = {
    status: blockers.length ? "blocked" : "ready",
    authority: "typespec-compiler",
    compilerVersions: [...versions].sort(),
    operationProjectionStats,
    blockers,
  };
  return sourceIndex;
}
