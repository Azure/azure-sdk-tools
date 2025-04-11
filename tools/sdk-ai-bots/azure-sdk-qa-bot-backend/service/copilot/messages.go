package copilot

type ChatRequest struct {
	Messages []ChatMessage `json:"messages"`
}

type ChatMessage struct {
	Role    string `json:"role"`
	Content string `json:"content"`
}

type Model string

const (
	ModelGPT35      Model = "gpt-3.5-turbo"
	ModelGPT4       Model = "gpt-4"
	ModelGPT4o      Model = "gpt-4o"
	ModelEmbeddings Model = "text-embedding-ada-002"
)

type ChatCompletionsRequest struct {
	Messages            []ChatMessage `json:"messages"`
	Model               Model         `json:"model"`
	Stream              bool          `json:"stream"`
	Temperature         float32       `json:"temperature"`
	TopP                float32       `json:"top_p"`
	MaxCompletionTokens int32         `json:"max_completion_tokens"`
}

type EmbeddingsRequest struct {
	Model Model    `json:"model"`
	Input []string `json:"input"`
}

type EmbeddingsResponse struct {
	Data  []*EmbeddingsResponseData `json:"data"`
	Usage *EmbeddingsResponseUsage  `json:"usage"`
}

type EmbeddingsResponseData struct {
	Embedding []float32 `json:"embedding"`
	Index     int       `json:"index"`
}

type EmbeddingsResponseUsage struct {
	PromptTokens int `json:"prompt_tokens"`
	TotalTokens  int `json:"total_tokens"`
}

type ChatCompletionsResponse struct {
	Choices []ChatChoice `json:"choices"`
}

type ChatChoice struct {
	Index   int         `json:"index"`
	Message ChatMessage `json:"message"`
}
