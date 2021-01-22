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
	Use:   "apiview <packageDir> <outputDir>",
	Short: "apiview will generate the JSON token output for APIView",
	Long: `The apiview command can be used to generate the tokenized 
output needed by the APIView tool to show a view of publicly 
exposed portions of an SDK. The generated file will use the 
following naming format: <module name>.json
NOTE: This command will overwrite any file with the same name 
in the output directory.`,
	// Uncomment the following line if your bare application
	// has an action associated with it:
	Run: func(cmd *cobra.Command, args []string) {
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
