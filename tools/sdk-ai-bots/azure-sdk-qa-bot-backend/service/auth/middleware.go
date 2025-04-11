package auth

import (
	"log"
	"net/http"
	"os"

	"github.com/gin-gonic/gin"
	"github.com/joho/godotenv"
)

// APIKeyAuth middleware checks for a valid API key in the X-API-Key header
func APIKeyAuth() gin.HandlerFunc {
	return func(c *gin.Context) {
		// Get API key from environment variable
		err := godotenv.Load()
		if err != nil {
			log.Fatal(err)
		}
		expectedAPIKey := os.Getenv("API_KEY")
		if expectedAPIKey == "" {
			// If no API key is set in environment, consider the API open
			c.Next()
			return
		}

		// Get API key from request header
		apiKey := c.GetHeader("X-API-Key")
		if apiKey == "" {
			c.AbortWithStatusJSON(http.StatusUnauthorized, gin.H{
				"error": "API key is required",
			})
			return
		}

		// Validate API key
		if apiKey != expectedAPIKey {
			c.AbortWithStatusJSON(http.StatusUnauthorized, gin.H{
				"error": "Invalid API key",
			})
			return
		}

		c.Next()
	}
}
