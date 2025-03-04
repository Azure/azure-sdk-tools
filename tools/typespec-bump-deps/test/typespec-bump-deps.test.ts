import { expect } from "chai";
import { describe, it } from "mocha";
import { updatePackageJson, getVersionRange } from "../src/cli/typespec-bump-deps.js";

describe("typespec-bump-deps cli", () => {
  describe("updatePackageJson()", () => {
    it("should replace dependency versions", () => {
      const packageJson = {
        dependencies: {
          "package-a": "1.0.0",
          "extra": "1",
        },
        devDependencies: {
          "package-b": "1.0.0",
          "extra": "1",
        },
        peerDependencies: {
          "package-c": "1.0.0",
          "extra": "1",
        },
      };

      const packageToVersionRecord = {
        "package-a": "2.0.0",
        "package-b": "3.0.0",
        "package-c": "4.0.0",
      };

      updatePackageJson(
        packageJson,
        packageToVersionRecord,
        false, // usePeerRanges
        false, // addNpmOverrides
        false, // addRushOverrides
      );

      expect(packageJson).to.deep.equal({
        dependencies: {
          "package-a": "2.0.0",
          "extra": "1",
        },
        devDependencies: {
          "package-b": "3.0.0",
          "extra": "1",
        },
        peerDependencies: {
          "package-c": "4.0.0",
          "extra": "1",
        },
      });
    });

    it("should add overrides when addNpmOverrides == true", () => {
      const packageJson = {};

      const packageToVersionRecord = {
        "package-a": "2.0.0",
        "package-b": "3.0.0",
        "package-c": "4.0.0",
      };

      updatePackageJson(
        packageJson,
        packageToVersionRecord,
        false, // usePeerRanges
        true, // addNpmOverrides
        false, // addRushOverrides
      );

      expect(packageJson).to.deep.equal({
        overrides: {
          "package-a": "2.0.0",
          "package-b": "3.0.0",
          "package-c": "4.0.0",
        },
      });
    });

    it("should add globalOverrides when addRushOverrides == true", () => {
      const packageJson = {
        dependencies: {},
      };

      const packageToVersionRecord = {
        "package-a": "2.0.0",
        "package-b": "3.0.0",
        "package-c": "4.0.0",
      };

      const usePeerRanges = false;
      const addNpmOverrides = false;
      const addRushOverrides = true;

      updatePackageJson(packageJson, packageToVersionRecord, usePeerRanges, addNpmOverrides, addRushOverrides);

      expect(packageJson).to.deep.equal({
        dependencies: {},
        globalOverrides: {
          "package-a": "2.0.0",
          "package-b": "3.0.0",
          "package-c": "4.0.0",
        },
      });
    });

    describe("when usePeerRanges == true", () => {
      it("should use version ranges for peerDependencies", () => {
        const packageJson = {
          peerDependencies: {
            "package-a": ">=1.2.3",
          },
          devDependencies: {
            "package-a": "1.2.3",
          },
        };

        const packageToVersionRecord = {
          "package-a": "2.1.0-dev.1",
        };

        updatePackageJson(
          packageJson,
          packageToVersionRecord,
          true, // usePeerRanges
          false, // addNpmOverrides
          false, // addRushOverrides
        );

        expect(packageJson).to.deep.equal({
          peerDependencies: {
            "package-a": ">=1.2.3 || >=2.1.0-0 <2.1.0",
          },
          devDependencies: {
            "package-a": "2.1.0-dev.1",
          },
        });
      });

      it("should use a range for unpaired peerDependencies", () => {
        const packageJson = {
          peerDependencies: {
            "package-a": "1.2.3",
          },
          devDependencies: {},
        };

        const packageToVersionRecord = {
          "package-a": "2.1.0-dev.1",
        };

        updatePackageJson(
          packageJson,
          packageToVersionRecord,
          true, // usePeerRanges
          false, // addNpmOverrides
          false, // addRushOverrides
        );

        expect(packageJson).to.deep.equal({
          peerDependencies: {
            "package-a": "1.2.3 || >=2.1.0-0 <2.1.0",
          },
          devDependencies: {
            "package-a": "2.1.0-dev.1",
          },
        });
      });

      it("should throw error if a package is both a dependency and peerDependency", () => {
        const packageJson = {
          dependencies: {
            "package-a": "1.2.3",
            "package-b": "1.2.3",
          },
          peerDependencies: {
            "package-a": "1.2.3",
          },
        };

        const packageToVersionRecord = {
          "package-a": "2.1.0-dev.1",
        };

        expect(() => {
          updatePackageJson(
            packageJson,
            packageToVersionRecord,
            true, // usePeerRanges
            false, // addNpmOverrides
            false, // addRushOverrides
          );
        }).to.throw("package-a is both a dependency and peerDependency");
      });
    });
  });

  describe("getVersionRange()", () => {
    it("should produce a pre-release version range", () => {
      const actual = getVersionRange("1.2.0");

      expect(actual).to.equal(">=1.2.0-0 <1.2.0");
    });

    it("should use 0 for the patch version", () => {
      const actual = getVersionRange("1.2.3");

      expect(actual).to.equal(">=1.2.0-0 <1.2.0");
    });

    it("should use the original version's minor and major version", () => {
      const actual = getVersionRange("4.3.0");

      expect(actual).to.equal(">=4.3.0-0 <4.3.0");
    });
  });
});
