package config

import (
	"fmt"
	"log"
	"os"
)

type BotENV struct {
	BOT_CLIENT_ID string
	BOT_TENANT_ID string
}

const (
	bot_client_id = "BOT_CLIENT_ID"
	bot_tenant_id = "BOT_TENANT_ID"
)

var BotEnv *BotENV

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
	if BotEnv == nil {
		if err := InitBotEnv(); err != nil {
			log.Println(err)
			return ""
		}
	}
	return BotEnv.BOT_CLIENT_ID
}

func GetBotTenantID() string {
	if BotEnv == nil {
		if err := InitBotEnv(); err != nil {
			log.Println(err)
			return ""
		}
	}
	return BotEnv.BOT_TENANT_ID
}
