// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledgeAICompletion;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec;

namespace Azure.Sdk.Tools.Mock.Handlers.TypeSpec;

internal static class TypeSpecMockResponses
{
    public const string ContosoTypeSpecProject = "specification/contosowidgetmanager/Contoso.WidgetManager";
}

/// <summary>Mock handler for azsdk_typespec_generate_authoring_plan.</summary>
public class TypeSpecGenerateAuthoringPlanHandler : IMockToolHandler
{
    public string ToolName => "azsdk_typespec_generate_authoring_plan";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new TypeSpecAuthoringResponse
    {
        TypeSpecProject = TypeSpecMockResponses.ContosoTypeSpecProject,
        Solution = "Add a new operation `GetWidgetById` to the WidgetService and bump the api-version.",
        References =
        [
            new DocumentReference
            {
                Title = "TypeSpec authoring guide",
                Source = "azure-rest-api-specs-wiki",
                Link = "https://aka.ms/typespec-azure-guide"
            }
        ]
    };
}

/// <summary>Mock handler for azsdk_typespec_init_project.</summary>
public class TypeSpecInitProjectHandler : IMockToolHandler
{
    public string ToolName => "azsdk_typespec_init_project";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new TspToolResponse
    {
        TypeSpecProject = TypeSpecMockResponses.ContosoTypeSpecProject,
        IsSuccessful = true,
        NextSteps = ["Run `tsp compile .` to validate the new project."]
    };
}

/// <summary>Mock handler for azsdk_convert_swagger_to_typespec.</summary>
public class ConvertSwaggerToTypeSpecHandler : IMockToolHandler
{
    public string ToolName => "azsdk_convert_swagger_to_typespec";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new TspToolResponse
    {
        TypeSpecProject = TypeSpecMockResponses.ContosoTypeSpecProject,
        IsSuccessful = true,
        NextSteps =
        [
            "Review the converted TypeSpec for any TODOs.",
            "Run `tsp compile .` to validate."
        ]
    };
}

/// <summary>Mock handler for azsdk_run_typespec_validation.</summary>
public class RunTypeSpecValidationHandler : IMockToolHandler
{
    public string ToolName => "azsdk_run_typespec_validation";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new TypeSpecValidationResponse
    {
        TypeSpecProject = TypeSpecMockResponses.ContosoTypeSpecProject,
        PackageType = SdkType.Dataplane,
        Message = "TypeSpec validation passed (mock).",
        validationResults = ["No issues found."]
    };
}

/// <summary>Mock handler for azsdk_get_modified_typespec_projects.</summary>
public class GetModifiedTypeSpecProjectsHandler : IMockToolHandler
{
    public string ToolName => "azsdk_get_modified_typespec_projects";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new ObjectCommandResponse
    {
        Message = "Found 1 modified TypeSpec project (mock).",
        Result = new[] { TypeSpecMockResponses.ContosoTypeSpecProject }
    };
}

/// <summary>Mock handler for azsdk_typespec_check_project_in_public_repo.</summary>
public class TypeSpecCheckProjectInPublicRepoHandler : IMockToolHandler
{
    public string ToolName => "azsdk_typespec_check_project_in_public_repo";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new DefaultCommandResponse
    {
        Message = "TypeSpec project is present in the public azure-rest-api-specs repository (mock).",
        Result = true
    };
}

/// <summary>Mock handler for azsdk_typespec_delegate_apiview_feedback.</summary>
public class TypeSpecDelegateApiViewFeedbackHandler : IMockToolHandler
{
    public string ToolName => "azsdk_typespec_delegate_apiview_feedback";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new DefaultCommandResponse
    {
        Message = "APIView feedback delegated to the TypeSpec author (mock).",
        Result = new
        {
            commentsDelegated = 2,
            assignee = "contoso-typespec-author"
        }
    };
}

/// <summary>Mock handler for azsdk_customized_code_update.</summary>
public class CustomizedCodeUpdateHandler : IMockToolHandler
{
    public string ToolName => "azsdk_customized_code_update";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new CustomizedCodeUpdateResponse
    {
        Success = true,
        Message = "Customized code updated and rebuilt successfully (mock).",
        AppliedPatches =
        [
            new AppliedPatch(
                FilePath: "src/Generated/Customization/WidgetClientCustomization.cs",
                Description: "Renamed Get to GetWidget",
                ReplacementCount: 2)
        ]
    };
}
