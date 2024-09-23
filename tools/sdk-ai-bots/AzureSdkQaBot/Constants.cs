namespace AzureSdkQaBot
{
    public class Constants
    {
        public const string Action_BreakingChangeReview = "Follow [breaking changes process](https://eng.ms/docs/cloud-ai-platform/azure-core/azure-core-pm-and-design/trusted-platform-pm-karimb/service-lifecycle-and-actions-team/service-lifecycle-actions-team/apex/media/launchingproductbreakingchanges#breaking-change-process-1)";
        public const string Action_ArmReview_NotFinished_BeforeSdkBreakingChangeReview = "Please finish ARM review first.";
        public const string Action_RequestMerge_MgmtPlane = "Please add the following comment to your pull request: /pr RequestMerge";
        public const string Action_RequestMerge_DataPlane = "Please reach out to the REST API Stewardship board to formally request the merging of your pull request.";

        public const string Action_SDKBreakingChangeGo_NotFinished = "If the review of breaking changes in the Go SDK has not been completed within two business days, please reach out to **Ray Chen** for assistance.";
        public const string Action_SDKBreakingChangePython_NotFinished = "If the review of breaking changes in the Python SDK has not been completed within two business days, please reach out to **Yuchao Yan** for assistance.";
        public const string Action_SDKBreakingChangeJavaScript_NotFinished = "If the review of breaking changes in the JavaScript SDK has not been completed within two business days, please reach out to **Qiaoqiao Zhang** for assistance.";

        public const string Action_BreakingChangeReview_Finished = "Please refer to the 'Automated merging requirements met' CI check result to determine if this pull request is ready for merging.";

        public const string Message_SDKBreakingChangeGo_NotFinished = "Typically, a review of breaking changes in the Go SDK requires two business days.";
        public const string Message_SDKBreakingChangePython_NotFinished = "Typically, a review of breaking changes in the Python SDK requires two business days.";
        public const string Message_SDKBreakingChangeJavaScript_NotFinished = "Typically, a review of breaking changes in the JavaScript SDK requires two business days.";

        public const string Message_ArmReview_NotFinished_BeforeSdkBreakingChangeReview = "The ARM review has not been completed yet. Once it is finished, the SDK breaking change review will start automatically.";

        public const string Message_GoSdkReview_Finished = "Go SDK review has been completed.";
        public const string Message_PythonSdkReview_Finished = "Python SDK review has been completed.";
        public const string Message_JavaScriptSdkReview_Finished = "JavaScript SDK review has been completed.";

        public const string Message_BreakingChangeReview_Finished = "Breaking change review has been completed.";
        public const string Message_Review_Finished = "All review stages have been completed, and this pull request is now ready to be merged.";

        public const string Message_PrIsMerged = "Your pull request has been merged or closed.";
        public const string Message_SDKBreakingChangeReview_NotNeeded = "Since there are no reported SDK breaking changes in your pull request, it does not require a review for SDK breaking changes.";

        public const string Message_FurtherHelp_Pipeline = " If you need further assistance with the stuck pipeline issues, please reach out **Konrad Jamrozik**.";
        public const string Message_FurtherHelp_Avocado = " If you need further assistance with the avocado error, please reach out **Konrad Jamrozik**.";
        public const string Message_FurtherHelp_Oav = " If you need further assistance with the model validation or semantic validation, please reach out **Scott Beddall**.";
        public const string Message_FurtherHelp_LintTool = " For additional help with the lintDiff or lintRPaaS rules, please contact **Roopesh Manda**. If you require further assistance with any tool-related errors, please reach out to **Konrad Jamrozik**.";
        public const string Message_FurtherHelp_Oad = " If you need further assistance with the breaking change tool errors, please reach out **Konrad Jamrozik**.";
        public const string Message_FurtherHelp_TypeSpecValidation = " If you need further assistance with the TypeSpecValidation errors, please reach out **Mike Harder**.";
        public const string Message_FurtherHelp_ApiDocPreview = " If you need further assistance with the apiDocPreview errors, please reach out **Daniel Jurek**.";
        public const string Message_FurtherHelp_ApiView = " If you need further assistance with the ApiView errors, please reach out **Dozie Ononiwu**.";
        public const string Message_FurtherHelp_Arm_Schemas = " If you need further assistance with the azure-resource-manager-schemas errors, please reach out **Ben Broderick Phillips**.";
        public const string Message_FurtherHelp_Powershell = " The Azure-Powershell CI check is optional, and if it fails, it can be ignored to unblock further progress.";

        public const string Message_FurtherHelp_GoSdk = " If you need further assistance with the azure-sdk-for-go check error, please reach out **Ray Chen**.";
        public const string Message_FurtherHelp_JavaSdk = " If you need further assistance with the azure-sdk-for-java check error, please reach out **Weidong Xu**.";
        public const string Message_FurtherHelp_DotnetSdk = " If you need further assistance with the azure-sdk-for-dotnet check error, please reach out **Wei Hu**.";
        public const string Message_FurtherHelp_PythonSdk = " If you need further assistance with the azure-sdk-for-python check error, please reach out **Yuchao Yan**.";
        public const string Message_FurtherHelp_JsSdk = " If you need further assistance with the azure-sdk-for-js check error, please reach out **Qiaoqiao Zhang**.";

        public const string Message_Error_Null_PR = "The pull request information cannot be retrieved, which suggests that the PR may be invalid.";
        public const string Message_Error_NonGithub_PR = "It looks like the URL you provided is for https://dev.azure.com, which is not supported by this assistant. Please enter a valid GitHub pull request URL instead.";
        public const string Message_Error_ExceedTokenLimit = "The input provided exceeds the maximum token limit. Please create a new POST request to submit your question.";

        public const string Label_APIBreakingChange = "BreakingChangeReviewRequired";
        public const string Label_APINewApiVersionRequired = "NewAPIVersionRequired";
        public const string Label_APIBreakingChangeApproval = "Approved-BreakingChange";

        public const string Label_SDKBreakingChange_Go = "CI-BreakingChange-Go";
        public const string Label_SDKBreakingChange_Python = "CI-BreakingChange-Python";
        public const string Label_SDKBreakingChange_PythonTrack2 = "CI-BreakingChange-Python-Track2";
        public const string Label_SDKBreakingChange_JavaScript = "CI-BreakingChange-JavaScript";

        public const string Label_SDKBreakingChange_Go_Approval = "Approved-SdkBreakingChange-Go";
        public const string Label_SDKBreakingChange_Python_Approval = "Approved-SdkBreakingChange-Python";
        public const string Label_SDKBreakingChange_JavaScript_Approval = "Approved-SdkBreakingChange-JavaScript";

        public const string Label_MergeRequested = "MergeRequested";
        public const string Label_ResourceManager = "resource-manager";
        public const string Label_DataPlane = "data-plane";


        public const string CheckName_MergeRequirement = "Automated merging requirements met";
    }
}
