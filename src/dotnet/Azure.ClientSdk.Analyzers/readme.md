## How to test WIP Azure.ClientSdk.Analyzers against azure-sdk-for-net
1. Make changes into a feature branch in azure-sdk-tools.
2. Run [release pipeline](https://dev.azure.com/azure-sdk/internal/_build?definitionId=2945&_a=summary) for the feature branch.
3. Create a draft PR in azure-sdk-for-net bumping to the newly created dev version.
4. If we are satisfied with the results and all other PR comments are handled we can merge the azure-sdk-tools PR.
5. A new dev version will be published with a release from main branch.
6. Update the azure-sdk-for-net PR to use the newest version and merge the azure-sdk-for-net PR.
