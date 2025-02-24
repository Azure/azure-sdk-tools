// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"path/filepath"
	"testing"

	"github.com/stretchr/testify/require"
)

func TestName(t *testing.T) {
	for _, test := range []struct {
		modulePath, moduleRoot, pkgPath, want string
	}{
		{
			modulePath: "test_major_version/v2",
			moduleRoot: "testdata/test_major_version",
			pkgPath:    "subpackage",
			want:       "test_major_version/subpackage",
		},
		{
			modulePath: "test_package_name",
			moduleRoot: "testdata/test_package_name/test_package_name@v1.0.0",
			want:       "test_package_name",
		},
		{
			modulePath: "test_package_name",
			moduleRoot: "testdata/test_package_name/test_package_name@v1.0.0",
			pkgPath:    "subpackage",
			want:       "test_package_name/subpackage",
		},
		{
			modulePath: "test_subpackage",
			moduleRoot: "testdata/test_subpackage",
			want:       "test_subpackage",
		},
		{
			modulePath: "test_subpackage",
			moduleRoot: "testdata/test_subpackage",
			pkgPath:    "subpackage",
			want:       "test_subpackage/subpackage",
		},
	} {
		t.Run("", func(t *testing.T) {
			d, err := filepath.Abs(test.moduleRoot)
			require.NoError(t, err)
			p, err := NewPkg(filepath.Join(d, test.pkgPath), test.modulePath, d)
			require.NoError(t, err)
			require.Equal(t, test.want, p.Name())
		})
	}
}
