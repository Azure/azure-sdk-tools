// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"bytes"
	"encoding/json"
	"os"
	"path/filepath"
	"testing"

	"github.com/stretchr/testify/require"
)

func TestMain(m *testing.M) {
	indexTestdata = true
	os.Exit(m.Run())
}

func TestOutput(t *testing.T) {
	f := filepath.Join("testdata", "test_output", "output.json")
	expected, err := os.ReadFile(f)
	require.NoError(t, err)

	review, err := createReview(filepath.Dir(f))
	require.NoError(t, err)
	actual, err := json.MarshalIndent(review, "", "  ")
	require.NoError(t, err)
	actual = append(actual, '\n')
	// unconditionally writing the output to disk creates a diff for debugging failures
	require.NoError(t, os.WriteFile(f, actual, 0666))

	if !bytes.Equal(expected, actual) {
		t.Error("review content for testdata/test_output has changed")
	}

	t.Run("unique LineIDs", func(t *testing.T) {
		seen := map[string]bool{}
		forAll(review.ReviewLines, func(rl ReviewLine) {
			if id := rl.LineID; id != "" {
				if seen[id] {
					t.Error("duplicate LineID: " + id)
				}
				seen[id] = true
			}
		})
	})

	lineIDs := map[string]bool{}
	forAll(review.ReviewLines, func(rl ReviewLine) {
		lineIDs[rl.LineID] = true
	})

	t.Run("diagnostics", func(t *testing.T) {
		for _, diagnostic := range review.Diagnostics {
			if diagnostic.Text == "" {
				t.Errorf("broken diagnostic: empty text for %q", diagnostic.TargetID)
			}
			if diagnostic.Level == 0 {
				t.Errorf("broken diagnostic: no level for %q", diagnostic.TargetID)
			}
			if !lineIDs[diagnostic.TargetID] {
				t.Errorf("broken diagnostic: no LineID corresponds to TargetID %q", diagnostic.TargetID)
			}
		}
	})

	t.Run("navigation links", func(t *testing.T) {
		searchTokens(review.ReviewLines, func(rt ReviewToken) bool {
			if !lineIDs[rt.NavigateToID] {
				t.Errorf("broken navigation link: no LineID corresponds to NavigateToID %q", rt.NavigateToID)
			}
			return false
		})
	})
}

func TestMultiModule(t *testing.T) {
	for _, path := range []string{
		"testdata/test_multi_module",
		"testdata/test_multi_module/A",
		"testdata/test_multi_module/A/B",
	} {
		t.Run(path, func(t *testing.T) {
			p, err := createReview(filepath.Clean(path))
			require.NoError(t, err)
			require.Equal(t, 1, len(p.Navigation), "review should include only one package")
			require.Equal(t, filepath.Base(path), p.Navigation[0].Text, "review includes the wrong module")
		})
	}
}

func TestSubpackage(t *testing.T) {
	review, err := createReview(filepath.Clean("testdata/test_subpackage"))
	require.NoError(t, err)
	require.Equal(t, "Go", review.Language)
	require.Equal(t, "test_subpackage", review.Name)
	seen := map[string]bool{}
	searchLines(review.ReviewLines, func(rl ReviewLine) bool {
		if id := rl.LineID; id != "" {
			if seen[id] {
				t.Error("duplicate LineID: " + id)
			}
			seen[id] = true
		}
		return false // examine all lines
	})
	// 2 packages * 10 exports each = 22 unique definition IDs expected
	require.Equal(t, 22, len(seen))
	// 10 exports - 4 methods = 6 nav links expected
	require.Equal(t, 2, len(review.Navigation))
	expectedPackages := []string{"test_subpackage", "test_subpackage/subpackage"}
	for _, nav := range review.Navigation {
		require.Contains(t, expectedPackages, nav.Text)
		require.Equal(t, 6, len(nav.ChildItems))
		for _, item := range nav.ChildItems {
			require.Contains(t, seen, item.NavigationID)
		}
	}
}

