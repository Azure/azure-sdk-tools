import { OperationParameter } from "@azure/core-client";

export const apiVersion: OperationParameter = {
  parameterPath: "apiVersion",
  mapper: {
    defaultValue: "2024-01-01",
    isConstant: true,
    serializedName: "api-version",
    type: {
      name: "String"
    }
  }
};

export const contentType: OperationParameter = {
  parameterPath: ["options", "contentType"],
  mapper: {
    defaultValue: "application/json",
    isConstant: true,
    serializedName: "Content-Type",
    type: {
      name: "String",
    },
  },
};
