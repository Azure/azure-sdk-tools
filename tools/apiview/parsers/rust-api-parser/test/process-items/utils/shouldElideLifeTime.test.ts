import { describe, it, expect } from 'vitest';
import { shouldElideLifetime } from '../../../src/process-items/utils/shouldElideLifeTime';

describe('shouldElideLifetime', () => {
  it('should return true for lifetimes starting with \'life', () => {
    const lifetimeStrings = ["'life", "'lifetime", "'life123"];
    
    lifetimeStrings.forEach(lifetime => {
      expect(shouldElideLifetime(lifetime)).toBe(true);
    });
  });
  
  it('should return true for numeric lifetimes', () => {
    const numericLifetimes = ["123", "456", "0"];
    
    numericLifetimes.forEach(lifetime => {
      expect(shouldElideLifetime(lifetime)).toBe(true);
    });
  });
  
  it('should return false for other lifetimes', () => {
    const otherLifetimes = ["'a", "'static", "'_", "'_#1"];
    
    otherLifetimes.forEach(lifetime => {
      expect(shouldElideLifetime(lifetime)).toBe(false);
    });
  });
  
  it('should handle empty string', () => {
    expect(shouldElideLifetime('')).toBe(false);
  });
});
