package model

type Reaction string

const (
	Reaction_Good Reaction = "good"
	Reaction_Bad  Reaction = "bad"
)

type FeedbackReq struct {
	ChannelID string   `json:"channel_id,omitempty" jsonschema:"omitempty,description=Optional channel ID"`
	TenantID string    `json:"tenant_id" jsonschema:"required,description=The tenant ID"`
	Messages []Message `json:"messages" jsonschema:"required,description=The conversation messages"`
	Reaction Reaction  `json:"reaction" jsonschema:"required,description=User's reaction to the conversation"`
	Comment  string    `json:"comment" jsonschema:"omitempty,description=Optional comment from the user about the conversation"`
	Reasons  []string  `json:"reasons,omitempty" jsonschema:"omitempty,description=Optional reasons for the feedback, used to improve the model"`
	Link     string    `json:"link,omitempty" jsonschema:"omitempty,description=Optional link to the conversation for further reference"`
	UserName string    `json:"user_name,omitempty" jsonschema:"omitempty,description=Optional user name"`
	Subject  string    `json:"subject,omitempty" jsonschema:"omitempty,description=Optional subject of the conversation"`
}

type FeedbackResp struct {
}