func TestDiagnostics(t *testing.T) {
	review, err := createReview(filepath.Clean("testdata/test_diagnostics"))
	require.NoError(t, err)
	require.Equal(t, "Go", review.Language)
	require.Equal(t, "test_diagnostics", review.Name)
	require.Equal(t, 4, len(review.Diagnostics))
	for _, diagnostic := range review.Diagnostics {
		switch target := diagnostic.TargetID; target {
		case "test_diagnostics.Alias":
			require.Equal(t, CodeDiagnosticLevelInfo, diagnostic.Level)
			require.Equal(t, aliasFor+"internal.InternalStruct", diagnostic.Text)
		case "test_diagnostics.ExportedStruct":
			require.Equal(t, CodeDiagnosticLevelError, diagnostic.Level)
			require.Equal(t, diagnostic.Text, embedsUnexportedStruct+"unexportedStruct")
		case "test_diagnostics.ExternalAlias":
			require.Equal(t, CodeDiagnosticLevelWarning, diagnostic.Level)
			require.Equal(t, aliasFor+"net/http.Client", diagnostic.Text)
		case "test_diagnostics.Sealed":
			require.Equal(t, CodeDiagnosticLevelInfo, diagnostic.Level)
			require.Equal(t, sealedInterface, diagnostic.Text)
		default:
			t.Fatal("unexpected target " + target)
		}
	}
}

func TestExternalModule(t *testing.T) {
	review, err := createReview(filepath.Clean("testdata/test_external_module"))
	require.NoError(t, err)
	require.Equal(t, 1, len(review.Diagnostics))
	require.Equal(t, aliasFor+"github.com/Azure/azure-sdk-for-go/sdk/azcore.Policy", review.Diagnostics[0].Text)
	require.Equal(t, 1, len(review.Navigation))
	require.Equal(t, 1, len(review.Navigation[0].ChildItems))
	foundDo, foundPolicy := false, false
	searchLines(review.ReviewLines, func(rl ReviewLine) bool {
		if rl.LineID == "test_external_module.MyPolicy" {
			foundPolicy = true
		}
		if foundPolicy {
			for _, token := range rl.Tokens {
				if token.Value == "Do" {
					foundDo = true
					return true
				}
			}
		}
		return false
	})
	require.True(t, foundDo, "missing MyPolicy.Do()")
	require.True(t, foundPolicy, "missing MyPolicy type")
}

func TestAliasDefinitions(t *testing.T) {
	for _, test := range []struct {
		name, path, sourceName string
		diagLevel              CodeDiagnosticLevel
	}{
		{
			diagLevel:  CodeDiagnosticLevelWarning,
			name:       "service_group",
			path:       "testdata/test_service_group/group/test_alias_export",
			sourceName: "github.com/Azure/azure-sdk-tools/src/go/cmd/testdata/test_service_group/group/internal.Foo",
		},
		{
			diagLevel:  CodeDiagnosticLevelInfo,
			name:       "internal_package",
			path:       "testdata/test_alias_export",
			sourceName: "internal/exported.Foo",
		},
		{
			diagLevel:  CodeDiagnosticLevelWarning,
			name:       "external_package",
			path:       "testdata/test_external_alias_exporter",
			sourceName: "github.com/Azure/azure-sdk-tools/src/go/cmd/testdata/test_external_alias_source.Foo",
		},
	} {
		t.Run(test.name, func(t *testing.T) {
			p, err := filepath.Abs(test.path)
			require.NoError(t, err)
			review, err := createReview(p)
			require.NoError(t, err)
			require.Equal(t, "Go", review.Language)
			require.Equal(t, 1, len(review.Diagnostics))
			require.Equal(t, test.diagLevel, review.Diagnostics[0].Level)
			require.Equal(t, aliasFor+test.sourceName, review.Diagnostics[0].Text)
			require.Equal(t, 1, len(review.Navigation))
			require.Equal(t, filepath.Base(test.path), review.Navigation[0].Text)
			found := searchTokens(review.ReviewLines, func(rt ReviewToken) bool { return rt.Value == "Bar" })
			require.True(t, found, "review doesn't contain the aliased struct's definition")
		})
	}
}

func TestRecursiveAliasDefinitions(t *testing.T) {
	for _, test := range []struct {
		name, path, sourceName string
		diagLevel              CodeDiagnosticLevel
	}{
		{
			diagLevel:  CodeDiagnosticLevelInfo,
			name:       "internal_package",
			path:       "testdata/test_recursive_alias",
			sourceName: "service.Foo",
		},
	} {
		t.Run(test.name, func(t *testing.T) {
			review, err := createReview(filepath.Clean(test.path))
			require.NoError(t, err)
			require.Equal(t, "Go", review.Language)
			require.Equal(t, 2, len(review.Diagnostics))
			require.Equal(t, test.diagLevel, review.Diagnostics[0].Level)
			require.Equal(t, aliasFor+test.sourceName, review.Diagnostics[0].Text)
			require.Equal(t, 2, len(review.Navigation))
			require.Equal(t, filepath.Base(test.path), review.Navigation[0].Text)
			found := searchTokens(review.ReviewLines, func(rt ReviewToken) bool { return rt.Value == "Bar" })
			require.True(t, found, "review doesn't contain the aliased struct's definition")
		})
	}
}

