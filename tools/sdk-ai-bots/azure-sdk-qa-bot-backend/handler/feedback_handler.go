package handler

import (
	"net/http"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/feedback"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/github"
	"github.com/gin-gonic/gin"
)

func FeedBackHandler(c *gin.Context) {
	var req model.FeedbackReq
	if err := c.ShouldBindJSON(&req); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": err.Error()})
		return
	}

	// Save feedback to Excel
	feedbackService := feedback.NewFeedbackService()
	if err := feedbackService.SaveFeedback(req); err != nil {
		c.JSON(http.StatusInternalServerError, gin.H{"error": err.Error()})
		return
	}

	// Create GitHub sub-issue
	go func() {
		githubService := github.NewGitHubService()
		if err := githubService.CreateSubIssueFromFeedbackRequest(req); err != nil {
			c.JSON(http.StatusInternalServerError, gin.H{"error": err.Error()})
			return
		}
	}()

	c.JSON(http.StatusOK, &model.FeedbackResp{})
}
