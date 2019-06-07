/**
 * @fileoverview Definition of processors
 * @author Arpan Laha
 */

export default {
  ".json": {
    preprocess: function(text: string): string[] {
      const code = "const json = " + text;
      return [code];
    },
    postprocess: function(messages: string[][]): string[] {
      return messages[0];
    }
  }
};
