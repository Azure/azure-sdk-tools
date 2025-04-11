package search

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"net/http"
	"os"
	"strings"

	"github.com/copilot-extensions/rag-extension/model"
	"github.com/joho/godotenv"
)

type SearchClient struct {
	BaseUrl string
	ApiKey  string
	Index   string
}

func NewSearchClient() *SearchClient {
	err := godotenv.Load()
	if err != nil {
		log.Fatal(err)
	}
	return &SearchClient{
		BaseUrl: os.Getenv("AI_SEARCH_BASEURL"),
		ApiKey:  os.Getenv("AI_SEARCH_APIKEY"),
		Index:   os.Getenv("AI_SEARCH_INDEX"),
	}
}

func (s *SearchClient) QueryIndex(ctx context.Context, req *model.QueryIndexRequest) (*model.QueryIndexResponse, error) {
	body, err := json.Marshal(req)
	if err != nil {
		return nil, fmt.Errorf("failed to marshal request: %w", err)
	}

	httpReq, err := http.NewRequestWithContext(ctx, http.MethodPost, fmt.Sprintf("%s/indexes/%s/%s", s.BaseUrl, s.Index, "docs/search?api-version=2024-11-01-preview"), bytes.NewReader(body))
	if err != nil {
		return nil, fmt.Errorf("failed to create request: %w", err)
	}
	httpReq.Header.Set("Content-Type", "application/json")
	httpReq.Header.Set("Accept", "application/json")
	httpReq.Header.Set("api-key", s.ApiKey)
	resp, err := (&http.Client{}).Do(httpReq)
	if err != nil {
		return nil, fmt.Errorf("failed to send request: %w", err)
	}

	if resp.StatusCode != http.StatusOK {
		b, _ := io.ReadAll(resp.Body)
		fmt.Println(string(b))
		return nil, fmt.Errorf("unexpected status code: %d", resp.StatusCode)
	}
	b, _ := io.ReadAll(resp.Body)
	httpResp := &model.QueryIndexResponse{}
	if err := json.Unmarshal(b, httpResp); err != nil {
		return nil, fmt.Errorf("failed to unmarshal response: %w", err)
	}

	return httpResp, nil
}

func (s *SearchClient) SearchTopKRelatedDocuments(query string, k int, sources []model.Source) ([]model.Index, error) {
	req := &model.QueryIndexRequest{
		Search: query,
		Count:  false,
		Top:    k,
		Select: "title, context_id",
		VectorQueries: []model.VectorQuery{
			{
				Text:       query,
				K:          k,
				Fields:     "text_vector",
				Kind:       "text",
				Exhaustive: true,
			},
		},
	}
	if len(sources) > 0 {
		filters := make([]string, 0)
		for _, source := range sources {
			filters = append(filters, fmt.Sprintf("context_id eq '%s'", source))
		}
		req.Filter = strings.Join(filters, " or ")
	}
	resp, err := s.QueryIndex(context.Background(), req)
	if err != nil {
		return nil, fmt.Errorf("QueryIndex() got an error: %v", err)
	}
	return resp.Value, nil
}

func (s *SearchClient) GetCompleteContext(chunk model.Index) ([]model.Index, error) {
	req := &model.QueryIndexRequest{
		Count:   false,
		OrderBy: "ordinal_position",
		Select:  "chunk_id, chunk, title, header_1, header_2, header_3, ordinal_position, context_id",
		Filter:  fmt.Sprintf("title eq '%s' and context_id eq '%s'", chunk.Title, chunk.ContextID),
	}
	resp, err := s.QueryIndex(context.Background(), req)
	if err != nil {
		return nil, fmt.Errorf("QueryIndex() got an error: %v", err)
	}
	return resp.Value, nil
}

func (s *SearchClient) CompleteChunk(chunk model.Index) model.Index {
	chunks, err := s.GetCompleteContext(chunk)
	if err != nil {
		return chunk
	}
	if len(chunks) == 0 {
		return chunk
	}
	var contents []string
	totalLength := 0
	for _, chunk := range chunks {
		if totalLength+len(chunk.Chunk) > 10000 {
			break
		}
		totalLength += len(chunk.Chunk)
		contents = append(contents, chunk.Chunk)
	}
	chunk.Chunk = strings.Join(contents, "\n")
	chunk.Title = chunks[0].Title
	chunk.Header1 = chunks[0].Header1
	chunk.Header2 = ""
	chunk.Header3 = ""
	chunk.OrdinalPosition = 0
	return chunk
}
