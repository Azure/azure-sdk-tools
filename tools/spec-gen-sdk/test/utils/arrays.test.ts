import { describe, it, expect } from 'vitest';
import { any, where, toArray } from "../../src/utils/arrays";

describe("arrays", () => {
  describe("any", () => {
    it("should return true for non-empty array", () => {
      expect(any([1, 2, 3])).toBe(true);
    });

    it("should return false for empty array", () => {
      expect(any([])).toBe(false);
    });

    it("should return false for undefined", () => {
      expect(any(undefined)).toBe(false);
    });

    it("should return false for null", () => {
      expect(any(null as any)).toBe(false);
    });
  });

  describe("where", () => {
    it("should filter array based on condition", () => {
      const numbers = [1, 2, 3, 4, 5];
      const evenNumbers = where(numbers, (n) => n % 2 === 0);
      expect(evenNumbers).toEqual([2, 4]);
    });

    it("should return empty array when no items match condition", () => {
      const numbers = [1, 3, 5];
      const evenNumbers = where(numbers, (n) => n % 2 === 0);
      expect(evenNumbers).toEqual([]);
    });

    it("should handle empty array input", () => {
      expect(where([], (n) => true)).toEqual([]);
    });

    it("should throw error for null array", () => {
      expect(() => where(null as any, (n) => true)).toThrow();
    });

    it("should throw error for undefined array", () => {
      expect(() => where(undefined as any, (n) => true)).toThrow();
    });
  });

  describe("toArray", () => {
    it("should return same array if input is already an array", () => {
      const arr = [1, 2, 3];
      expect(toArray(arr)).toBe(arr);
    });

    it("should wrap single value in array", () => {
      expect(toArray(1)).toEqual([1]);
    });

    it("should use custom conversion function when provided", () => {
      const conversion = (n: number) => [n, n * 2];
      expect(toArray(2, conversion)).toEqual([2, 4]);
    });

    it("should handle null input with default conversion", () => {
      expect(toArray(null as any)).toEqual([null]);
    });

    it("should handle undefined input with default conversion", () => {
      expect(toArray(undefined as any)).toEqual([undefined]);
    });

    it("should handle empty array with custom conversion", () => {
      const conversion = (n: number) => [n, n * 2];
      expect(toArray([] as any[], conversion)).toEqual([]);
    });
  });
});
