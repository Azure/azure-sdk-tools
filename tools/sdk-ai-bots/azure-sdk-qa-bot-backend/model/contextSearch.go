package model

type ContextSearchReq struct {
	TenantID        TenantID         `json:"tenant_id" jsonschema:"required,description=The tenant ID of the agent"`
	TopK            *int             `json:"top_k" jsonschema:"description=omitempty,The number of top K documents to search for the answer. Default is 10"`
	Sources         []Source         `json:"sources" jsonschema:"description=omitempty,The sources to search for the answer. Default is all"`
	Message         Message          `json:"message" jsonschema:"required,description=The message to send to the agent"`
	History         []Message        `json:"history" jsonschema:"description=omitempty,The history of messages exchanged with the agent"`
	AdditionalInfos []AdditionalInfo `json:"additional_infos,omitempty" jsonschema:"omitempty,description=Additional information to provide to the agent, such as links or images"`
}

type ContextSearchResp struct {
	HasResult  bool        `json:"has_result" jsonschema:"required,description=Whether the agent has a result"` // TODO resultType
	Knowledges []Knowledge `json:"knowledges" jsonschema:"omitempty,description=The documents for the query"`
}
