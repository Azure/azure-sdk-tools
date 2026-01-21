// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

package main

const (
	moduleName    = "github.com/Azure/azure-sdk-tools/tools/sdk-ai-bots/azure-sdk-qa-bot-backend"
	moduleVersion = "v0.7.0"
)

// GetVersion returns the module version
func GetVersion() string {
	return moduleVersion
}

// GetModuleName returns the module name
func GetModuleName() string {
	return moduleName
}
