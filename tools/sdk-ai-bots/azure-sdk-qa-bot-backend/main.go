package main

import (
	"net/http"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/handler"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/auth"
	"github.com/gin-gonic/gin"
)

func setupRouter() *gin.Engine {
	r := gin.Default()

	// Ping test
	r.GET("/ping", func(c *gin.Context) {
		c.String(http.StatusOK, "pong")
	})

	// Protected endpoints
	r.POST("/completion", auth.APIKeyAuth(), handler.CompletionHandler)
	r.POST("/feedback", auth.APIKeyAuth(), handler.FeedBackHandler)

	return r
}

func main() {
	// init resources
	config.InitOpenAIClient()
	
	r := setupRouter()
	r.Run(":8088")
}
