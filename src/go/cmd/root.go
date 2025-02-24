// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"fmt"
	"os"

	"github.com/spf13/cobra"
)

// rootCmd represents the base command when called without any subcommands
var rootCmd = &cobra.Command{
	Use: "apiviewgo <moduleDir> <outputDir>",
	Long: `apiviewgo outputs a file representing the public API of an Azure SDK for Go
module in APIView format. It writes this file to <outputDir>/<module name>.json,
overwriting any file of the same name.`,
	Run: func(cmd *cobra.Command, args []string) {
		if len(args) != 2 {
			err := cmd.Help()
			if err != nil {
				fmt.Println(err)
			}
			return
		}
		err := CreateAPIView(args[0], args[1])
		if err != nil {
			fmt.Println(err)
		}
	},
}

// Execute adds all child commands to the root command and sets flags appropriately.
// This is called by main.main(). It only needs to happen once to the rootCmd.
func Execute() {
	if err := rootCmd.Execute(); err != nil {
		fmt.Println(err)
		os.Exit(1)
	}
}
