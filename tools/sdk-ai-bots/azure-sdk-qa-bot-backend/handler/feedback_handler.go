package handler

import (
	"log"
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

	// Save feedback to Excel
	feedbackService := feedback.NewFeedbackService()
	if err := feedbackService.SaveFeedback(req); err != nil {
		c.JSON(http.StatusInternalServerError, gin.H{"error": err.Error()})
		return
	}

	// Create GitHub sub-issue only for negative feedback
	if req.Reaction == model.Reaction_Bad {
		if err := feedbackService.CreateGitHubIssueForNegativeFeedback(req); err != nil {
			// Log error with feedback details for manual recovery if needed
			log.Printf("ERROR: Failed to create GitHub issue for negative feedback from user %s (TenantID: %s, ChannelID: %s): %v",
				req.UserName, req.TenantID, req.ChannelID, err)
			log.Printf("Feedback details - Reasons: %v, Comment: %s, Link: %s",
				req.Reasons, req.Comment, req.Link)
		}
	}

	c.JSON(http.StatusOK, &model.FeedbackResp{})
}
