package test

import (
	"testing"

	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/agent"
	"github.com/stretchr/testify/require"
)

// TestRoutingTenant_General validates routing tenant for all general channel testcases
func TestRoutingTenant_General(t *testing.T) {
	config.LoadEnvFile()
	config.InitConfiguration()
	config.InitSecrets()
	config.InitOpenAIClient()

	service, err := agent.NewCompletionService()
	require.NoError(t, err)

	// Each testcase: query, expected tenant (always model.TenantID_AzureSDKOnboarding for general channel)
	testcases := []struct {
		name    string
		content string
		tenant  model.TenantID
	}{
		{
			"SDK Validation - .NET",
			`I have an open PR inital genome api spec by samira-farhin · Pull Request #25097 · Azure/azure-rest-api-specs-pr that's currently failing on SDK Validation - .NET - PR. My latest changes to resolve SDK Validation in "tspconfig.yaml" are included below. After these updates, the SDK validation issues for Go and Java were resolved, but the C# issue still remains. I'm unsure what else needs to be addressed.\ninital genome api spec by samira-farhin · Pull Request #25097 · Azure/azure-rest-api-specs-pr\ninital genome api spec by samira-farhin · Pull Request #25097 · Azure/azure-rest-api-specs-pr\n\ncommand  pwsh ./eng/scripts/Automation-Sdk-Init.ps1 ../azure-sdk-for-net-pr_tmp/initInput.json ../azure-sdk-for-net-pr_tmp/initOutput.json\ncommand  pwsh ./eng/scripts/Invoke-GenerateAndBuildV2.ps1 ../azure-sdk-for-net-pr_tmp/generateInput.json ../azure-sdk-for-net-pr_tmp/generateOutput.json\ncmdout  [.Net] Start to call tsp-client to generate package:Azure.ResourceManager.Genome\ncmdout  [.Net] Start to build sdk project: /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src\ncmdout  [.Net] /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Generated/GenomeAccountCollection.cs(270,20): error CS0122: 'GeneratorPageableHelpers' is inaccessible due to its protection level [/mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Azure.ResourceManager.Genome.csproj::TargetFramework=netstandard2.0]\n...`,
			model.TenantID_DotnetChannelQaBot,
		},
		{
			"Permission to merge to RPSaaSMaster",
			`Hi\nGeneral\n,\nDue to recent team reorganization I am now in charge of my team and I need to get permission to merge changes to RPSaaSMaster, e.g. I need to merge this PR - Small fix for Dsts Sci Groups by Alessar · Pull Request #24977 · Azure/azure-rest-api-specs-pr\nCould you please help me with that?\nThank you`,
			model.TenantID_AzureSDKOnboarding,
		},
		{
			"MSWB API spec removal",
			`The Azure Modeling and Simulation Workbench (MSWB) preview service has been retired, so I'm trying to remove its related API specs from the REST API specs repository.\nPR Deleting 5 API specs for the deprecated MSWB service - RPSaaSDev by yochu-msft · Pull Request #2508… targets RPSaaSDev to delete 5 API specs.\nI want to move forward with merging this PR without resolving the Swagger LintDiff failure since the specs are being removed.\n'Next Steps to Merge' says "If you still want to proceed merging this PR without addressing the above failures, refer to step 4 in the PR workflow diagram." but 'PR workflow diagram' step 4 loops back that "Follow the instructions in the Next Steps to Merge comment." How should I merge the PR without fixing the failure?\ncc Mick Zaffke`,
			model.TenantID_APISpecReviewBot,
		},
		{
			"Deprecation of PostgreSql Single Server and its SDK Guidance",
			`Hi, PostgreSql Single Server officially deprecated in March 2025 with support completely ending September 2025. Efforts these past few months have been made to migrate users to PostgreSql Flexible Server.\nWe wish to remove the operations associated with single server found here: https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/postgresql/Azure.ResourceManager.PostgreSql/src/PostgreSql\nWant to confirm what is the exact process to remove the operations:\nIs official request needed?\nDo we manually remove the files associated to the resource?\nThank you for the help.`,
			model.TenantID_AzureSDKOnboarding,
		},
		{
			"Confirm deprecation workflow for HDInsightOnAks (Preview, usage = 0): API & SDK",
			`Hi team,\nHDInsightOnAks service is already in deprecation, which is Preview and currently has 0 usage. We need to retire both API and SDKs. After reviewing some discussions about Deprecation of PostgreSql, I’ve drafted the following workflow and would like the SDK team to confirm and help clarify a few questions.\nWorkflow and Questions:\n1. Remove the HDInsightOnAks spec folder from azure-rest-api-specs\n2. After removing the HDInsightOnAks API, send SDK deprecation email to Josephine\na) For SDK deprecation, do we only need to send an email? Is there a recommended email template?\nb) For each language repo, should I manually remove the code, or will the SDK team handle the removal?\nc) Is the final deprecation outcome to add a Deprecated annotation?\nPlease confirm and add any missing steps. Thanks!`,
			model.TenantID_AzureSDKOnboarding,
		},
		{
			"Official API documentation publishing",
			`General,\nHow do we generate official API documentation like below?\nAzure REST API reference documentation | Microsoft Learn\nFor a new RP like below. Is this done by the SDK team?\n[azure-rest-api-specs/specification/cdn/resource-manager/Microsoft.Cdn/Cdn/preview/2024-07-22-previe…](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/cdn/resource-manager/Microsoft.Cdn/Cdn/preview/2024-07-22-preview/edgeaction.json)`,
			model.TenantID_AzureSDKOnboarding,
		},
		{
			"Clarification on Folder Structure Alignment for CP and DP SDKs",
			`Hi\nGeneral\n,\nOur CP SDKs for both .NET and JavaScript are currently placed under the playwright folder.\njs->azure-sdk-for-js/sdk/playwright at main · Azure/azure-sdk-for-js\nnet->azure-sdk-for-net/sdk/playwright at main · Azure/azure-sdk-for-net\nwhile the non-Azure (DP) SDKs are placed under the loadtesting folder.\njs->azure-sdk-for-js/sdk/loadtesting at main · Azure/azure-sdk-for-js\nnet->azure-sdk-for-net/sdk/loadtestservice at main · Azure/azure-sdk-for-net\nwe wanted to confirm:\nIs it important or recommended to move the DP SDKs under the playwright folder to align with the CP SDK structure or the current structure is fine?\nIf yes, could you please guide us on the recommended process or steps to make this shift (e.g., repo structure changes, PR approach, or naming conventions to follow)?\nContext:\nAs part of the retirement of Microsoft Playwright Testing (MPT) and its merger into Azure App Testing, we deprecated the old SDK packages associated with MPT and created new alternative packages about whose folder structure I mentioned above.\nMPT’s core web testing capabilities are being integrated into Azure App Testing to provide a unified experience for both Load Testing and Playwright-based web testing. The standalone MPT service will be retired, and customers will need to migrate to Azure App Testing.\nQiaoqiao Zhang Please help us with this.\nThanks\ncc Madhuri`,
			model.TenantID_GeneralQaBot,
		},
		{
			"Error running 'npx prettier': Cannot find module 'prettier/plugins/estree'",
			`$  npx prettier --write specification/maps/data-plane/Weather/stable/1.1/weather.json\n[error] Cannot find module 'prettier/plugins/estree'\n[error] Require stack:\n[error] - C:\\github\\azure-rest-api-specs\\eng\\scripts\\prettier-swagger-plugin.js\n[error] - C:\\github\\azure-rest-api-specs\\node_modules\\prettier\\index.js\n[error] - C:\\github\\azure-rest-api-specs\\node_modules\\prettier\\cli.js\n[error] - C:\\github\\azure-rest-api-specs\\node_modules\\prettier\\bin-prettier.js`,
			model.TenantID_APISpecReviewBot,
		},
		{
			"Override description property in referenced parameter.",
			`In this PR I need to override the description for the typeahead parameter for a specific API, so that all other API that reference this property will not be affected:\nBut this is throwing an error:\nerror: Schema violation: must NOT have additional properties (paths > /search/address/{format} > get > parameters > 4)\nadditionalProperty: description\nI don't know why it is throwing an error, it worked in a similar situation for the BoundingBoxCompassNotation object in PR38554`,
			model.TenantID_APISpecReviewBot,
		},
		{
			"doc only update?",
			`Hey team,\nI'm a PM on the VMSS team. I'm trying to understand the new Typespec way to do a documentation only update to swagger? In this case, I just want to update these doc strings to remove/modify the preview notes. I believe I updated the correct text in the models.tsp files. The swagger compiled correctly. Now my questions:\nI want this to be updated in current documentation posted online, as well as SDK docs. How do I submit this change against main or the latest feature branch? Do I need to do additional work to make sure this change is propagated through to SDKs?\nWhen I submitted the PR, got this error. Not sure how to fix: "The default tag contains multiple API versions swaggers."\nThe PR for reference:\n[VMSS prioritizeunhealthyvm and force delete doc update by fitzgeraldsteele · Pull Request #25785 ](https://github.com/Azure/azure-rest-api-specs-pr/pull/25785)`,
			model.TenantID_APISpecReviewBot,
		},
	}

	for _, tc := range testcases {
		t.Run(tc.name, func(t *testing.T) {
			messages := []model.Message{{
				Role:    model.Role_User,
				Content: tc.content,
			}}
			llmMessages := convertToLLMMessages(messages)
			routedTenantID, _ := service.RouteTenant(model.TenantID_GeneralQaBot, llmMessages)
			require.NotNil(t, routedTenantID)
			require.Equal(t, tc.tenant, routedTenantID, "Testcase '%s' should route to %s tenant, but got %s", tc.name, tc.tenant, routedTenantID)
		})
	}
}

