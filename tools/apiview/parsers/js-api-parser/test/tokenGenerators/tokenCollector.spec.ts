import { describe, expect, it } from "vitest";
import { TokenCollector, createToken } from "../../src/tokenGenerators/helpers";
import { TokenKind } from "../../src/models";

describe("TokenCollector", () => {
  describe("currentTarget", () => {
    it("returns main tokens when no children exist", () => {
      const collector = new TokenCollector();
      expect(collector.currentTarget).toBe(collector.tokens);
    });

    it("returns last child's tokens after newLine()", () => {
      const collector = new TokenCollector();
      collector.push(createToken(TokenKind.Text, "a"));
      collector.newLine();
      expect(collector.currentTarget).not.toBe(collector.tokens);
      expect(collector.currentTarget).toBe(collector.children[0].Tokens);
    });

    it("tracks the latest child after multiple newLine() calls", () => {
      const collector = new TokenCollector();
      collector.newLine();
      collector.newLine();
      collector.newLine();
      expect(collector.children).toHaveLength(3);
      expect(collector.currentTarget).toBe(collector.children[2].Tokens);
    });
  });

  describe("push", () => {
    it("adds tokens to main array when no children", () => {
      const collector = new TokenCollector();
      const token = createToken(TokenKind.Keyword, "export");
      collector.push(token);
      expect(collector.tokens).toHaveLength(1);
      expect(collector.tokens[0]).toBe(token);
    });

    it("routes tokens to last child after newLine()", () => {
      const collector = new TokenCollector();
      collector.push(createToken(TokenKind.Text, "main"));
      collector.newLine();
      collector.push(createToken(TokenKind.Text, "child"));

      expect(collector.tokens).toHaveLength(1);
      expect(collector.tokens[0].Value).toBe("main");
      expect(collector.children[0].Tokens).toHaveLength(1);
      expect(collector.children[0].Tokens[0].Value).toBe("child");
    });

    it("accepts multiple tokens at once", () => {
      const collector = new TokenCollector();
      collector.push(
        createToken(TokenKind.Keyword, "a"),
        createToken(TokenKind.Keyword, "b"),
        createToken(TokenKind.Keyword, "c"),
      );
      expect(collector.tokens).toHaveLength(3);
    });
  });

  describe("newLine", () => {
    it("creates an empty child ReviewLine", () => {
      const collector = new TokenCollector();
      collector.newLine();
      expect(collector.children).toHaveLength(1);
      expect(collector.children[0].Tokens).toEqual([]);
    });

    it("allows building multi-line content", () => {
      const collector = new TokenCollector();
      collector.push(createToken(TokenKind.Text, "line1"));
      collector.newLine();
      collector.push(createToken(TokenKind.Text, "line2"));
      collector.newLine();
      collector.push(createToken(TokenKind.Text, "line3"));

      expect(collector.tokens.map((t) => t.Value)).toEqual(["line1"]);
      expect(collector.children).toHaveLength(2);
      expect(collector.children[0].Tokens.map((t) => t.Value)).toEqual(["line2"]);
      expect(collector.children[1].Tokens.map((t) => t.Value)).toEqual(["line3"]);
    });
  });

  describe("toResult", () => {
    it("returns only tokens when no children", () => {
      const collector = new TokenCollector();
      collector.push(createToken(TokenKind.Text, "hello"));
      const result = collector.toResult();
      expect(result.tokens).toHaveLength(1);
      expect(result.children).toBeUndefined();
    });

    it("includes children from newLine()", () => {
      const collector = new TokenCollector();
      collector.push(createToken(TokenKind.Text, "main"));
      collector.newLine();
      collector.push(createToken(TokenKind.Text, "child"));

      const result = collector.toResult();
      expect(result.tokens).toHaveLength(1);
      expect(result.children).toHaveLength(1);
      expect(result.children![0].Tokens[0].Value).toBe("child");
    });

    it("merges additional children with internal children", () => {
      const collector = new TokenCollector();
      collector.newLine();
      collector.push(createToken(TokenKind.Text, "internal"));

      const additional = [{ Tokens: [createToken(TokenKind.Text, "external")] }];
      const result = collector.toResult(additional);

      expect(result.children).toHaveLength(2);
      expect(result.children![0].Tokens[0].Value).toBe("internal");
      expect(result.children![1].Tokens[0].Value).toBe("external");
    });

    it("includes only additional children when no internal children", () => {
      const collector = new TokenCollector();
      collector.push(createToken(TokenKind.Text, "main"));

      const additional = [{ Tokens: [createToken(TokenKind.Text, "extra")] }];
      const result = collector.toResult(additional);

      expect(result.children).toHaveLength(1);
      expect(result.children![0].Tokens[0].Value).toBe("extra");
    });

    it("returns no children when both internal and additional are empty", () => {
      const collector = new TokenCollector();
      const result = collector.toResult([]);
      expect(result.children).toBeUndefined();
    });
  });
});
