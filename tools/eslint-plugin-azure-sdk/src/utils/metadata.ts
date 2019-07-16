import { Rule } from "eslint";

export const getRuleMetaData = (
  ruleName: string,
  ruleDescription: string
): Rule.RuleMetaData => ({
  docs: {
    description: ruleDescription,
    category: "Best Practices",
    recommended: true,
    url: `"https://github.com/Azure/azure-sdk-tools/blob/master/tools/eslint-plugin-azure-sdk/docs/rules/${ruleName}.md`
  },
  schema: []
});