// TestRoutingTenant_APISpecReview validates routing tenant for testcases from API spec review channel
func TestRoutingTenant_APISpecReview(t *testing.T) {
	config.LoadEnvFile()
	config.InitConfiguration()
	config.InitSecrets()
	config.InitOpenAIClient()

	service, err := agent.NewCompletionService()
	require.NoError(t, err)

	testcases := []struct {
		name    string
		content string
		tenant  model.TenantID
	}{
		{
			"Suppression Review in RPSaaSDev",
			`Hi, I just created a PR to RPSaaSDev for our product which will help us test using the new RestrictTrafficToTestTenants feature.
The idea is that the changes we plan to merge to RPSaaSMaster will be mirrored in a dedicated folder in RPSaaSDev so we can test the exact swaggers we plan to ship, while we have another 'dev' folder where we put our iterative swagger changes which we plan to ship later.
I raised the PR against RPSaaSDev, but it got flagged for a suppression review. Is this a merge blocker for RPSaaSDev? I think it just got flagged since it's a new folder, but its the same suppressions that are already approved for that namespace in our dev folder as well as in RPSaaSMaster. Can we just merge? Thanks in advance!
(This dual folder setup will never be reflected for our product in RPSaaSMaster by the way, just a development helper for us!)`,
			model.TenantID_APISpecReviewBot,
		},
		{
			"How to Sdk generation in local",
			`I have couple of prs failing with sdk validation.
Is there a way to reproduce these errors in local?`,
			model.TenantID_AzureSDKOnboarding,
		},
		{
			"Update the enum for the existing API version",
			`We have the api-spec for verion 2025-08-01 for the storage mover.
This version is not live yet.And no customer are using this. If I have to change a Enum and also add new optional for this version, can it be changed and we get the approval from the breaking change team?`,
			model.TenantID_APISpecReviewBot,
		},
		{
			"Assistance required with breaking change PR",
			`Hi team,
We have the following open PR which adds a few missing fields to our returned payload and seems to be marked as a breaking change (violation of rule 1041 - AddedPropertyInResponse). Since we are a REST API only, I'm not sure how counts as a breaking change as it changes nothing about the way existing customers interact with our APIs.
Is there some way to suppress this rule/ request an exception?`,
			model.TenantID_APISpecReviewBot,
		},
		{
			"Quick Check on PR Review Status",
			`Hi API Spec Review,
I noticed this PR has been waiting for review for a couple of days—which is totally fine—but I just wanted to check in to confirm whether it's showing up as ready for review on your end. The GitHub bot added the ARMSignedOff label, but it hasn't been approved yet, so I'm wondering if it's currently in the review queue or if there's anything else needed from our side.
Thanks in advance!`,
			model.TenantID_APISpecReviewBot,
		},
		{
			"Licence agreement",
			`Hi,
We have a PR which has all pipelines passing except the license agreement.
It looks like only author's license agreement comment will be considered by the automated pipeline.
Can someone help to resolve this? author of this PR won't be available for a while and i have taken over this PR now`,
			model.TenantID_APISpecReviewBot,
		},
		{
			"Avocado warning",
			`Hi,
Need some help on this PR - Swagger Avocado and Swagger BreakingChange validations fail, because in this PR we are removing Microsoft.PlannedQuota namespace and the corresponding specification file.
This is completely expected and safe to remove, as this namespace was created in the previous PR and was never in use.
Please note, Microsoft.PlannedQuota was neither registered nor in use.
Could you help on how to bypass this?`,
			model.TenantID_APISpecReviewBot,
		},
		{
			"TypeSpec Errors as a result of moving existing swaggers to different directory",
			`In PR37469 we are moving swagger files related to existing customer facing API that are GA/stable, but are located in a 'preview' directory and are moving them to a 'stable' directory. As a result are getting multiple TypeSpec Errors because these were not created using TypeSpec. There are no plans to change existing released API. Is there any reason these errors cannot be suppressed?
Would the following suppression be adequate?
suppress: TypeSpec
from: <filename>.json
reason: The reason for this suppression is the API is already released and introducing changes create undo customer risk.`,
			model.TenantID_APISpecReviewBot,
		},
		{
			"Guidelines for revising an already published API spec?",
			`Hello, we recently published a new API version, however, we realized that a property should have been marked optional. Are there guidelines on how to revise an already published API version? We have not announced this version to our customer yet.`,
			model.TenantID_APISpecReviewBot,
		},
		{
			"Creating new service typespec definition",
			`Hello,
I'm currently writing a new definition for a service and wanted to ask if there's any guidance on how to go about it. I saw this guide however it doesn't go over creating the service folder and creating the readme.md file, please help with this, thanks`,
			model.TenantID_AzureSDKOnboarding,
		},
		{
			"Creating a Management Plane RP and referencing Data Plane model",
			`Hello API Spec Review, I'm currently working on creating a new management plane RPAAS RP in the private spec repo based off of the RPSaaSMaster branch, the service spec requires a reference to Compute RP model VMProfileProperties however the specification\\compute folder for Compute is only available in the main branch of the private repo, so I wanted to ask:
Is it possible to create a RPAAS RP based on the main branch in the private repo?
Can a RPAAS RP reference a model in a data plane RP?
Is there a way to reference the compute model from the RPSaaSMaster branch?
Thanks!`,
			model.TenantID_APISpecReviewBot,
		},
		{
			"IntegerTypeMustHaveFormat errors",
			`I've got multiple IntegerTypeMustHaveFormat errors in existing swagger files in my PR. These swagger files all represent stable versions of Azure Maps REST API that have been available to customers for some time, I'm just moving the files to different directories, so changes could potentially have customer impact. If there is no format defined, what does it default to? I would imagine int32 given an int in C# is int32 (int64 is a long)... Does this even apply to REST API? If the default value is int32 for example, would it be appropriate to define them all as "format": "int32",? or would it be better to suppress these errors?`,
			model.TenantID_APISpecReviewBot,
		},
		{
			"Trying to understand API spec validation errors",
			`Hello all,
I just published a new PR for adding a new API version for our service. I followed the same procedure we have followed over last couple of years, but I am seeing validation errors which are pretty hard to decipher with my limited knowledge regarding them.
Can someone please take a look and help me narrow down the issues?
The best I can see is that this file is missing?
Cause: ResolverError: Error reading file "/home/runner/work/azure-rest-api-specs/azure-rest-api-specs/after/specification/storagecache/common-types/resource-management/v3/types.json"`,
			model.TenantID_APISpecReviewBot,
		},
		{
			"Generating Dictionary<string,object> type for swagger",
			`Hi API Spec Review,
We are currently using IDictionary<string, object> in our models to allow passing through various key-value pairs, where the values can be a string, an array, or other types. So I defined the property in Swagger and provided examples in one PR, but encountered errors during PR validation.
Could you please suggest the right way to define such a flexible dictionary type in Swagger, and provide an example?`,
			model.TenantID_APISpecReviewBot,
		},
	}

	for _, tc := range testcases {
		t.Run(tc.name, func(t *testing.T) {
			messages := []model.Message{{
				Role:    model.Role_User,
				Content: tc.content,
			}}
			llmMessages := convertToLLMMessages(messages)
			routedTenantID, _ := service.RouteTenant(model.TenantID_APISpecReviewBot, llmMessages)
			require.NotNil(t, routedTenantID)
			require.Equal(t, tc.tenant, routedTenantID, "Testcase '%s' should route to %s tenant, but got %s", tc.name, tc.tenant, routedTenantID)
		})
	}
}
