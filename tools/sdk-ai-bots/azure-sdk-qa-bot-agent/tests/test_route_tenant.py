"""Integration tests for tenant routing.

Ported from the Go test suite in
``azure-sdk-qa-bot-backend/test/routing_tenant_test.go``.

These tests call the real LLM via :func:`tools.tenant_tools.TenantTools._llm_route`
to verify that queries are routed to the expected tenant.  They require:
  - ``AZURE_APPCONFIG_ENDPOINT`` env var set (for App Configuration)
  - Azure credentials available (``DefaultAzureCredential``)
  - Network access to the AI Foundry endpoint
"""

from __future__ import annotations

import sys
from pathlib import Path

import pytest
import pytest_asyncio

# Ensure the project root is on sys.path so ``config``, ``tools``, etc. resolve.
_PROJECT_ROOT = str(Path(__file__).resolve().parent.parent)
if _PROJECT_ROOT not in sys.path:
    sys.path.insert(0, _PROJECT_ROOT)

from dotenv import load_dotenv

load_dotenv()

from config.tenant_config import TenantID
from tools.tenant_tools import TenantTools

# ---------------------------------------------------------------------------
# Test cases (ported from Go)
# ---------------------------------------------------------------------------

ROUTING_TEST_CASES = [
    pytest.param(
        "SDK Validation - .NET",
        (
            'I have an open PR inital genome api spec by samira-farhin · Pull Request #25097 · Azure/azure-rest-api-specs-pr that\'s currently failing on SDK Validation - .NET - PR. My latest changes to resolve SDK Validation in "tspconfig.yaml" are included below. After these updates, the SDK validation issues for Go and Java were resolved, but the C# issue still remains. I\'m unsure what else needs to be addressed.\n'
            "inital genome api spec by samira-farhin · Pull Request #25097 · Azure/azure-rest-api-specs-pr\n"
            "inital genome api spec by samira-farhin · Pull Request #25097 · Azure/azure-rest-api-specs-pr\n"
            "\n"
            "command  pwsh ./eng/scripts/Automation-Sdk-Init.ps1 ../azure-sdk-for-net-pr_tmp/initInput.json ../azure-sdk-for-net-pr_tmp/initOutput.json\n"
            "command  pwsh ./eng/scripts/Invoke-GenerateAndBuildV2.ps1 ../azure-sdk-for-net-pr_tmp/generateInput.json ../azure-sdk-for-net-pr_tmp/generateOutput.json\n"
            "cmdout  [.Net] Start to call tsp-client to generate package:Azure.ResourceManager.Genome\n"
            "cmdout  [.Net] Start to build sdk project: /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src\n"
            "cmdout  [.Net] /mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Generated/GenomeAccountCollection.cs(270,20): error CS0122: 'GeneratorPageableHelpers' is inaccessible due to its protection level [/mnt/vss/_work/1/s/azure-sdk-for-net-pr/sdk/genome/Azure.ResourceManager.Genome/src/Azure.ResourceManager.Genome.csproj::TargetFramework=netstandard2.0]\n"
            "..."
        ),
        TenantID.GENERAL_QA_BOT.value,
        TenantID.DOTNET_CHANNEL_QA_BOT.value,
        id="SDK Validation - .NET",
    ),
    pytest.param(
        "How to Sdk generation in local",
        "I have couple of prs failing with sdk validation. Is there a way to reproduce these errors in local?",
        TenantID.AZURE_SDK_ONBOARDING.value,
        TenantID.AZURE_SDK_ONBOARDING.value,
        id="How to Sdk generation in local",
    ),
    pytest.param(
        "Permission to merge to RPSaaSMaster",
        (
            "Hi\nGeneral\n,\n"
            "Due to recent team reorganization I am now in charge of my team and I need to get permission to merge changes to RPSaaSMaster, "
            "e.g. I need to merge this PR - Small fix for Dsts Sci Groups by Alessar · Pull Request #24977 · Azure/azure-rest-api-specs-pr\n"
            "Could you please help me with that?\nThank you"
        ),
        TenantID.GENERAL_QA_BOT.value,
        TenantID.AZURE_SDK_ONBOARDING.value,
        id="Permission to merge to RPSaaSMaster",
    ),
    pytest.param(
        "MSWB API spec removal",
        (
            "The Azure Modeling and Simulation Workbench (MSWB) preview service has been retired, so I'm trying to remove its related API specs from the REST API specs repository.\n"
            "PR Deleting 5 API specs for the deprecated MSWB service - RPSaaSDev by yochu-msft · Pull Request #2508… targets RPSaaSDev to delete 5 API specs.\n"
            "I want to move forward with merging this PR without resolving the Swagger LintDiff failure since the specs are being removed.\n"
            "'Next Steps to Merge' says \"If you still want to proceed merging this PR without addressing the above failures, refer to step 4 in the PR workflow diagram.\" "
            "but 'PR workflow diagram' step 4 loops back that \"Follow the instructions in the Next Steps to Merge comment.\" "
            "How should I merge the PR without fixing the failure?\ncc Mick Zaffke"
        ),
        TenantID.API_SPEC_REVIEW_BOT.value,
        TenantID.API_SPEC_REVIEW_BOT.value,
        id="MSWB API spec removal",
    ),
    pytest.param(
        "Assistance required with breaking change PR",
        (
            "Hi team, We have the following open PR which adds a few missing fields to our returned payload and seems to be marked as a breaking change "
            "(violation of rule 1041 - AddedPropertyInResponse). Since we are a REST API only, I'm not sure how counts as a breaking change as it changes nothing "
            "about the way existing customers interact with our APIs. Is there some way to suppress this rule/ request an exception?"
        ),
        TenantID.API_SPEC_REVIEW_BOT.value,
        TenantID.API_SPEC_REVIEW_BOT.value,
        id="Assistance required with breaking change PR",
    ),
    pytest.param(
        "SDK Generation Pipeline - Python codegen from typespec missing required _validation.py file",
        (
            "We used SDK Validation - Python build pipeline to generate our azure-quantum python SDK. The artifacts of the build contains tar of the autogenerated client. "
            "However, the required _validation file is entirely missing from the generated client, so generated SDK is broken and invalid. "
            "Has anyone seen this before? Are we missing something in our typespec for _validation file to be generated properly?"
        ),
        TenantID.GENERAL_QA_BOT.value,
        TenantID.PYTHON_CHANNEL_QA_BOT.value,
        id="Python codegen missing _validation.py",
    ),
    pytest.param(
        "Java SDK generation failure",
        (
            "I have this pr where java sdk validation is failing, This is the pipeline: "
            "https://dev.azure.com/azure-sdk/public/_build/results?buildId=5517681&view=logs&j=83516c17-6666-5250-abde-63983ce72a49&t=00be4b52-4a63-5865-8e02-c61723ad0692"
        ),
        TenantID.GENERAL_QA_BOT.value,
        TenantID.JAVA_CHANNEL_QA_BOT.value,
        id="Java SDK generation failure",
    ),
    pytest.param(
        "404 Broken link in PR - JS",
        (
            "For JS - I see that the pipeline run has passed successfully but in the PR we are getting the broken error: "
            "[404] broken link https://learn.microsoft.com/javascript/api/@azure/arm-dell-storage. "
            "Do you know what's causing this or how to fix it?"
        ),
        TenantID.GENERAL_QA_BOT.value,
        TenantID.JAVASCRIPT_CHANNEL_QA_BOT.value,
        id="404 Broken link JS",
    ),
    pytest.param(
        "API Spec onboarding",
        "I am from a  service team and need to work on the SDKs. Here is the API spec. what should I do? ",
        TenantID.GENERAL_QA_BOT.value,
        TenantID.AZURE_SDK_ONBOARDING.value,
        id="API Spec onboarding",
    ),
    pytest.param(
        "LRO header return 200",
        (
            "Hi, I'm from the servicefabric RP team working with [azure-rest-api-specs/specification/servicefabricmanagedclusters/resource-manager/Microsoft.ServiceF…]"
            "(https://github.com/Azure/azure-rest-api-specs/blob/main/specification/servicefabricmanagedclusters/resource-manager/Microsoft.ServiceFabric/ServiceFabricManagedClusters/preview/2025-06-01-preview/servicefabricmanagedclusters.json)\n"
            "On our operations for ManagedClusters_CreateOrUpdate, NodeTypes_CreateOrUpdate, and ApplicationTypeVersions_CreateOrUpdate, we have both a 200 and 202 response defined.\n"
            "A 200 response is returned when the customer sends the initial request to create the resource. A 202 is returned on every subsequent PUT.\n"
            "I recently noticed that our service code returns an async operation URI in the 200 response for the above operations. "
            "https://msazure.visualstudio.com/One/_git/winfab-RP?path=/src/sfmc/SfmcBackendService/Service/Controllers/ClustersController.cs&version=GBdevelop&line=294&lineEnd=295&lineStartColumn=1&lineEndColumn=1&lineStyle=plain&_a=contents\n"
            "```\nHTTP/1.1 200 OK\nContent-length: 2131\nContent-Type: application/json; charset=utf-8\nServer: Microsoft-HTTPAPI/2.0\n"
            "Azure-AsyncOperation: http://localhost:8080/subscriptions/b36cdf46-b75d-4dc2-9fe1-1296ee8c623d/providers/Microsoft.ServiceFabric/locations/southcentralus/managedclusteroperations/c39cd8e1-18a0-41fc-a778-6a4ad6adbd653?api-version=2024-02-01\n"
            "Date: Thu, 23 Oct 2025 22:57:10 GMT\nConnection: close\n```\n"
            "Should I specify this in the spec? If so, what is the recommendation for doing so? I couldn't find an example for a 200 response with an async operation header."
        ),
        TenantID.AZURE_SDK_QA_BOT.value,
        TenantID.AZURE_SDK_QA_BOT.value,
        id="LRO header return 200",
    ),
    pytest.param(
        "Typespec Validation Failing on PR",
        (
            "Hi TypeSpec Discussion,\n"
            "CI has been failing constantly for our PR ([Azure Load Testing\\] Add 2025-03-01-preview Data-Plane APIs by Harshan01 · Pull Request #32585 · Azure/azure-rest-api-specs]"
            "(https://github.com/Azure/azure-rest-api-specs/pull/32585)) for Typespec Validation step. "
            "The logs show that we are missing the Go SDK configuration and I am able to produce this error locally as well. "
            "However, our service doesnt have a Go SDK and we are not planning to put it in scope right now. "
            "This check has suddenly started failing for our PRs, what should we do?\n"
            "```\nExecuting rule：SdkTspConfigVa1idation\n"
            "[SdkTspconfigVa1idation]：validation failed．\n"
            "- Failed to find \"options.@azure-tools/typespec-go.generate-fakes\"．Please add \"options.@azure-tools/typespec-go.generate-fakes\".\n"
            "- Failed to find \"options.@azure-tools/typespec-go.inject一spans\"．Please add \"options.@azure-tools/typespec-go.inject一spans.\n"
            "- Failed to find \"options.@azure-tools/typespec-go.service-dir\"．Please add \"options.@azure-tools/typespec-go.service-dir\".\n"
            "- Failed to find \"options.@azure-tools/typespec-go.package-dir\"．Please add\"options.@azure-tools/typespec-go.package-dir\".\n"
            "Please See https://aka.ms/azsdk/spec-gen-sdk-config for more info．\n"
            "For additional information on TypeSpec validation, please refer to https://aka.ms/azsdk/specs/typespec-validation.\n```"
        ),
        TenantID.AZURE_SDK_QA_BOT.value,
        TenantID.AZURE_SDK_QA_BOT.value,
        id="Typespec Validation Failing",
    ),
    pytest.param(
        "ArmResourcePatchAsync & discriminator",
        (
            "title: ArmResourcePatchAsync & discriminator\n\n"
            "question: I was trying to figure out the best way to allow an update for a Host resource. "
            "I tried ArmResourcePatchAsync, but the resource has a discriminator. oav fails with OBJECT_MISSING_REQUIRED_PROPERTY_DEFINITION .  "
            "[Windows Server AHB for VMware via PATCH by cataggar · Pull Request #25538 · Azure/azure-rest-api-sp…]"
            "(https://github.com/Azure/azure-rest-api-specs-pr/pull/25538)"
        ),
        TenantID.AZURE_SDK_QA_BOT.value,
        TenantID.AZURE_SDK_QA_BOT.value,
        id="ArmResourcePatchAsync & discriminator",
    ),
    pytest.param(
        "Grant permission to view workflow",
        (
            "Hi team, could someone please help grant me permission to view the workflow for my Azure REST API PR?\n"
            "Right now, after pushing my commit, I'm unable to see the error details for the validation checks, "
            'it just says "at least one review required to see the workflow." '
            "This makes it difficult to verify if my changes are passing validation before the final review.\n"
            "Would it be possible to enable workflow visibility for me so I can debug and ensure everything is in order ahead of time? "
            "PR link: [Stable version 2025-09-01 with prevalidation and autoscale changes by prachinandi · Pull Request #3…]"
            "(https://github.com/Azure/azure-rest-api-specs/pull/37218)"
        ),
        TenantID.AZURE_SDK_QA_BOT.value,
        TenantID.AZURE_SDK_ONBOARDING.value,
        id="Grant permission workflow",
    ),
    pytest.param(
        "SDK Validation suppressions.yaml",
        (
            "I have these lines in my suppressions.yaml \n \nYAML\n- tool: TypeSpecValidation\n  reason: >\n"
            "    Not ready to generate SDKs from TypeSpec.\n"
            "    Responsibility: Service team with SDK team collaboration.\n"
            "    More info: https://aka.ms/azsdk/spec-gen-sdk-config.\n"
            "  rules: \n  - SdkTspConfigValidation\n  paths: \n"
            "  - HybridContainerService.Management/tspconfig.yaml\n \n"
            "But PR is still running SDK validations and failing. Did the suppressions method change? "
        ),
        TenantID.GENERAL_QA_BOT.value,
        TenantID.API_SPEC_REVIEW_BOT.value,
        id="SDK Validation suppressions.yaml",
    ),
    pytest.param(
        "How to make an interface internal?",
        (
            "I am looking for ways to make an interface internal so that it does not appear in public interface of python SDK. "
            "This is the interface which emits `EvaluationResultsOperations` which shows up on client. "
            "I would like to generate it but keep it hidden from public interface.\n"
            "I tried following:\n"
            "1. Mark all operations under it internal\n"
            "2. Adding @access decorator to interface but that fails.\n"
            "Is there a way to achieve it ?"
        ),
        TenantID.AZURE_SDK_QA_BOT.value,
        TenantID.AZURE_SDK_QA_BOT.value,
        id="How to make interface internal",
    ),
]


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
class TestRouteTenant:
    """Integration tests for LLM-based tenant routing.

    Each test case sends a user query + original tenant to the LLM and asserts
    the recommended tenant matches the expected value.
    """

    @pytest.mark.parametrize(
        "name, content, original_tenant, expected_tenant",
        ROUTING_TEST_CASES,
    )
    async def test_routing(
        self,
        name: str,
        content: str,
        original_tenant: str,
        expected_tenant: str,
    ) -> None:
        tools = TenantTools()
        routed = await tools._llm_route(original_tenant, content)
        assert routed == expected_tenant, (
            f"[{name}] expected '{expected_tenant}', got '{routed}'"
        )
