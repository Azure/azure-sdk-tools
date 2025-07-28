package model

import (
	"fmt"
	"net/http"
)

// ErrorCode represents specific error codes for the service
type ErrorCode string

const (
	// Client Error Codes (4xx)
	ErrorCodeInvalidRequest  ErrorCode = "INVALID_REQUEST"
	ErrorCodeMissingMessage  ErrorCode = "MISSING_MESSAGE"
	ErrorCodeEmptyContent    ErrorCode = "EMPTY_CONTENT"
	ErrorCodeInvalidTenantID ErrorCode = "INVALID_TENANT_ID"
	ErrorCodeUnauthorized    ErrorCode = "UNAUTHORIZED"

	// Server Error Codes (5xx)
	ErrorCodeServiceInitFailure  ErrorCode = "SERVICE_INIT_FAILURE"
	ErrorCodeLLMServiceFailure   ErrorCode = "LLM_SERVICE_FAILURE"
	ErrorCodeLLMRateLimitFailure ErrorCode = "LLM_RATE_LIMIT_FAILURE"
	ErrorCodeSearchFailure       ErrorCode = "SEARCH_FAILURE"
	ErrorCodeInternalError       ErrorCode = "INTERNAL_ERROR"
)

// ErrorCategory represents the category of error for monitoring and alerting
type ErrorCategory string

const (
	ErrorCategoryValidation     ErrorCategory = "validation"
	ErrorCategoryAuthentication ErrorCategory = "authentication"
	ErrorCategoryAuthorization  ErrorCategory = "authorization"
	ErrorCategoryRateLimit      ErrorCategory = "rate limit"
	ErrorCategoryService        ErrorCategory = "service"
	ErrorCategoryDependency     ErrorCategory = "dependency"
	ErrorCategoryInternal       ErrorCategory = "internal"
)

// APIError represents a structured error for the service
type APIError struct {
	Code       ErrorCode     `json:"code"`
	Message    string        `json:"message"`
	Category   ErrorCategory `json:"category"`
	StatusCode int           `json:"-"` // HTTP status code, not included in JSON response
	Details    interface{}   `json:"details,omitempty"`
	RequestID  string        `json:"request_id,omitempty"`
	Timestamp  string        `json:"timestamp,omitempty"`
}

// Error implements the error interface
func (e *APIError) Error() string {
	return fmt.Sprintf("[%s] %s", e.Code, e.Message)
}

// NewAPIError creates a new API error
func NewAPIError(code ErrorCode, message string, details interface{}) *APIError {
	category, statusCode := getErrorMetadata(code)
	return &APIError{
		Code:       code,
		Message:    message,
		Category:   category,
		StatusCode: statusCode,
		Details:    details,
	}
}

// getErrorMetadata returns category and HTTP status code for an error code
func getErrorMetadata(code ErrorCode) (ErrorCategory, int) {
	switch code {
	// Client errors (4xx)
	case ErrorCodeInvalidRequest, ErrorCodeMissingMessage, ErrorCodeEmptyContent:
		return ErrorCategoryValidation, http.StatusBadRequest
	case ErrorCodeInvalidTenantID:
		return ErrorCategoryValidation, http.StatusBadRequest
	case ErrorCodeUnauthorized:
		return ErrorCategoryAuthentication, http.StatusUnauthorized
	// Server errors (5xx)
	case ErrorCodeServiceInitFailure:
		return ErrorCategoryService, http.StatusInternalServerError
	case ErrorCodeLLMServiceFailure:
		return ErrorCategoryDependency, http.StatusInternalServerError
	case ErrorCodeSearchFailure:
		return ErrorCategoryDependency, http.StatusInternalServerError
	case ErrorCodeLLMRateLimitFailure:
		return ErrorCategoryRateLimit, http.StatusTooManyRequests
	default:
		return ErrorCategoryInternal, http.StatusInternalServerError
	}
}

// Predefined error constructors for common errors
func NewInvalidRequestError(message string, details interface{}) *APIError {
	return NewAPIError(ErrorCodeInvalidRequest, message, details)
}

func NewMissingMessageError() *APIError {
	return NewAPIError(ErrorCodeMissingMessage, "Message is required", nil)
}

func NewEmptyContentError() *APIError {
	return NewAPIError(ErrorCodeEmptyContent, "Message content cannot be empty", nil)
}

func NewInvalidTenantIDError(tenantID string) *APIError {
	return NewAPIError(ErrorCodeInvalidTenantID, fmt.Sprintf("Invalid tenant ID: %s", tenantID), nil)
}

func NewServiceInitFailureError(err error) *APIError {
	return NewAPIError(ErrorCodeServiceInitFailure, "Failed to initialize service", err.Error())
}

func NewLLMServiceFailureError(err error) *APIError {
	return NewAPIError(ErrorCodeLLMServiceFailure, "LLM service request failed (retryable)", err.Error())
}

func NewLLMRateLimitFailureError(err error) *APIError {
	return NewAPIError(ErrorCodeLLMRateLimitFailure, "LLM service hit rate limit (retryable)", err.Error())
}

func NewSearchFailureError(err error) *APIError {
	return NewAPIError(ErrorCodeSearchFailure, "Search service request failed (retryable)", err.Error())
}

// Legacy Error for backward compatibility
type Error struct {
	Code    string `json:"code"`
	Message string `json:"message"`
}
