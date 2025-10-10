// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.SampleGeneration
{
    /// <summary>
    /// Contains code templates for different programming languages used in sample generation.
    /// </summary>
    internal static class CodeTemplates
    {
        /// <summary>
        /// .NET sample template following Azure SDK conventions.
        /// </summary>
        public const string Dotnet = @"// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.TestFramework;
using Azure.Template.Models;
using NUnit.Framework;

namespace Azure.Template.Tests.Samples
{
    public partial class TemplateSamples: SamplesBase<TemplateClientTestEnvironment>
    {
        [Test]
        [AsyncOnly]
        public async Task GettingASecretAsync()
        {
            #region Snippet:Azure_Template_GetSecretAsync
#if SNIPPET
            string endpoint = ""https://myvault.vault.azure.net"";
            var credential = new DefaultAzureCredential();
#else
            string endpoint = TestEnvironment.KeyVaultUri;
            var credential = TestEnvironment.Credential;
#endif
            var client = new TemplateClient(endpoint, credential);

            SecretBundle secret = await client.GetSecretValueAsync(""TestSecret"");

            Console.WriteLine(secret.Value);
            #endregion

            Assert.NotNull(secret.Value);
        }
    }
}";

        /// <summary>
        /// TypeScript sample template following Azure SDK conventions.
        /// </summary>
        public const string TypeScript = @"// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/**
 * @summary Demonstrates the use of a ConfigurationClient to retrieve a setting value.
 */

import { ConfigurationClient } from ""@azure/template"";
import { DefaultAzureCredential } from ""@azure/identity"";

// Load the .env file if it exists
import ""dotenv/config"";

async function main(): Promise<void> {
  const endpoint = process.env.APPCONFIG_ENDPOINT || ""<endpoint>"";
  const key = process.env.APPCONFIG_TEST_SETTING_KEY || ""<test-key>"";

  const client = new ConfigurationClient(endpoint, new DefaultAzureCredential());

  const setting = await client.getConfigurationSetting(key);

  console.log(""The setting has a value of:"", setting.value);
  console.log(""Details:"", setting);
}

main().catch((err) => {
  console.error(""The sample encountered an error:"", err);
});";

        /// <summary>
        /// Python sample template following Azure SDK conventions.
        /// </summary>
        public const string Python = @"# ------------------------------------
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
# ------------------------------------
import datetime
import os

# ----------------------------------------------------------------------------------------------------------
# Prerequisites:
# 1. An Azure Key Vault (https://learn.microsoft.com/azure/key-vault/quick-create-cli)
#
# 2. azure-keyvault-keys and azure-identity libraries (pip install these)
#
# 3. Set environment variable VAULT_URL with the URL of your key vault
#    
# 4. Set up your environment to use azure-identity's DefaultAzureCredential. For more information about how to configure
#    the DefaultAzureCredential, refer to https://aka.ms/azsdk/python/identity/docs#azure.identity.DefaultAzureCredential
#
# 5. Key create, get, update, and delete permissions for your service principal in your vault
#
# ----------------------------------------------------------------------------------------------------------
# Sample - demonstrates the basic CRUD operations on a vault(key) resource for Azure Key Vault
#
# 1. Create a new RSA Key (create_rsa_key)
#
# 2. Create a new EC Key (create_ec_key)
#
# 3. Get an existing key (get_key)
#
# 4. Update an existing key (update_key)
#
# 5. Delete a key (begin_delete_key)
# ----------------------------------------------------------------------------------------------------------

# Instantiate a key client that will be used to call the service.
# Here we use the DefaultAzureCredential, but any azure-identity credential can be used.
# [START create_a_key_client]
from azure.identity import DefaultAzureCredential
from azure.keyvault.keys import KeyClient

VAULT_URL = os.environ[""VAULT_URL""]
credential = DefaultAzureCredential()
client = KeyClient(vault_url=VAULT_URL, credential=credential)
# [END create_a_key_client]

# Let's create an RSA key with size 2048, hsm disabled and optional key_operations of encrypt, decrypt.
# if the key already exists in the Key Vault, then a new version of the key is created.
print(""\n.. Create an RSA Key"")
key_size = 2048
key_ops = [""encrypt"", ""decrypt"", ""sign"", ""verify"", ""wrapKey"", ""unwrapKey""]
key_name = ""rsaKeyName""
rsa_key = client.create_rsa_key(key_name, size=key_size, key_operations=key_ops)
print(f""RSA Key with name '{rsa_key.name}' created of type '{rsa_key.key_type}'."")

# Let's create an Elliptic Curve key with algorithm curve type P-256.
# if the key already exists in the Key Vault, then a new version of the key is created.
print(""\n.. Create an EC Key"")
key_curve = ""P-256""
key_name = ""ECKeyName""
ec_key = client.create_ec_key(key_name, curve=key_curve)
print(f""EC Key with name '{ec_key.name}' created of type '{ec_key.key_type}'."")

# Let's get the rsa key details using its name
print(""\n.. Get a Key by its name"")
rsa_key = client.get_key(rsa_key.name)
print(f""Key with name '{rsa_key.name}' was found."")

# Let's say we want to update the expiration time for the EC key and disable the key to be usable
# for cryptographic operations. The update method allows the user to modify the metadata (key attributes)
# associated with a key previously stored within Key Vault.
print(""\n.. Update a Key by name"")
expires = datetime.datetime.utcnow() + datetime.timedelta(days=365)
updated_ec_key = client.update_key_properties(
    ec_key.name, ec_key.properties.version, expires_on=expires, enabled=False
)
print(f""Key with name '{updated_ec_key.name}' was updated on date '{updated_ec_key.properties.updated_on}'"")
print(f""Key with name '{updated_ec_key.name}' was updated to expire on '{updated_ec_key.properties.expires_on}'"")

# The RSA key is no longer used, need to delete it from the Key Vault.
print(""\n.. Delete Keys"")
client.begin_delete_key(ec_key.name)
client.begin_delete_key(rsa_key.name)
print(f""Deleted key '{ec_key.name}'"")
print(f""Deleted key '{rsa_key.name}'"")
";
        /// <summary>
        /// Java sample template following Azure SDK conventions.
        /// </summary>
        public const string Java = @"// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package com.azure.security.keyvault.keys;

import com.azure.core.http.rest.Response;
import com.azure.core.util.Context;
import com.azure.core.util.polling.PollResponse;
import com.azure.core.util.polling.SyncPoller;
import com.azure.security.keyvault.keys.models.CreateRsaKeyOptions;
import com.azure.security.keyvault.keys.models.DeletedKey;
import com.azure.security.keyvault.keys.models.KeyVaultKey;
import com.azure.identity.DefaultAzureCredentialBuilder;

import java.time.OffsetDateTime;

/**
 * Sample demonstrates how to set, get, update and delete a key.
 */
public class HelloWorld {
    /**
     * Authenticates with the key vault and shows how to set, get, update and delete a key in the key vault.
     *
     * @param args Unused. Arguments to the program.
     *
     * @throws IllegalArgumentException when invalid key vault endpoint is passed.
     * @throws InterruptedException when the thread is interrupted in sleep mode.
     */
    public static void main(String[] args) throws InterruptedException, IllegalArgumentException {
        /* Instantiate a KeyClient that will be used to call the service. Notice that the client is using default Azure
        credentials. For more information on this and other types of credentials, see this document:
        https://docs.microsoft.com/java/api/overview/azure/identity-readme?view=azure-java-stable.

        To get started, you'll need a URL to an Azure Key Vault. See the README
        (https://github.com/Azure/azure-sdk-for-java/blob/main/sdk/keyvault/azure-security-keyvault-keys/README.md)
        for links and instructions. */
        KeyClient keyClient = new KeyClientBuilder()
            .vaultUrl(""<your-key-vault-url>"")
            .credential(new DefaultAzureCredentialBuilder().build())
            .buildClient();

        // Let's create an RSA key valid for 1 year. If the key already exists in the key vault, then a new version of
        // the key is created.
        Response<KeyVaultKey> createKeyResponse =
            keyClient.createRsaKeyWithResponse(new CreateRsaKeyOptions(""CloudRsaKey"")
                .setExpiresOn(OffsetDateTime.now().plusYears(1))
                .setKeySize(2048), new Context(""key1"", ""value1""));

        // Let's validate the create key operation succeeded using the status code information in the response.
        System.out.printf(""Create Key operation succeeded with status code %s \n"", createKeyResponse.getStatusCode());

        // Let's get the RSA key from the key vault.
        KeyVaultKey cloudRsaKey = keyClient.getKey(""CloudRsaKey"");

        System.out.printf(""Key is returned with name %s and type %s \n"", cloudRsaKey.getName(),
            cloudRsaKey.getKeyType());

        // After one year, the RSA key is still required, we need to update the expiry time of the key.
        // The update method can be used to update the expiry attribute of the key.
        cloudRsaKey.getProperties().setExpiresOn(cloudRsaKey.getProperties().getExpiresOn().plusYears(1));

        KeyVaultKey updatedKey = keyClient.updateKeyProperties(cloudRsaKey.getProperties());

        System.out.printf(""Key's updated expiry time %s \n"", updatedKey.getProperties().getExpiresOn());

        // We need the RSA key with bigger key size, so you want to update the key in key vault to ensure it has the
        // required size. Calling createRsaKey() on an existing key creates a new version of the key in the key vault
        // with the new specified size.
        keyClient.createRsaKey(new CreateRsaKeyOptions(""CloudRsaKey"")
            .setExpiresOn(OffsetDateTime.now().plusYears(1))
            .setKeySize(4096));

        // The RSA key is no longer needed, need to delete it from the key vault.
        SyncPoller<DeletedKey, Void> rsaDeletedKeyPoller = keyClient.beginDeleteKey(""CloudRsaKey"");
        PollResponse<DeletedKey> pollResponse = rsaDeletedKeyPoller.poll();
        DeletedKey rsaDeletedKey = pollResponse.getValue();

        System.out.println(""Deleted Date  %s"" + rsaDeletedKey.getDeletedOn().toString());
        System.out.printf(""Deleted Key's Recovery Id %s"", rsaDeletedKey.getRecoveryId());

        // The key is being deleted on the server.
        rsaDeletedKeyPoller.waitForCompletion();

        // To ensure the key is deleted server-side.
        Thread.sleep(30000);

        // If the keyvault is soft-delete enabled, then deleted keys need to be purged for permanent deletion.
        keyClient.purgeDeletedKey(""CloudRsaKey"");
    }
}
";
        /// <summary>
        /// Go sample template following Azure SDK conventions.
        /// </summary>
        public const string Go = @"// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

package azopenai_test

import (
	""context""
	""fmt""
	""io""
	""os""

	""github.com/openai/openai-go""
)

// Example_audioTranscription demonstrates how to transcribe speech to text using Azure OpenAI's Whisper model.
// This example shows how to:
// - Create an Azure OpenAI client with token credentials
// - Read an audio file and send it to the API
// - Convert spoken language to written text using the Whisper model
// - Process the transcription response
//
// The example uses environment variables for configuration:
// - AOAI_WHISPER_ENDPOINT: Your Azure OpenAI endpoint URL
// - AOAI_WHISPER_MODEL: The deployment name of your Whisper model
//
// Audio transcription is useful for accessibility features, creating searchable archives of audio content,
// generating captions or subtitles, and enabling voice commands in applications.
func Example_audioTranscription() {
	if !CheckRequiredEnvVars(""AOAI_WHISPER_ENDPOINT"", ""AOAI_WHISPER_MODEL"") {
		fmt.Fprintf(os.Stderr, ""Skipping example, environment variables missing\n"")
		return
	}

	endpoint := os.Getenv(""AOAI_WHISPER_ENDPOINT"")
	model := os.Getenv(""AOAI_WHISPER_MODEL"")

	client, err := CreateOpenAIClientWithToken(endpoint, """")
	if err != nil {
		fmt.Fprintf(os.Stderr, ""ERROR: %s\n"", err)
		return
	}

	audio_file, err := os.Open(""testdata/sampledata_audiofiles_myVoiceIsMyPassportVerifyMe01.mp3"")
	if err != nil {
		fmt.Fprintf(os.Stderr, ""ERROR: %s\n"", err)
		return
	}
	defer audio_file.Close()

	resp, err := client.Audio.Transcriptions.New(context.TODO(), openai.AudioTranscriptionNewParams{
		Model:          openai.AudioModel(model),
		File:           audio_file,
		ResponseFormat: openai.AudioResponseFormatJSON,
	})

	if err != nil {
		fmt.Fprintf(os.Stderr, ""ERROR: %s\n"", err)
		return
	}

	fmt.Fprintf(os.Stderr, ""Transcribed text: %s\n"", resp.Text)
}

// Example_generateSpeechFromText demonstrates how to convert text to speech using Azure OpenAI's text-to-speech service.
// This example shows how to:
// - Create an Azure OpenAI client with token credentials
// - Send text to be converted to speech
// - Specify voice and audio format parameters
// - Handle the audio response stream
//
// The example uses environment variables for configuration:
// - AOAI_TTS_ENDPOINT: Your Azure OpenAI endpoint URL
// - AOAI_TTS_MODEL: The deployment name of your text-to-speech model
//
// Text-to-speech conversion is valuable for creating audiobooks, virtual assistants,
// accessibility tools, and adding voice interfaces to applications.
func Example_generateSpeechFromText() {
	if !CheckRequiredEnvVars(""AOAI_TTS_ENDPOINT"", ""AOAI_TTS_MODEL"") {
		fmt.Fprintf(os.Stderr, ""Skipping example, environment variables missing\n"")
		return
	}

	endpoint := os.Getenv(""AOAI_TTS_ENDPOINT"")
	model := os.Getenv(""AOAI_TTS_MODEL"")

	client, err := CreateOpenAIClientWithToken(endpoint, """")
	if err != nil {
		fmt.Fprintf(os.Stderr, ""ERROR: %s\n"", err)
		return
	}

	audioResp, err := client.Audio.Speech.New(context.Background(), openai.AudioSpeechNewParams{
		Model:          openai.SpeechModel(model),
		Input:          ""i am a computer"",
		Voice:          openai.AudioSpeechNewParamsVoiceAlloy,
		ResponseFormat: openai.AudioSpeechNewParamsResponseFormatFLAC,
	})

	if err != nil {
		fmt.Fprintf(os.Stderr, ""ERROR: %s\n"", err)
		return
	}

	defer audioResp.Body.Close()

	audioBytes, err := io.ReadAll(audioResp.Body)

	if err != nil {
		// TODO: Update the following line with your application specific error handling logic
		fmt.Fprintf(os.Stderr, ""ERROR: %s\n"", err)
		return
	}

	fmt.Fprintf(os.Stderr, ""Got %d bytes of FLAC audio\n"", len(audioBytes))
}

// Example_audioTranslation demonstrates how to translate speech from one language to English text.
// This example shows how to:
// - Create an Azure OpenAI client with token credentials
// - Read a non-English audio file
// - Translate the spoken content to English text
// - Process the translation response
//
// The example uses environment variables for configuration:
// - AOAI_WHISPER_ENDPOINT: Your Azure OpenAI endpoint URL
// - AOAI_WHISPER_MODEL: The deployment name of your Whisper model
//
// Speech translation is essential for cross-language communication, creating multilingual content,
// and building applications that break down language barriers.
func Example_audioTranslation() {
	if !CheckRequiredEnvVars(""AOAI_WHISPER_ENDPOINT"", ""AOAI_WHISPER_MODEL"") {
		fmt.Fprintf(os.Stderr, ""Skipping example, environment variables missing\n"")
		return
	}

	endpoint := os.Getenv(""AOAI_WHISPER_ENDPOINT"")
	model := os.Getenv(""AOAI_WHISPER_MODEL"")

	client, err := CreateOpenAIClientWithToken(endpoint, """")
	if err != nil {
		fmt.Fprintf(os.Stderr, ""ERROR: %s\n"", err)
		return
	}

	audio_file, err := os.Open(""testdata/sampleaudio_hindi_myVoiceIsMyPassportVerifyMe.mp3"")
	if err != nil {
		fmt.Fprintf(os.Stderr, ""ERROR: %s\n"", err)
		return
	}
	defer audio_file.Close()

	resp, err := client.Audio.Translations.New(context.TODO(), openai.AudioTranslationNewParams{
		Model:  openai.AudioModel(model),
		File:   audio_file,
		Prompt: openai.String(""Translate the following Hindi audio to English""),
	})

	if err != nil {
		fmt.Fprintf(os.Stderr, ""ERROR: %s\n"", err)
		return
	}

	fmt.Fprintf(os.Stderr, ""Translated text: %s\n"", resp.Text)
}
";
    }
}
