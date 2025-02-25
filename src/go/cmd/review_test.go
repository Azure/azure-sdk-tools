// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"path/filepath"
	"strings"
	"testing"

	"github.com/stretchr/testify/require"
	"golang.org/x/mod/module"
)

func TestLocalModulePath(t *testing.T) {
	for _, tc := range []struct {
		expect, filePath string
		mod              module.Version
	}{
		// realistic cases
		{
			expect:   "/home/me/azure-sdk-for-go/sdk/azcore",
			filePath: "/home/me/azure-sdk-for-go/sdk/storage/azblob",
			mod:      module.Version{Path: "github.com/Azure/azure-sdk-for-go/sdk/azcore"},
		},
		{
			expect:   "/home/me/sdk/foo/sdk/bar/sdk/internal",
			filePath: "/home/me/sdk/foo/sdk/bar/sdk/azcore",
			mod:      module.Version{Path: "github.com/Azure/azure-sdk-for-go/sdk/internal"},
		},
		{
			expect:   "c:/Users/me/work/sdk/storage/azblob",
			filePath: "c:/Users/me/work/sdk/azcore",
			mod:      module.Version{Path: "github.com/Azure/azure-sdk-for-go/sdk/storage/azblob"},
		},
		{
			expect:   "/home/me/az/sdk/security/keyvault/internal",
			filePath: "/home/me/az/sdk/security/keyvault/azkeys",
			mod:      module.Version{Path: "github.com/Azure/azure-sdk-for-go/sdk/security/keyvault/internal"},
		},
		{
			expect:   "c:/azure-sdk-for-go/sdk/azidentity",
			filePath: "c:/azure-sdk-for-go/sdk/azidentity/cache",
			mod:      module.Version{Path: "github.com/Azure/azure-sdk-for-go/sdk/azidentity"},
		},
		{
			expect:   "d:/azure-sdk-for-go/sdk/azidentity/cache",
			filePath: "d:/azure-sdk-for-go/sdk/azidentity",
			mod:      module.Version{Path: "github.com/Azure/azure-sdk-for-go/sdk/azidentity/cache"},
		},
		{
			expect:   "/home/me/azure-sdk-for-go/sdk/internal",
			filePath: "/home/me/azure-sdk-for-go/sdk/resourcemanager/internal",
			mod:      module.Version{Path: "github.com/Azure/azure-sdk-for-go/sdk/internal"},
		},
		{
			expect:   "/home/me/azure-sdk-for-go/sdk/internal",
			filePath: "/home/me/azure-sdk-for-go/sdk/security/keyvault/internal",
			mod:      module.Version{Path: "github.com/Azure/azure-sdk-for-go/sdk/internal"},
		},

		// unrealistic edge cases just to exercise the algorithm
		{
			expect:   "/home/me/azure-sdk-for-go/sdk/azcore",
			filePath: "/home/me/azure-sdk-for-go/sdk/azcore",
			mod:      module.Version{Path: "github.com/Azure/azure-sdk-for-go/sdk/azcore"},
		},
		{
			expect:   "/home/me/azure-sdk-for-go/sdk/security/keyvault/internal",
			filePath: "/home/me/azure-sdk-for-go/sdk/internal",
			mod:      module.Version{Path: "github.com/Azure/azure-sdk-for-go/sdk/security/keyvault/internal"},
		},
		{
			expect:   "",
			filePath: "/foo",
			mod:      module.Version{Path: ""},
		},
		{
			expect:   "",
			filePath: "",
			mod:      module.Version{Path: "github.com/Azure/azure-sdk-for-go/sdk/azcore"},
		},
		{
			expect:   "",
			filePath: "/home/me/azure-sdk-for-go/sdk/storage/azblob",
			mod:      module.Version{Path: "net/http"},
		},
		{
			expect:   "",
			filePath: "/home/me/azure-sdk-for-go/sdk/storage/azblob",
			mod:      module.Version{Path: "github.com/Foo/bar"},
		},
	} {
		expect := strings.ReplaceAll(tc.expect, "/", string(filepath.Separator))
		actual := localModulePath(tc.mod, tc.filePath)
		require.Equal(t, expect, actual)
	}
}
