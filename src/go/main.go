// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package main

import (
	"fmt"
	"os"
)

func main() {
	// the first arguement should be the directory of the Go library for which to generate the output
	dirPath := os.Args[1]
	outputPath := os.Args[2]
	err := CreateAPIView(dirPath, outputPath)
	if err != nil {
		fmt.Println(err.Error())
		return
	}
}
