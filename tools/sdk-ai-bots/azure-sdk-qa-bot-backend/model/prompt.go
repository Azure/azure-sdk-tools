package model

type DefaultPromptResponse struct {
	Answer    string `json:"answer"`
	HasResult bool   `json:"has_result"`
}
