
# Feature Request: cross language ids

## Summary
Add a new feature to support cross language ids for JavaScript api parser.

## Problem Statement
By adding cross-language ids to our Apis we allow api reviewers to co-relate Apis that are generated from the same service Api spec.

## Proposed Solution
An additional positional command line option is added. It would be optional and be a file path to a `metadata.json` file that similar to #metadata.json.  When argument to this option is specified, the parser tool should read the file, parse the content as a JSON object, extract the `crossLanguageDefinitions` field from parsed JSON object. The `crossLanguageDefinitions.CrossLanguageDefinitionId` property is a mapping between the JS api canonical reference and cross language ids.  In `buildMember` function, if we have a pair of `"{api item id}" : "{some cross language id}"`, then in addition to set `LineId` to the `itemId`, we also set `CrossLanguageId` to the value that maps to the api item id key.

## Acceptance Criteria
- [ ] optional command line option added
- [ ] `CrossLanguageDefinitionId` mapping extract and pass to functions that generate review lines
- [ ] `CrossLanguageId` set for api items whose item id exists in the mapping as a key
- [ ] some unit test added as passing
- [ ] CLI help and readme example updated to show usage of the new option

## Alternatives Considered
What other approaches have you considered? Why is your proposed solution preferred?