func TestAliasDiagnostics(t *testing.T) {
	review, err := createReview(filepath.Clean("testdata/test_alias_diagnostics"))
	require.NoError(t, err)
	require.Equal(t, "Go", review.Language)
	require.Equal(t, "test_alias_diagnostics", review.Name)
	require.Equal(t, 6, len(review.Diagnostics))
	for _, diagnostic := range review.Diagnostics {
		if diagnostic.TargetID == "test_alias_diagnostics.WidgetValue" {
			require.Equal(t, CodeDiagnosticLevelInfo, diagnostic.Level)
			require.Equal(t, aliasFor+"internal.WidgetValue", diagnostic.Text)
		} else {
			require.Equal(t, "test_alias_diagnostics.Widget", diagnostic.TargetID)
			switch diagnostic.Level {
			case CodeDiagnosticLevelInfo:
				require.Equal(t, aliasFor+"internal.Widget", diagnostic.Text)
			case CodeDiagnosticLevelError:
				switch txt := diagnostic.Text; txt {
				case missingAliasFor + "WidgetProperties":
				case missingAliasFor + "WidgetPropertiesP":
				case missingAliasFor + "WidgetThings":
				case missingAliasFor + "WidgetThingsP":
				default:
					t.Fatalf("unexpected diagnostic text %s", txt)
				}
			default:
				t.Fatalf("unexpected diagnostic level %d", diagnostic.Level)
			}
		}
	}
}

func TestMajorVersion(t *testing.T) {
	review, err := createReview(filepath.Clean("testdata/test_major_version"))
	require.NoError(t, err)
	require.Equal(t, "Go", review.Language)
	require.Equal(t, "test_major_version", review.Name)
	require.Equal(t, 1, len(review.Navigation))
	require.Equal(t, "test_major_version/subpackage", review.Navigation[0].Text)
}

func TestVars(t *testing.T) {
	review, err := createReview(filepath.Clean("testdata/test_vars"))
	require.NoError(t, err)
	require.NotZero(t, review)
	countSomeChoice := 0
	hasHTTPClient := false
	searchLines(review.ReviewLines, func(rl ReviewLine) bool {
		for i, token := range rl.Tokens {
			if token.Value == "SomeChoice" && rl.Tokens[i-1].Value == "*" {
				countSomeChoice++
			} else if token.Value == "http.Client" && rl.Tokens[i-1].Value == "*" {
				hasHTTPClient = true
			}
		}
		return false
	})
	require.NoError(t, err)
	require.EqualValues(t, 2, countSomeChoice)
	require.True(t, hasHTTPClient)
}

func Test_getPackageNameFromModPath(t *testing.T) {
	require.EqualValues(t, "foo", getPackageNameFromModPath("foo"))
	require.EqualValues(t, "foo", getPackageNameFromModPath("foo/v2"))
	require.EqualValues(t, "sdk/foo", getPackageNameFromModPath("github.com/Azure/azure-sdk-for-go/sdk/foo"))
	require.EqualValues(t, "sdk/foo/bar", getPackageNameFromModPath("github.com/Azure/azure-sdk-for-go/sdk/foo/bar"))
	require.EqualValues(t, "sdk/foo/bar", getPackageNameFromModPath("github.com/Azure/azure-sdk-for-go/sdk/foo/bar/v5"))
}

func TestDeterministicOutput(t *testing.T) {
	for i := 0; i < 100; i++ {
		review1, err := createReview(filepath.Clean("testdata/test_multi_recursive_alias"))
		require.NoError(t, err)
		review2, err := createReview(filepath.Clean("testdata/test_multi_recursive_alias"))
		require.NoError(t, err)

		output1, err := json.MarshalIndent(review1, "", " ")
		require.NoError(t, err)
		output2, err := json.MarshalIndent(review2, "", " ")
		require.NoError(t, err)

		require.EqualValues(t, string(output1), string(output2))
	}
}

// searchLines recursively searches for a line matching the given predicate.
// It returns true when the predicate returns true, and false if the predicate
// returns false for every line.
func searchLines(lines []ReviewLine, match func(ReviewLine) bool) bool {
	found := false
	forAll(lines, func(ln ReviewLine) {
		if match(ln) {
			found = true
		}
	})
	return found
}

// searchTokens searches for a token matching the given predicate.
// It returns true when the predicate returns true, and false if the predicate
// returns false for every token.
func searchTokens(lines []ReviewLine, match func(ReviewToken) bool) bool {
	return searchLines(lines, func(rl ReviewLine) bool {
		for _, tk := range rl.Tokens {
			if match(tk) {
				return true
			}
		}
		return false
	})
}
