package model

type AnswerRecordReq struct {
	ChannelName string `json:"channel_name" jsonschema:"required,description=The name of the channel where the answer was provided"`
	ChannelID   string `json:"channel_id" jsonschema:"required,description=The ID of the channel where the answer was provided"`
	MessageLink string `json:"message_link" jsonschema:"required,description=Link to the message containing the bot's answer"`
}

type AnswerRecordResp struct {
}
