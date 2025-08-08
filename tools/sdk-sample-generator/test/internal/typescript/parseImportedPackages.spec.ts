import { describe, expect, it } from "vitest";
import { parseImportedPackages } from "../../../genaisrc/src/typescript/typecheck.ts";

describe("parseImportedPackages", () => {
    const basePkgs = new Set(["typescript", "@types/node"]);

    function expectPkgs(
        code: string,
        excluded: string[] = [],
        expected: string[],
    ) {
        const result = parseImportedPackages(code, new Set(excluded));
        const got = new Set(result);
        const want = new Set([...basePkgs, ...expected]);
        expect(got).toEqual(want);
    }

    it("extracts a single named import", () => {
        const code = `import { map } from "lodash";`;
        expectPkgs(code, [], ["lodash"]);
    });

    it("extracts a default import", () => {
        const code = `import React from "react";`;
        expectPkgs(code, [], ["react"]);
    });

    it("extracts side-effect imports", () => {
        const code = `import "zone.js/dist/zone";`;
        expectPkgs(code, [], ["zone.js"]);
    });

    it("ignores relative and absolute paths", () => {
        const code = `
      import foo from "./local";
      const bar = require("../utils");
      import("/another/thing");
      import "./also-local";
    `;
        expectPkgs(code, [], []);
    });

    it("handles require() calls", () => {
        const code = `const _ = require("underscore");`;
        expectPkgs(code, [], ["underscore"]);
    });

    it("handles dynamic import() expressions", () => {
        const code = `const pkg = await import("dayjs/plugin/utc");`;
        expectPkgs(code, [], ["dayjs"]);
    });

    it("deduplicates multiple imports", () => {
        const code = `
      import { a } from "pkg";
      const b = require("pkg/submod");
      import("pkg/dist");
    `;
        expectPkgs(code, [], ["pkg"]);
    });

    it("respects excludedPkgs", () => {
        const code = `
      import express from "express";
      import { Request } from "@types/express";
    `;
        expectPkgs(code, ["express"], ["@types/express"]);
        expectPkgs(code, ["@types/express"], ["express"]);
    });

    it("handles scoped packages and sub‑paths", () => {
        const code = `
      import { Client } from "@azure/ai-language-text";
      import helper from "@azure/ai-language-text/dist/esm/helper";
    `;
        expectPkgs(code, [], ["@azure/ai-language-text"]);
    });

    it("returns only base packages when no imports", () => {
        const code = `console.log("hello");`;
        expectPkgs(code, [], []);
    });
});
