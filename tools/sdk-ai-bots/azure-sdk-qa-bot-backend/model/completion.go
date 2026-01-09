package model

type TenantID string

const (
	TenantID_AzureSDKQaBot          TenantID = "azure_sdk_qa_bot" // default as TypeSpec QA bot
	TenantID_TypeSpecExtension      TenantID = "typespec_extension"
	TenantID_PythonChannelQaBot     TenantID = "python_channel_qa_bot"
	TenantID_DotnetChannelQaBot     TenantID = "dotnet_channel_qa_bot"
	TenantID_AzureSDKOnboarding     TenantID = "azure_sdk_onboarding"
	TenantID_GolangChannelQaBot     TenantID = "golang_channel_qa_bot"
	TenantID_JavaChannelQaBot       TenantID = "java_channel_qa_bot"
	TenantID_JavaScriptChannelQaBot TenantID = "javascript_channel_qa_bot"
	TenantID_GeneralQaBot           TenantID = "general_qa_bot"
	TenantID_APISpecReviewBot       TenantID = "api_spec_review_bot"
)

type Source string

const (
	Source_TypeSpec                       Source = "typespec_docs"
	Source_TypeSpecAzure                  Source = "typespec_azure_docs"
	Source_AzureRestAPISpec               Source = "azure_rest_api_specs_wiki"
	Source_AzureSDKForPython              Source = "azure_sdk_for_python_docs"
	Source_AzureSDKForPythonWiki          Source = "azure_sdk_for_python_wiki"
	Source_TypeSpecQA                     Source = "static_typespec_qa"
	Source_AzureAPIGuidelines             Source = "azure_api_guidelines"
	Source_AzureResourceManagerRPC        Source = "azure_resource_manager_rpc"
	Source_TypeSpecMigration              Source = "static_typespec_migration_docs"
	Source_AzureSDKDocsEng                Source = "azure-sdk-docs-eng"
	Source_AzureSDKGuidelines             Source = "azure-sdk-guidelines"
	Source_TypeSpecAzureHttpSpecs         Source = "typespec_azure_http_specs"
	Source_TypeSpecHttpSpecs              Source = "typespec_http_specs"
	Source_AzureSDKForGo                  Source = "azure_sdk_for_go_docs"
	Source_StaticAzureDocs                Source = "static_azure_docs"
	Source_StaticTypeSpecToSwaggerMapping Source = "static_typespec_to_swagger_mapping"
	Source_AzureSDKForJava                Source = "azure_sdk_for_java_docs"
	Source_AzureSDKForJavaWiki            Source = "azure_sdk_for_java_wiki"
	Source_AutorestJava                   Source = "autorest_java_docs"
	Source_AzureSDKForJavaScript          Source = "azure_sdk_for_js_docs"
	Source_AzureSDKForJavaScriptWiki      Source = "azure_sdk_for_js_wiki"
	Source_AzureSDKForNetDocs             Source = "azure_sdk_for_net_docs"
)

type Role string

const (
	Role_User      Role = "user"
	Role_Assistant Role = "assistant"
	Role_System    Role = "system"
)

type Message struct {
	Role       Role    `json:"role" jsonschema:"required,description=The role of the message sender"`
	Content    string  `json:"content" jsonschema:"required,description=The content of the message"`
	RawContent *string `json:"raw_content,omitempty" jsonschema:"omitempty,description=The raw content of the message, used for searching"`
	Name       *string `json:"name,omitempty" jsonschema:"omitempty,description=The name of the message sender, used for system messages"`
}

type Reference struct {
	Title   string `json:"title" jsonschema:"required,description=The title of the document"`
	Source  string `json:"source" jsonschema:"required,description=The source of the document"`
	Link    string `json:"link" jsonschema:"required,description=The link to the document"`
	Content string `json:"content" jsonschema:"required,description=The content of the document"`
}

type AdditionalInfoType string

const (
	AdditionalInfoType_Link  AdditionalInfoType = "link"
	AdditionalInfoType_Image AdditionalInfoType = "image"
)

type AdditionalInfo struct {
	Type    AdditionalInfoType `json:"type" jsonschema:"required,description=The type of the additional information"`
	Content string             `json:"content" jsonschema:"required,description=The content of the additional information"`
	Link    string             `json:"link" jsonschema:"required,description=The link to the additional information, required if type is link"`
}

type CompletionReq struct {
	TenantID        TenantID         `json:"tenant_id" jsonschema:"required,description=The tenant ID of the agent"`
	TopK            *int             `json:"top_k" jsonschema:"description=omitempty,The number of top K documents to search for the answer. Default is 10"`
	Sources         []Source         `json:"sources" jsonschema:"description=omitempty,The sources to search for the answer. Default is all"`
	Message         Message          `json:"message" jsonschema:"required,description=The message to send to the agent"`
	History         []Message        `json:"history" jsonschema:"description=omitempty,The history of messages exchanged with the agent"`
	WithFullContext *bool            `json:"with_full_context" jsonschema:"description=omitempty,Whether to use the full context for the agent. Default is false"`
	WithPreprocess  *bool            `json:"with_preprocess" jsonschema:"description=omitempty,Whether to preprocess the message before sending it to the agent. Default is false"`
	AdditionalInfos []AdditionalInfo `json:"additional_infos,omitempty" jsonschema:"omitempty,description=Additional information to provide to the agent, such as links or images"`
}

type CompletionResp struct {
	ID                string           `json:"id" jsonschema:"required,description=The unique ID of the completion"`
	Answer            string           `json:"answer" jsonschema:"required,description=The answer from the agent"`
	HasResult         bool             `json:"has_result" jsonschema:"required,description=Whether the agent has a result"` // TODO resultType
	References        []Reference      `json:"references" jsonschema:"omitempty,description=The references to the documents used to generate the answer"`
	FullContext       *string          `json:"full_context" jsonschema:"omitempty,description=The full context used to generate the answer"`
	Intention         *IntentionResult `json:"intention" jsonschema:"omitempty,description=The intention of the question"`
	ReasoningProgress *string          `json:"reasoning_progress,omitempty" jsonschema:"omitempty,description=The reasoning progress of generating the answer"`
	RouteTenant       *TenantID        `json:"route_tenant,omitempty" jsonschema:"omitempty,description=The tenant ID the question is routed to"`
}

type QuestionScope string

const (
	QuestionScope_Unknown   QuestionScope = "unknown"
	QuestionScope_Branded   QuestionScope = "branded"
	QuestionScope_Unbranded QuestionScope = "unbranded"
)

type IntentionResult struct {
	Question           string        `json:"question" jsonschema:"required,description=The question to ask the agent"`
	Category           string        `json:"category" jsonschema:"required,description=The category of the question"`
	SpecType           string        `json:"spec_type,omitempty" jsonschema:"omitempty,description=The type of the spec, such as typespec, azure rest api, etc."`
	Scope              QuestionScope `json:"scope,omitempty" jsonschema:"omitempty,description=The scope of the question"`
	NeedsRagProcessing bool          `json:"needs_rag_processing" jsonschema:"required,description=Whether to invoke RAG workflow"`
}

type TenantRoutingResult struct {
	RouteTenant TenantID `json:"route_tenant" jsonschema:"required,description=The tenant ID to route the question to"`
}
