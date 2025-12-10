package test

import (
	"context"
	"testing"

	"github.com/Azure/azure-sdk-for-go/sdk/keyvault/azsecrets"
	"github.com/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend/config"
	"github.com/stretchr/testify/require"
)

func TestListSecrets(t *testing.T) {
	config.InitConfiguration()
	//Establish a connection to the Key Vault client
	client, err := azsecrets.NewClient(config.AppConfig.KEYVAULT_ENDPOINT, config.Credential, nil)
	require.NoError(t, err)
	//List the keys in the Key Vault
	pager := client.NewListSecretsPager(nil)
	for pager.More() {
		resp, err := pager.NextPage(context.Background())
		require.NoError(t, err)
		for _, secret := range resp.Value {
			require.NotEmpty(t, *secret.ID)
		}
	}
}
