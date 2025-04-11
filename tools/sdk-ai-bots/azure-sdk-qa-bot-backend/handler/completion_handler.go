package handler

import (
	"github.com/copilot-extensions/rag-extension/model"
	"github.com/copilot-extensions/rag-extension/service/agent"
	"github.com/gin-gonic/gin"
)

func CompletionHandler(c *gin.Context) {
	var req model.CompletionReq
	if err := c.BindJSON(&req); err != nil {
		c.JSON(400, gin.H{
			"error": "Failed to bind JSON request: " + err.Error(),
		})
		return
	}

	service, err := agent.NewCompletionService()
	if err != nil {
		c.JSON(500, gin.H{"error": err.Error()})
		return
	}
	resp, err := service.ChatCompletion(&req)
	if err != nil {
		c.JSON(500, gin.H{"error": err.Error()})
		return
	}
	c.JSON(200, &resp)
}
