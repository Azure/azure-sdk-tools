#!/usr/bin/env python3
"""
Simple test script for the Code Review MCP Server.
This bypasses MCP and directly calls the API to verify it works.
"""

import asyncio
import httpx
import json

CODE_REVIEW_API_URL = "http://localhost:8088/code_review"

# Sample Go code with intentional guideline violations
TEST_CODE = """//go:build go1.18
// +build go1.18

package armstorage

import (
	"context"
	"github.com/Azure/azure-sdk-for-go/sdk/azcore"
	"github.com/Azure/azure-sdk-for-go/sdk/azcore/arm"
)

// storageAccountsClient contains the methods for the StorageAccounts group.
type storageAccountsClient struct {
	internal       *arm.Client
	subscriptionID string
}

// NewstorageAccountsClient creates a new instance of StorageAccountsClient.
func NewstorageAccountsClient(subscriptionID string, credential azcore.TokenCredential, options *arm.ClientOptions) (*storageAccountsClient, error) {
	cl, err := arm.NewClient(moduleName, moduleVersion, credential, options)
	if err != nil {
		return nil, err
	}
	return &storageAccountsClient{
		subscriptionID: subscriptionID,
		internal:       cl,
	}, nil
}

// StorageAccount represents a storage account resource.
type StorageAccount struct {
	location *string
	tags     map[string]*string
	id       *string
	name     *string
}

// PublicNetworkAccess defines whether public access is allowed
type PublicNetworkAccess string

const (
	public_access_enabled  PublicNetworkAccess = "Enabled"
	public_access_disabled PublicNetworkAccess = "Disabled"
)

// delete - Delete a storage account.
func (client *storageAccountsClient) delete(ctx context.Context, resourceGroupName string, accountName string) error {
	return nil
}
"""


async def test_code_review():
    """Test the code review API."""
    print("=" * 60)
    print("Testing Azure SDK Code Review API")
    print("=" * 60)
    print(f"\nAPI URL: {CODE_REVIEW_API_URL}")
    print("\n" + "-" * 60)
    print("Sending test code for review...")
    print("-" * 60)

    payload = {
        "language": "go",
        "code": TEST_CODE,
        "file_path": "sdk/resourcemanager/storage/armstorage/client.go"
    }

    try:
        async with httpx.AsyncClient(timeout=120.0) as client:
            response = await client.post(
                CODE_REVIEW_API_URL,
                json=payload,
                headers={"Content-Type": "application/json"}
            )
            response.raise_for_status()
            result = response.json()

        print("\n✅ API Response Received!\n")
        print("-" * 60)
        print("Raw JSON Response:")
        print("-" * 60)
        print(json.dumps(result, indent=2))

        print("\n" + "=" * 60)
        print("Formatted Review Results")
        print("=" * 60)

        review_id = result.get("id", "N/A")
        language = result.get("language", "N/A")
        summary = result.get("summary", "")
        comments = result.get("comments", [])

        print(f"\nReview ID: {review_id}")
        print(f"Language: {language}")
        print(f"Summary: {summary}")

        if not comments:
            print("\n✅ No guideline violations detected!")
        else:
            print(f"\n📋 Found {len(comments)} issue(s):\n")

            for i, comment in enumerate(comments, 1):
                print(f"\n--- Issue {i} ---")
                print(f"Problem: {comment.get('comment', 'N/A')}")
                print(f"Bad Code: {comment.get('bad_code', 'N/A')}")
                if comment.get('suggestion'):
                    print(f"Suggestion: {comment.get('suggestion')}")
                if comment.get('guideline_link'):
                    print(f"Guideline: {comment.get('guideline_link')}")

    except httpx.ConnectError:
        print(f"\n❌ Error: Could not connect to {CODE_REVIEW_API_URL}")
        print("   Make sure the Azure SDK QA Bot backend is running:")
        print("   cd tools/sdk-ai-bots/azure-sdk-qa-bot-backend && go run main.go")

    except httpx.TimeoutException:
        print("\n❌ Error: Request timed out")

    except httpx.HTTPStatusError as e:
        print(f"\n❌ Error: HTTP {e.response.status_code}")
        print(f"   Response: {e.response.text}")

    except Exception as e:
        print(f"\n❌ Error: {type(e).__name__}: {e}")


if __name__ == "__main__":
    asyncio.run(test_code_review())
