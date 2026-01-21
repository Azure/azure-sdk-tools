package config

import (
	"context"
	"fmt"
	"log"
	"os"

	"github.com/Azure/AppConfiguration-GoProvider/azureappconfiguration"
	"github.com/Azure/azure-sdk-for-go/sdk/azcore"
	"github.com/Azure/azure-sdk-for-go/sdk/azidentity"
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

type Config struct {
	AOAI_CHAT_REASONING_MODEL             string
	AOAI_CHAT_REASONING_MODEL_TEMPERATURE float32
	AOAI_CHAT_COMPLETIONS_MODEL           string
	AOAI_CHAT_COMPLETIONS_TEMPERATURE     float32
	AOAI_CHAT_MAX_TOKENS                  int
	AOAI_CHAT_CONTEXT_MAX_TOKENS          int
	AOAI_CHAT_COMPLETIONS_ENDPOINT        string

	AI_SEARCH_BASE_URL           string
	AI_SEARCH_INDEX              string
	AI_SEARCH_AGENT              string
	AI_SEARCH_KNOWLEDGE_BASE     string
	AI_SEARCH_KNOWLEDGE_SOURCE   string
	AI_SEARCH_KNOWLEDGE_BASE_API string
	AI_SEARCH_TOPK               int

	STORAGE_BASE_URL            string
	STORAGE_KNOWLEDGE_CONTAINER string
	STORAGE_FEEDBACK_CONTAINER  string
	STORAGE_RECORDS_CONTAINER   string

	KEYVAULT_ENDPOINT string
}

var BotEnv *BotENV
var AppConfig *Config
var Credential azcore.TokenCredential

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

func initCredential() error {
	var creds []azcore.TokenCredential

	// 1: Workload Identity
	workloadCred, err := azidentity.NewWorkloadIdentityCredential(nil)
	if err == nil {
		creds = append(creds, workloadCred)
		log.Printf("Workload Identity credential added")
	} else {
		log.Printf("Workload Identity credential not available: %v", err)
	}

	// 2: Managed Identity
	clientID := os.Getenv("AZURE_CLIENT_ID")
	if len(clientID) > 0 {
		miOpts := &azidentity.ManagedIdentityCredentialOptions{
			ID: azidentity.ClientID(clientID),
		}
		miCred, miCredErr := azidentity.NewManagedIdentityCredential(miOpts)
		if miCredErr == nil {
			creds = append(creds, miCred)
			log.Printf("Managed Identity credential added")
		} else {
			log.Printf("Managed Identity credential not available: %v", miCredErr)
		}
	} else {
		log.Printf("AZURE_CLIENT_ID not set; skipping Managed Identity credential")
	}

	// 3: Azure CLI
	azCLI, err := azidentity.NewAzureCLICredential(nil)
	if err == nil {
		creds = append(creds, azCLI)
		log.Printf("Azure CLI credential added")
	} else {
		log.Printf("Azure CLI credential not available: %v", err)
	}

	if len(creds) == 0 {
		return fmt.Errorf("no valid credentials available")
	}

	chain, err := azidentity.NewChainedTokenCredential(creds, nil)
	if err != nil {
		return err
	}
	Credential = chain
	return nil
}

func InitConfiguration() {
	// Initialize the global credential first
	if err := initCredential(); err != nil {
		log.Fatalf("Failed to create credential: %v", err)
	}

	// Get the endpoint from environment variable
	endpoint := os.Getenv("AZURE_APPCONFIG_ENDPOINT")

	// Set up authentication options
	authOptions := azureappconfiguration.AuthenticationOptions{
		Endpoint:   endpoint,
		Credential: Credential,
	}

	// Load configuration from Azure App Configuration
	appConfig, err := azureappconfiguration.Load(context.TODO(), authOptions, nil)
	if err != nil {
		log.Fatalf("Failed to load configuration: %v", err)
	}

	var config Config
	if err := appConfig.Unmarshal(&config, nil); err != nil {
		log.Fatalf("Failed to unmarshal configuration: %v", err)
	}
	AppConfig = &config
}
