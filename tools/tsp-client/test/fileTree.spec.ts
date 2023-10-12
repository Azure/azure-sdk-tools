import { assert } from "chai";
import { createFileTree } from "../src/fileTree.js";

describe("FileTree", function () {
  it("handles basic tree", async function () {
    const tree = createFileTree("http://example.com/foo/bar/baz.tsp");
    tree.addFile("http://example.com/foo/bar/baz.tsp", "text");
    tree.addFile("http://example.com/foo/buzz/foo.tsp", "text");
    tree.addFile("http://example.com/foo/bar/qux.tsp", "text");
    const result = await tree.createTree();
    assert.strictEqual(result.mainFilePath, "bar/baz.tsp");
    assert.strictEqual(result.files.size, 3);
    const resultPaths = new Set(result.files.keys());
    assert.isTrue(resultPaths.has("bar/baz.tsp"));
    assert.isTrue(resultPaths.has("bar/qux.tsp"));
    assert.isTrue(resultPaths.has("buzz/foo.tsp"));
    for (const contents of result.files.values()) {
      assert.strictEqual(contents, "text");
    }
  });
});
