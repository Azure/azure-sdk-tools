package main

import (
	"net/http"

	"github.com/copilot-extensions/rag-extension/handler"
	"github.com/copilot-extensions/rag-extension/service/auth"
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
	r := setupRouter()
	r.Run(":8088")
}
