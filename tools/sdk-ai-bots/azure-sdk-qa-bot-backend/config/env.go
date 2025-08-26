package config

import (
	"fmt"
	"log"
	"os"
	"strconv"

	"github.com/joho/godotenv"
)

type BotENV struct {
	BOT_CLIENT_ID string
	BOT_TENANT_ID string
}

const (
	bot_client_id = "BOT_CLIENT_ID"
	bot_tenant_id = "BOT_TENANT_ID"
)

// Configuration variables initialized from environment
var (
	AOAI_CHAT_REASONING_MODEL      string
	AOAI_CHAT_COMPLETIONS_MODEL    string
	AOAI_CHAT_COMPLETIONS_TOP_P    float32
	AOAI_CHAT_MAX_TOKENS           int
	AOAI_CHAT_CONTEXT_MAX_TOKENS   int
	AOAI_CHAT_COMPLETIONS_ENDPOINT string

	AI_SEARCH_BASE_URL string
	AI_SEARCH_INDEX    string
	AI_SEARCH_AGENT    string

	STORAGE_BASE_URL            string
	STORAGE_KNOWLEDGE_CONTAINER string
	STORAGE_FEEDBACK_CONTAINER  string
	STORAGE_RECORDS_CONTAINER   string

	KEYVAULT_ENDPOINT string
)

var BotEnv *BotENV

// LoadEnvFile loads environment variables from .env if it exists
func LoadEnvFile() {
	if err := godotenv.Load(".env"); err != nil {
		log.Printf("No .env file found or error loading it: %v", err)
	} else {
		log.Printf("Loaded environment variables from .env")
	}
}

func InitBotEnv() error {
	botClientID := os.Getenv(bot_client_id)
	if botClientID == "" {
		return fmt.Errorf("%s environment variable required", bot_client_id)
	}
	botTenantID := os.Getenv(bot_tenant_id)
	if botTenantID == "" {
		return fmt.Errorf("%s environment variable required", bot_tenant_id)
	}
	BotEnv = &BotENV{
		BOT_CLIENT_ID: botClientID,
		BOT_TENANT_ID: botTenantID,
	}
	return nil
}

func GetBotClientID() string {
	if BotEnv == nil || len(BotEnv.BOT_CLIENT_ID) == 0 {
		if err := InitBotEnv(); err != nil {
			log.Println(err)
			return ""
		}
	}
	return BotEnv.BOT_CLIENT_ID
}

func GetBotTenantID() string {
	if BotEnv == nil || len(BotEnv.BOT_TENANT_ID) == 0 {
		if err := InitBotEnv(); err != nil {
			log.Println(err)
			return ""
		}
	}
	return BotEnv.BOT_TENANT_ID
}

// InitEnvironment initializes configuration from environment variables
func InitEnvironment() error {
	// Helper function to get required string env var
	getRequiredStringEnv := func(key string) (string, error) {
		value := os.Getenv(key)
		if value == "" {
			return "", fmt.Errorf("required environment variable %s is not set", key)
		}
		return value, nil
	}

	// Helper function to get required float32 env var
	getRequiredFloat32Env := func(key string) (float32, error) {
		value := os.Getenv(key)
		if value == "" {
			return 0, fmt.Errorf("required environment variable %s is not set", key)
		}
		parsed, err := strconv.ParseFloat(value, 32)
		if err != nil {
			return 0, fmt.Errorf("invalid float value for %s: %s", key, value)
		}
		return float32(parsed), nil
	}

	// Helper function to get required int env var
	getRequiredIntEnv := func(key string) (int, error) {
		value := os.Getenv(key)
		if value == "" {
			return 0, fmt.Errorf("required environment variable %s is not set", key)
		}
		parsed, err := strconv.Atoi(value)
		if err != nil {
			return 0, fmt.Errorf("invalid integer value for %s: %s", key, value)
		}
		return parsed, nil
	}

	var err error
	if AOAI_CHAT_REASONING_MODEL, err = getRequiredStringEnv("AOAI_CHAT_REASONING_MODEL"); err != nil {
		return err
	}
	if AOAI_CHAT_COMPLETIONS_MODEL, err = getRequiredStringEnv("AOAI_CHAT_COMPLETIONS_MODEL"); err != nil {
		return err
	}
	if AOAI_CHAT_COMPLETIONS_TOP_P, err = getRequiredFloat32Env("AOAI_CHAT_COMPLETIONS_TOP_P"); err != nil {
		return err
	}
	if AOAI_CHAT_MAX_TOKENS, err = getRequiredIntEnv("AOAI_CHAT_MAX_TOKENS"); err != nil {
		return err
	}
	if AOAI_CHAT_CONTEXT_MAX_TOKENS, err = getRequiredIntEnv("AOAI_CHAT_CONTEXT_MAX_TOKENS"); err != nil {
		return err
	}
	if AOAI_CHAT_COMPLETIONS_ENDPOINT, err = getRequiredStringEnv("AOAI_CHAT_COMPLETIONS_ENDPOINT"); err != nil {
		return err
	}
	if AI_SEARCH_BASE_URL, err = getRequiredStringEnv("AI_SEARCH_BASE_URL"); err != nil {
		return err
	}
	if AI_SEARCH_INDEX, err = getRequiredStringEnv("AI_SEARCH_INDEX"); err != nil {
		return err
	}
	if AI_SEARCH_AGENT, err = getRequiredStringEnv("AI_SEARCH_AGENT"); err != nil {
		return err
	}
	if STORAGE_BASE_URL, err = getRequiredStringEnv("STORAGE_BASE_URL"); err != nil {
		return err
	}
	if STORAGE_KNOWLEDGE_CONTAINER, err = getRequiredStringEnv("STORAGE_KNOWLEDGE_CONTAINER"); err != nil {
		return err
	}
	if STORAGE_FEEDBACK_CONTAINER, err = getRequiredStringEnv("STORAGE_FEEDBACK_CONTAINER"); err != nil {
		return err
	}
	if STORAGE_RECORDS_CONTAINER, err = getRequiredStringEnv("STORAGE_RECORDS_CONTAINER"); err != nil {
		return err
	}
	if KEYVAULT_ENDPOINT, err = getRequiredStringEnv("KEYVAULT_ENDPOINT"); err != nil {
		return err
	}

	return nil
}
