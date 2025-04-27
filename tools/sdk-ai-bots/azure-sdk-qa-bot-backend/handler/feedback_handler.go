package handler

import (
	"net/http"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/feedback"
	"github.com/gin-gonic/gin"
)

func FeedBackHandler(c *gin.Context) {
	var req model.FeedbackReq
	if err := c.ShouldBindJSON(&req); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": err.Error()})
		return
	}

	service := feedback.NewFeedbackService()
	if err := service.SaveFeedback(req); err != nil {
		c.JSON(http.StatusInternalServerError, gin.H{"error": err.Error()})
		return
	}

	c.JSON(http.StatusOK, &model.FeedbackResp{})
}
