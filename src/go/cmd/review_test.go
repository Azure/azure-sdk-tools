package cmd

import (
	"path/filepath"
	"strings"
	"testing"

	"github.com/stretchr/testify/require"
)

func TestLocalModulePath(t *testing.T) {
	for _, tc := range []struct {
		expect, filePath, modPath string
	}{
		// realistic cases
		{
			expect:   "/home/me/azure-sdk-for-go/sdk/azcore",
			filePath: "/home/me/azure-sdk-for-go/sdk/storage/azblob",
			modPath:  "github.com/Azure/azure-sdk-for-go/sdk/azcore",
		},
		{
			expect:   "/home/me/sdk/foo/sdk/bar/sdk/internal",
			filePath: "/home/me/sdk/foo/sdk/bar/sdk/azcore",
			modPath:  "github.com/Azure/azure-sdk-for-go/sdk/internal",
		},
		{
			expect:   "c:/Users/me/work/sdk/storage/azblob",
			filePath: "c:/Users/me/work/sdk/azcore",
			modPath:  "github.com/Azure/azure-sdk-for-go/sdk/storage/azblob",
		},
		{
			expect:   "/home/me/az/sdk/security/keyvault/internal",
			filePath: "/home/me/az/sdk/security/keyvault/azkeys",
			modPath:  "github.com/Azure/azure-sdk-for-go/sdk/security/keyvault/internal",
		},
		{
			expect:   "c:/azure-sdk-for-go/sdk/azidentity",
			filePath: "c:/azure-sdk-for-go/sdk/azidentity/cache",
			modPath:  "github.com/Azure/azure-sdk-for-go/sdk/azidentity",
		},
		{
			expect:   "d:/azure-sdk-for-go/sdk/azidentity/cache",
			filePath: "d:/azure-sdk-for-go/sdk/azidentity",
			modPath:  "github.com/Azure/azure-sdk-for-go/sdk/azidentity/cache",
		},

		// unrealistic edge cases just to exercise the algorithm
		{
			expect:   "/home/me/azure-sdk-for-go/sdk/azcore",
			filePath: "/home/me/azure-sdk-for-go/sdk/azcore",
			modPath:  "github.com/Azure/azure-sdk-for-go/sdk/azcore",
		},
		{
			expect:   "",
			filePath: "/foo",
			modPath:  "",
		},
		{
			expect:   "",
			filePath: "",
			modPath:  "github.com/Azure/azure-sdk-for-go/sdk/azcore",
		},
		{
			expect:   "",
			filePath: "/home/me/azure-sdk-for-go/sdk/storage/azblob",
			modPath:  "net/http",
		},
		{
			expect:   "",
			filePath: "/home/me/azure-sdk-for-go/sdk/storage/azblob",
			modPath:  "github.com/Foo/bar",
		},
	} {
		expect := strings.ReplaceAll(tc.expect, "/", string(filepath.Separator))
		actual := localModulePath(tc.modPath, tc.filePath)
		require.Equal(t, expect, actual)
	}
}
