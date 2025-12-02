package main

import (
	"net/http"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/handler"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/utils"
	"github.com/gin-gonic/gin"
)

func setupRouter() *gin.Engine {
	r := gin.Default()

	// Ping test
	r.GET("/ping", func(c *gin.Context) {
		c.String(http.StatusOK, "pong")
	})

	// Protected endpoints
	r.POST("/completion", handler.CompletionHandler)
	r.POST("/feedback", handler.FeedBackHandler)
	r.POST("/record_answer", handler.AnswerRecordHandler)
	r.POST("/code_review", handler.CodeReviewHandler)

	return r
}

func main() {
	// Load environment variables from .env files first
	config.LoadEnvFile()

	// init resources
	config.InitConfiguration()
	config.InitSecrets()
	config.InitOpenAIClient()

	// Configure logging for Azure App Service to prevent log splitting
	utils.ConfigureAzureCompatibleLogging()

	r := setupRouter()
	err := r.Run(":8088")
	if err != nil {
		panic(err)
	}
}
