module.exports = function(context, data) {
  return {
    // check to see if if the outer key exists at the outermost level
    existsInFile: function(node) {
      const outer = data.outer;
      const fileName = data.fileName;

      const properties = node.properties;
      let foundOuter = false;
      for (const property of properties) {
        if (property.key && property.key.value === outer) {
          foundOuter = true;
          break;
        }
      }
      context.getFilename() === fileName
        ? foundOuter
          ? []
          : context.report({
              node: node,
              message:
                fileName +
                ": " +
                outer +
                " does not exist at the outermost level"
            })
        : [];
    },

    // check to see if the value of the outer key matches the expected value
    outerMatchesExpected: function(node) {
      const outer = data.outer
      const expectedValue = data.expectedValue
      const fileName = data.fileName

      context.getFilename() === fileName
        ? node.value.value === expectedValue
          ? []
          : context.report({
              node: node,
              message:
                fileName +
                ": " +
                outer +
                " is set to {{ identifier }} when it should be set to " +
                expectedValue,
              data: {
                identifier: node.value.value
              }
            })
        : [];
    },

    // check that the inner key is a member of the outer key
    isMemberOf: function(node) {
      const outer = data.outer;
      const inner = data.inner;
      const fileName = data.fileName;

      const properties = node.value.properties;
      let foundInner = false;
      for (const property of properties) {
        if (property.key && property.key.value === inner) {
          foundInner = true;
          break;
        }
      }
      context.getFilename() === fileName
        ? foundInner
          ? []
          : context.report({
              node: node,
              message: fileName + ": " + inner + " is not a member of " + outer
            })
        : [];
        
    },

    // check the node corresponding to the inner value to see if it is set to true
    innerMatchesExpected: function(node) {
      const outer = data.outer;
      const inner = data.inner;
      const expectedValue = data.expectedValue;
      const fileName = data.fileName;

      context.getFilename() === fileName
        ? node.value.value === expectedValue
          ? []
          : context.report({
              node: node,
              message:
                fileName +
                ": " +
                outer +
                "." +
                inner +
                " is set to {{ identifier }} when it should be set to " +
                expectedValue,
              data: {
                identifier: node.value.value
              }
            })
        : [];
    }
  };
};
