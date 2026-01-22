package test

import (
	"context"
	"testing"

	"github.com/Azure/azure-sdk-for-go/sdk/azcore/to"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/search"
	"github.com/stretchr/testify/require"
)

func TestQueryIndex(t *testing.T) {
	config.InitConfiguration()
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
	require.NoError(t, err)
	require.NotNil(t, resp)
	require.Greater(t, len(resp.Value), 0, "Expected at least one search result")
}

func TestGetFullContext(t *testing.T) {
	config.InitConfiguration()
	config.InitSecrets()
	searchClient := search.NewSearchClient()
	resp, err := searchClient.GetCompleteContext(model.Index{
		Title:     "getstarted#installation.md",
		ContextID: "typespec_azure_docs",
	})
	require.NoError(t, err)
	require.NotNil(t, resp)
	require.Greater(t, len(resp), 0, "Expected non-empty context")
}

func TestAgenticSearch(t *testing.T) {
	config.InitConfiguration()
	config.InitSecrets()
	searchClient := search.NewSearchClient()
	sourceFilter := map[model.Source]string{}
	resp, err := searchClient.AgenticSearch(context.Background(), "how can i install typespec?", nil, sourceFilter, "", to.Ptr(model.Scope_Unbranded), to.Ptr(model.Plane_Unknown))
	require.NoError(t, err)
	require.NotNil(t, resp)
	require.Greater(t, len(resp.References), 0, "Expected at least one search result")
}
