package search

import (
	"context"
	"encoding/json"
	"testing"

	"github.com/copilot-extensions/rag-extension/model"
	"github.com/copilot-extensions/rag-extension/service/search"
)

func TestQueryIndex(t *testing.T) {
	searchClient := search.NewSearchClient()
	req := &model.QueryIndexRequest{
		Search: "how can i install typespec?",
		Count:  true,
		Top:    5,
		VectorQueries: []model.VectorQuery{
			{
				Text:       "how can i install typespec",
				K:          5,
				Fields:     "text_vector",
				Kind:       "text",
				Exhaustive: true,
			},
		},
	}
	resp, err := searchClient.QueryIndex(context.Background(), req)
	if err != nil {
		t.Errorf("QueryIndex() got an error: %v", err)
	}
	print(resp)
}

func TestGetFullContext(t *testing.T) {
	searchClient := search.NewSearchClient()
	resp, err := searchClient.GetCompleteContext(model.Index{
		Title: "introduction_installation.mdx",
	})
	if err != nil {
		t.Errorf("QueryIndex() got an error: %v", err)
	}
	v, _ := json.Marshal(resp)
	print(string(v))
}
