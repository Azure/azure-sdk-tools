import { describe, it, expect } from 'vitest';
import { createDocsReviewLines } from '../../../src/process-items/utils/generateDocReviewLine';
import { TokenKind } from '../../../src/models/apiview-models';
import { Item } from '../../../rustdoc-types/output/rustdoc-types';

// Create a base item with all required Item properties
const createBaseItem = (id: number): Item => ({
  id: id,
  crate_id: 0,
  visibility: 'public',
  links: {},
  attrs: [],
  inner: { module: { items: [], is_crate: false, is_stripped: false } }
});

describe('generateDocReviewLine', () => {
  describe('createDocsReviewLines', () => {
    it('should create doc lines for an item with docs', () => {
      const item = {
        ...createBaseItem(123),
        docs: 'This is a documentation line.\nThis is a second documentation line.'
      };
      
      const result = createDocsReviewLines(item);
      
      expect(result).toHaveLength(2);
      
      // Check first line
      expect(result[0].LineId).toBe('123_docs_0');
      expect(result[0].Tokens).toHaveLength(1);
      expect(result[0].Tokens[0].Kind).toBe(TokenKind.Comment);
      expect(result[0].Tokens[0].Value).toBe('/// This is a documentation line.');
      
      // Check second line
      expect(result[1].LineId).toBe('123_docs_1');
      expect(result[1].Tokens).toHaveLength(1);
      expect(result[1].Tokens[0].Kind).toBe(TokenKind.Comment);
      expect(result[1].Tokens[0].Value).toBe('/// This is a second documentation line.');
    });
    
    it('should handle empty docs array', () => {
      const item = {
        ...createBaseItem(123),
        docs: ''
      };
      
      const result = createDocsReviewLines(item);
      
      expect(result).toEqual([]);
    });
    
    it('should handle null or undefined docs', () => {
      const items = [
        { ...createBaseItem(123), docs: null },
        { ...createBaseItem(456), docs: undefined }
      ];
      
      items.forEach(item => {
        const result = createDocsReviewLines(item);
        expect(result).toEqual([]);
      });
    });
    
    it('should handle items with generated id', () => {
      // Create a complete item with all required properties including id
      const item = {
        ...createBaseItem(789),
        docs: 'Documentation'
      };
      
      const result = createDocsReviewLines(item);
      
      expect(result).toHaveLength(1);
      expect(result[0].LineId).toBe('789_docs_0');
    });
  });
});
