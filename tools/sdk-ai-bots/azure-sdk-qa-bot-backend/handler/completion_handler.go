package handler

import (
	"encoding/json"
	"log"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/agent"
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
	resp, err := service.ChatCompletion(c.Request.Context(), &req)
	if err != nil {
		c.JSON(500, gin.H{"error": err.Error()})
		return
	}
	jsonResp, err := json.Marshal(resp)
	if err != nil {
		log.Printf("Failed to marshal response: %v\n", err)
	} else {
		log.Printf("Response: %s\n", jsonResp)
	}
	c.JSON(200, &resp)
}
