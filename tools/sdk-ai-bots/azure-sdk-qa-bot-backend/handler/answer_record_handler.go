package handler

import (
	"net/http"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/record"
	"github.com/gin-gonic/gin"
)

func AnswerRecordHandler(c *gin.Context) {
	var req model.AnswerRecordReq
	if err := c.ShouldBindJSON(&req); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": err.Error()})
		return
	}

	service := record.NewRecordService()
	if err := service.SaveAnswerRecord(req); err != nil {
		c.JSON(http.StatusInternalServerError, gin.H{"error": err.Error()})
		return
	}

	c.JSON(http.StatusOK, &model.AnswerRecordResp{})
}
