package model

type DefaultPromptResponse struct {
	Answer     string      `json:"answer"`
	HasResult  bool        `json:"has_result"`
	References []Reference `json:"references"`
	Category   string      `json:"category"`
	Reasoning  string      `json:"reasoning"`
}
