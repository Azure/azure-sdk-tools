package handler

import (
	"encoding/json"
	"log"

	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/model"
	"github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/service/agent"
	"github.com/gin-gonic/gin"
)

func CompletionHandler(c *gin.Context) {
	var req model.CompletionReq
	if err := c.BindJSON(&req); err != nil {
		// Use structured error instead of generic error
		apiErr := model.NewInvalidRequestError("Failed to bind JSON request", err.Error())
		c.JSON(apiErr.StatusCode, apiErr)
		return
	}

	service, err := agent.NewCompletionService()
	if err != nil {
		// Use structured error for service initialization failure
		apiErr := model.NewServiceInitFailureError(err)
		c.JSON(apiErr.StatusCode, apiErr)
		return
	}

	resp, err := service.ChatCompletion(c.Request.Context(), &req)
	if err != nil {
		// Check if it's already an APIError, otherwise wrap it
		if apiErr, ok := err.(*model.APIError); ok {
			c.JSON(apiErr.StatusCode, apiErr)
		} else {
			// Wrap unknown errors as internal errors
			apiErr := model.NewAPIError(
				model.ErrorCodeInternalError,
				"An unexpected error occurred",
				err.Error(),
			)
			c.JSON(apiErr.StatusCode, apiErr)
		}
		return
	}

	// Keep your original response logging
	jsonResp, err := json.Marshal(resp)
	if err != nil {
		log.Printf("Failed to marshal response: %v\n", err)
	} else {
		log.Printf("Response: %s\n", jsonResp)
	}
	c.JSON(200, &resp)
}
