var processJSON = require("../../../lib/index").processors[".json"];

module.exports = function(fileName) {
  return Object.assign(processJSON, { filename: fileName });
};
