package test

import (
	"context"
	"encoding/json"
	"testing"

	"github.com/Azure/azure-sdk-for-go/sdk/azcore/to"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/search"
)

func TestQueryIndex(t *testing.T) {
	config.InitEnvironment()
	config.InitSecrets()
	searchClient := search.NewSearchClient()
	req := &model.QueryIndexRequest{
		Search: "how can i install typespec?",
		Count:  true,
		Top:    5,
		VectorQueries: []model.VectorQuery{
			{
				Text:       "how can i install typespec",
				K:          to.Ptr(5),
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
	t.Logf("QueryIndex response: %+v", resp)
}

func TestGetFullContext(t *testing.T) {
	config.InitEnvironment()
	config.InitSecrets()
	searchClient := search.NewSearchClient()
	resp, err := searchClient.GetCompleteContext(model.Index{
		Title: "getstarted#installation.md",
	})
	if err != nil {
		t.Errorf("QueryIndex() got an error: %v", err)
	}
	v, _ := json.Marshal(resp)
	t.Logf("GetFullContext response: %s", v)
}

func TestAgenticSearch(t *testing.T) {
	config.InitEnvironment()
	config.InitSecrets()
	searchClient := search.NewSearchClient()
	resp, err := searchClient.AgenticSearch(context.Background(), "how can i install typespec?", nil, "")
	if err != nil {
		t.Errorf("AgenticSearch() got an error: %v", err)
	}
	t.Logf("AgenticSearch response: %+v", resp)
}
