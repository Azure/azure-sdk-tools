package handler

import (
	"net/http"

	"github.com/copilot-extensions/rag-extension/model"
	"github.com/copilot-extensions/rag-extension/service/feedback"
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
