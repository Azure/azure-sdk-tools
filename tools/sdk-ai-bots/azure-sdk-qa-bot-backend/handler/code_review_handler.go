package handler

import (
	"encoding/json"
	"log"

	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/agent"
	"github.com/gin-gonic/gin"
)

func CodeReviewHandler(c *gin.Context) {
	var req model.CodeReviewReq
	if err := c.BindJSON(&req); err != nil {
		apiErr := model.NewInvalidRequestError("Failed to bind JSON request", err.Error())
		c.JSON(apiErr.StatusCode, apiErr)
		return
	}

	service, err := agent.NewCodeReviewService()
	if err != nil {
		apiErr := model.NewServiceInitFailureError(err)
		c.JSON(apiErr.StatusCode, apiErr)
		return
	}

	resp, err := service.Review(c.Request.Context(), &req)
	if err != nil {
		if apiErr, ok := err.(*model.APIError); ok {
			c.JSON(apiErr.StatusCode, apiErr)
		} else {
			apiErr := model.NewAPIError(
				model.ErrorCodeInternalError,
				"An unexpected error occurred during code review",
				err.Error(),
			)
			c.JSON(apiErr.StatusCode, apiErr)
		}
		return
	}

	jsonResp, err := json.Marshal(resp)
	if err != nil {
		log.Printf("Failed to marshal response: %v\n", err)
	} else {
		log.Printf("Code review response: %s\n", jsonResp)
	}

	c.JSON(200, resp)
}
