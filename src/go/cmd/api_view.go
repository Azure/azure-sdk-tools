// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"encoding/json"
	"os"
	"path/filepath"
)

// CreateAPIView generates the output file that the API view tool uses.
func CreateAPIView(pkgDir, outputDir string) error {
	review, err := createReview(pkgDir)
	if err != nil {
		panic(err)
	}
	filename := filepath.Join(outputDir, review.Name+".json")
	file, _ := json.MarshalIndent(review, "", " ")
	err = os.WriteFile(filename, file, 0644)
	if err != nil {
		return err
	}
	return nil
}

func createReview(pkgDir string) (PackageReview, error) {
	r, err := NewReview(pkgDir)
	if err != nil {
		return PackageReview{}, err
	}
	return r.Review()
}
