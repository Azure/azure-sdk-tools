package model

type Reaction string

const (
	Reaction_Good Reaction = "good"
	Reaction_Bad  Reaction = "bad"
)

type FeedbackReq struct {
	TenantID string    `json:"tenant_id" jsonschema:"required,description=The tenant ID"`
	Messages []Message `json:"messages" jsonschema:"required,description=The conversation messages"`
	Reaction Reaction  `json:"reaction" jsonschema:"required,description=User's reaction to the conversation"`
}

type FeedbackResp struct {
}
