package model

type TenantID string

const (
	TenantID_AzureSDKQaBot      TenantID = "azure_sdk_qa_bot" // default as TypeSpec QA bot
	TenantID_TypeSpecExtension  TenantID = "typespec_extension"
	TenantID_PythonChannelQaBot TenantID = "python_channel_qa_bot"
)

type Source string

const (
	Source_TypeSpec              Source = "typespec_docs"
	Source_TypeSpecAzure         Source = "typespec_azure_docs"
	Source_AzureRestAPISpec      Source = "azure_rest_api_specs_wiki"
	Source_AzureSDKForPython     Source = "azure_sdk_for_python_docs"
	Source_AzureSDKForPythonWiki Source = "azure_sdk_for_python_wiki"
	Source_TypeSpecQA            Source = "static_typespec_qa"
)

type Role string

const (
	Role_User      Role = "user"
	Role_Assistant Role = "assistant"
	Role_System    Role = "system"
)

type Message struct {
	Role    Role   `json:"role" jsonschema:"required,description=The role of the message sender"`
	Content string `json:"content" jsonschema:"required,description=The content of the message"`
}

type Reference struct {
	Title   string `json:"title" jsonschema:"required,description=The title of the document"`
	Source  string `json:"source" jsonschema:"required,description=The source of the document"`
	Link    string `json:"link" jsonschema:"required,description=The link to the document"`
	Content string `json:"content" jsonschema:"required,description=The content of the document"`
}

type CompletionReq struct {
	TenantID                TenantID  `json:"tenant_id" jsonschema:"required,description=The tenant ID of the agent"`
	PromptTemplate          *string   `json:"prompt_template" jsonschema:"omitempty,description=The prompt template to use for the agent"`
	PromptTemplateArguments *string   `json:"prompt_template_arguments" jsonschema:"omitempty,description=The arguments to use for the prompt template"`
	TopK                    *int      `json:"top_k" jsonschema:"description=omitempty,The number of top K documents to search for the answer. Default is 10"`
	Sources                 []Source  `json:"sources" jsonschema:"description=omitempty,The sources to search for the answer. Default is all"`
	Message                 Message   `json:"message" jsonschema:"required,description=The message to send to the agent"`
	History                 []Message `json:"history" jsonschema:"description=omitempty,The history of messages exchanged with the agent"`
	WithFullContext         *bool     `json:"with_full_context" jsonschema:"description=omitempty,Whether to use the full context for the agent. Default is false"`
}

type CompletionResp struct {
	Answer      string           `json:"answer" jsonschema:"required,description=The answer from the agent"`
	HasResult   bool             `json:"has_result" jsonschema:"required,description=Whether the agent has a result"` // TODO resultType
	References  []Reference      `json:"references" jsonschema:"omitempty,description=The references to the documents used to generate the answer"`
	FullContext *string          `json:"full_context" jsonschema:"omitempty,description=The full context used to generate the answer"`
	Intension   *IntensionResult `json:"intension" jsonschema:"omitempty,description=The intension of the question"`
}

type QuestionCategory string

const (
	QuestionCategory_Unknown   QuestionCategory = "unknown"
	QuestionCategory_Branded   QuestionCategory = "branded"
	QuestionCategory_Unbranded QuestionCategory = "unbranded"
)

type IntensionResult struct {
	Question string           `json:"question" jsonschema:"required,description=The question to ask the agent"`
	Category QuestionCategory `json:"category" jsonschema:"required,description=The category of the question"`
}
