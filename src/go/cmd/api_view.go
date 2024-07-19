// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"encoding/json"
	"os"
	"path/filepath"
	"slices"
	"strings"
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

func recursiveSortNavigation(n Navigation) {
	for _, nn := range n.ChildItems {
		recursiveSortNavigation(nn)
	}
	slices.SortFunc(n.ChildItems, func(a Navigation, b Navigation) int {
		aa, err := json.Marshal(a)
		if err != nil {
			panic(err)
		}
		bb, err := json.Marshal(b)
		if err != nil {
			panic(err)
		}
		return strings.Compare(string(aa), string(bb))
	})
}
