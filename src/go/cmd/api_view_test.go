// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"bytes"
	"encoding/json"
	"go/ast"
	"os"
	"path/filepath"
	"strings"
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
	// normalizing line endings prevents flakiness due to git's handling of CRLF
	expected = bytes.ReplaceAll(expected, []byte("\r\n"), []byte("\n"))

	review, err := createReview(filepath.Dir(f))
	require.NoError(t, err)
	actual, err := json.MarshalIndent(review, "", "  ")
	require.NoError(t, err)
	actual = bytes.ReplaceAll(append(actual, '\n'), []byte("\r\n"), []byte("\n"))
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
	require.Equal(t, "1.8.9", review.PackageVersion)
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
	require.Equal(t, "1.1.0", review.PackageVersion)
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
	t.Setenv("GOPROXY", "off")
	review, err := createReview(filepath.Clean("testdata/test_external_module"))
	require.NoError(t, err)
	require.Equal(t, "1.0.0-beta.1", review.PackageVersion)
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
		{
			diagLevel:  CodeDiagnosticLevelInfo,
			name:       "internal_package_major_version",
			path:       "testdata/test_alias_export_major_version",
			sourceName: "internal/exported.Foo",
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

func TestAliasCompositeDefinitions(t *testing.T) {
	dir := t.TempDir()
	files := map[string]string{
		"go.mod": "module example.com/aliastest\n\ngo 1.21\n",
		"alias.go": `package aliastest
import (
	"example.com/aliastest/internal/bridge"
	"example.com/aliastest/internal/types"
)
type Slice = types.Data
type AnotherSlice = types.Data
type Map = types.StringMap
type Handler = types.Callback
type Status = types.Code
type Record = types.Entry
type Defined types.Data
func (Defined) Local() {}
type Indirect = bridge.Defined
`,
		"internal/bridge/bridge.go": `package bridge
import "example.com/aliastest/internal/types"
type Defined types.Data
func (Defined) BridgeOnly() {}
`,
		"internal/types/types.go": `package types
type Data []byte
func (Data) Len() int { return 0 }
func (*Data) Reset() {}
type StringMap map[string]int
func (StringMap) Size() int { return 0 }
type Callback func(int) string
func (Callback) Call() string { return "" }
type Code string
func (Code) Label() string { return "" }
type Entry struct{}
func (Entry) Valid() bool { return true }
`,
	}
	for name, content := range files {
		p := filepath.Join(dir, filepath.FromSlash(name))
		require.NoError(t, os.MkdirAll(filepath.Dir(p), 0755))
		require.NoError(t, os.WriteFile(p, []byte(content), 0644))
	}

	typeText := func(line ReviewLine) string {
		var b strings.Builder
		for _, token := range line.Tokens {
			if token.Kind == TokenKindComment {
				continue
			}
			b.WriteString(token.Value)
		}
		return b.String()
	}
	findLine := func(t *testing.T, review CodeFile, id string) ReviewLine {
		t.Helper()
		var found ReviewLine
		forAll(review.ReviewLines, func(line ReviewLine) {
			if line.LineID == id {
				found = line
			}
		})
		require.Equal(t, id, found.LineID, "missing definition")
		return found
	}
	review, err := createReview(dir)
	require.NoError(t, err)
	for _, test := range []struct {
		name, definition string
		methods          []string
		absent           []string
	}{
		{"Slice", "=[]byte", []string{"Len", "Reset"}, nil},
		{"AnotherSlice", "=[]byte", []string{"Len", "Reset"}, nil},
		{"Map", "=map[string]int", []string{"Size"}, nil},
		{"Handler", "=func(int)string", []string{"Call"}, nil},
		{"Status", "=string", []string{"Label"}, nil},
		{"Record", "", []string{"Valid"}, nil},
		{"Defined", "[]byte", []string{"Local"}, []string{"Len", "Reset"}},
		{"Indirect", "=internal/bridge.Defined", []string{"BridgeOnly"}, []string{"Len", "Reset"}},
	} {
		t.Run(test.name, func(t *testing.T) {
			line := findLine(t, review, "aliastest."+test.name)
			if test.definition != "" {
				require.Equal(t, "type"+test.name+test.definition, typeText(line))
			}
			for _, method := range test.methods {
				require.True(t, searchLines(line.Children, func(child ReviewLine) bool {
					return strings.Contains(child.LineID, "aliastest-(") &&
						strings.Contains(typeText(child), test.name+")"+method+"(")
				}), "missing method %s on %s", method, test.name)
			}
			for _, method := range test.absent {
				require.False(t, searchLines(line.Children, func(child ReviewLine) bool {
					return strings.Contains(typeText(child), test.name+")"+method+"(")
				}), "unexpected method %s on %s", method, test.name)
			}
		})
	}

	sourcePath := filepath.Join(dir, "internal", "types", "types.go")
	changed := strings.Replace(files["internal/types/types.go"], "[]byte", "[]rune", 1)
	changed = strings.Replace(changed, "map[string]int", "map[string]int64", 1)
	require.NoError(t, os.WriteFile(sourcePath, []byte(changed), 0644))
	changedReview, err := createReview(dir)
	require.NoError(t, err)
	require.Equal(t, "typeSlice=[]rune", typeText(findLine(t, changedReview, "aliastest.Slice")))
	require.Equal(t, "typeMap=map[string]int64", typeText(findLine(t, changedReview, "aliastest.Map")))
	require.True(t, searchTokens([]ReviewLine{findLine(t, changedReview, "aliastest.Slice")}, func(token ReviewToken) bool {
		return token.Value == "/* alias of example.com/aliastest/internal/types.Data */"
	}))
}

func TestAliasUnsupportedDefinition(t *testing.T) {
	p := &Pkg{modulePath: "example.com/aliastest", relName: "aliastest", c: newContent(), types: map[string]typeDef{}}
	alias := TypeAlias{Name: "Alias", Package: p, QualifiedName: "example.com/aliastest/internal.Pointer", isAlias: true}
	def := typeDef{n: &ast.TypeSpec{Name: ast.NewIdent("Pointer"), Type: &ast.StarExpr{}}, p: p}
	require.ErrorContains(t, alias.Resolve(def), "unsupported type *ast.StarExpr")
}

func TestAliasUnsupportedCLIError(t *testing.T) {
	dir := aliasFeedbackFixture(t, "type Public = types.Data\n", "type Data *int\n", nil)
	output := t.TempDir()
	rootCmd.SetArgs([]string{dir, output})
	t.Cleanup(func() { rootCmd.SetArgs(nil) })
	require.ErrorContains(t, rootCmd.Execute(), "unsupported type *ast.StarExpr")
	files, err := os.ReadDir(output)
	require.NoError(t, err)
	require.Empty(t, files)
}

func TestAliasAdditionalDefinitions(t *testing.T) {
	for _, test := range []struct {
		name, original, changed, originalType, changedType string
	}{
		{"channel", "chan int", "chan string", "chanint", "chanstring"},
		{"send channel", "chan<- int", "chan<- string", "chan<-int", "chan<-string"},
		{"receive channel", "<-chan int", "<-chan string", "<-chanint", "<-chanstring"},
		{"parenthesized slice", "([]byte)", "([]rune)", "[]byte", "[]rune"},
	} {
		t.Run(test.name, func(t *testing.T) {
			dir := aliasFeedbackFixture(t, "type Public = types.Data\n", "type Data "+test.original+"\n", nil)
			review, err := createReview(dir)
			require.NoError(t, err)
			require.Equal(t, "typePublic="+test.originalType, withoutAliasTarget(feedbackLineText(feedbackLine(t, review, "aliastest.Public"))))

			source := filepath.Join(dir, "internal", "types", "types.go")
			require.NoError(t, os.WriteFile(source, []byte("package types\ntype Data "+test.changed+"\n"), 0644))
			review, err = createReview(dir)
			require.NoError(t, err)
			require.Equal(t, "typePublic="+test.changedType, withoutAliasTarget(feedbackLineText(feedbackLine(t, review, "aliastest.Public"))))
		})
	}

	for _, typ := range []string{"*int", "(*int)"} {
		t.Run("unsupported pointer "+typ, func(t *testing.T) {
			dir := aliasFeedbackFixture(t, "type Public = types.Data\n", "type Data "+typ+"\n", nil)
			_, err := createReview(dir)
			require.ErrorContains(t, err, "unsupported type *ast.StarExpr")
		})
	}
	for _, alias := range []string{
		"type Public = *types.Data\n",
		"type Public = (*types.Data)\n",
		"type hidden = *types.Data\ntype Public = hidden\n",
	} {
		t.Run("direct pointer alias "+alias, func(t *testing.T) {
			dir := aliasFeedbackFixture(t, alias, "type Data struct{}\n", nil)
			_, err := createReview(dir)
			require.ErrorContains(t, err, "unsupported type *ast.StarExpr")
		})
	}
	t.Run("defined pointer", func(t *testing.T) {
		dir := aliasFeedbackFixture(t, "type Public *types.Data\n", "type Data struct{}\n", nil)
		_, err := createReview(dir)
		require.ErrorContains(t, err, "hidden component type")
	})
	t.Run("channel methods", func(t *testing.T) {
		dir := aliasFeedbackFixture(t, "type Public = types.Data\n", `
type Data chan int
func (Data) Send(other Data) Data { return other }
`, nil)
		review, err := createReview(dir)
		require.NoError(t, err)
		line := feedbackLine(t, review, "aliastest.Public")
		method := feedbackMethod(t, line, "Public", "Send")
		require.Equal(t, "func(Public)Send(otherPublic)Public", feedbackLineText(method))
	})
	t.Run("unicode receiver", func(t *testing.T) {
		dir := aliasFeedbackFixture(t, "type Public = types.\u00c9tat\n", "type \u00c9tat []byte\nfunc (\u00c9tat) Method(v \u00c9tat) \u00c9tat { return v }\n", nil)
		review, err := createReview(dir)
		require.NoError(t, err)
		method := feedbackMethod(t, feedbackLine(t, review, "aliastest.Public"), "Public", "Method")
		require.Equal(t, "func(Public)Method(vPublic)Public", feedbackLineText(method))
	})

	for _, test := range []struct {
		name, original, changed, originalMember, changedMember string
	}{
		{"struct", "(struct { Field int })", "(struct { Field string })", "int", "string"},
		{"interface", "(interface { Value() int })", "(interface { Value() string })", "int", "string"},
	} {
		t.Run("parenthesized "+test.name, func(t *testing.T) {
			dir := aliasFeedbackFixture(t, "type Public = types.Data\n", "type Data "+test.original+"\n", nil)
			review, err := createReview(dir)
			require.NoError(t, err)
			line := feedbackLine(t, review, "aliastest.Public")
			require.True(t, searchTokens(line.Children, func(token ReviewToken) bool { return token.Value == test.originalMember }))

			source := filepath.Join(dir, "internal", "types", "types.go")
			require.NoError(t, os.WriteFile(source, []byte("package types\ntype Data "+test.changed+"\n"), 0644))
			review, err = createReview(dir)
			require.NoError(t, err)
			line = feedbackLine(t, review, "aliastest.Public")
			require.True(t, searchTokens(line.Children, func(token ReviewToken) bool { return token.Value == test.changedMember }))
		})
	}
}

func TestAliasPrivateIntermediate(t *testing.T) {
	dir := aliasFeedbackFixture(t, `
type hidden = types.Data
type Public = hidden
type PublicTwo = Public
type privateDefined types.Data
func (privateDefined) Local() {}
func (d (*privateDefined)) PrivatePointer() {}
type Exposed = privateDefined
type Direct hidden
func (Direct) Own() {}
`, `
type Data []byte
func (Data) Unnamed() {}
func (d Data) Named() {}
`, nil)
	review, err := createReview(dir)
	require.NoError(t, err)
	line := feedbackLine(t, review, "aliastest.Public")
	require.Equal(t, "typePublic=[]byte", withoutAliasTarget(feedbackLineText(line)))
	for _, method := range []string{"Named", "Unnamed"} {
		feedbackMethod(t, line, "Public", method)
		feedbackMethod(t, feedbackLine(t, review, "aliastest.PublicTwo"), "PublicTwo", method)
	}
	exposed := feedbackLine(t, review, "aliastest.Exposed")
	feedbackMethod(t, exposed, "Exposed", "Local")
	feedbackMethod(t, exposed, "Exposed", "PrivatePointer")
	require.False(t, searchLines(exposed.Children, func(child ReviewLine) bool {
		return strings.Contains(feedbackLineText(child), "Exposed)Named(")
	}))
	direct := feedbackLine(t, review, "aliastest.Direct")
	require.Equal(t, "typeDirect[]byte", withoutAliasTarget(feedbackLineText(direct)))
	feedbackMethod(t, direct, "Direct", "Own")
	require.False(t, searchLines(direct.Children, func(child ReviewLine) bool {
		return strings.Contains(feedbackLineText(child), "Direct)Named(")
	}))
	require.False(t, searchLines(review.ReviewLines, func(line ReviewLine) bool {
		return strings.Contains(line.LineID, "hidden") || strings.Contains(line.LineID, "privateDefined")
	}))
	for _, diagnostic := range review.Diagnostics {
		require.NotEqual(t, "aliastest.hidden", diagnostic.TargetID)
		require.NotEqual(t, "aliastest.privateDefined", diagnostic.TargetID)
	}
	lineIDs := map[string]bool{}
	forAll(review.ReviewLines, func(line ReviewLine) {
		lineIDs[line.LineID] = true
	})
	for _, namespace := range review.Navigation {
		for _, item := range namespace.ChildItems {
			require.NotContains(t, item.NavigationID, "hidden")
			require.NotContains(t, item.NavigationID, "privateDefined")
			require.True(t, lineIDs[item.NavigationID], "navigation target %s has no review line", item.NavigationID)
		}
	}
}

func TestAliasCyclicDefinitions(t *testing.T) {
	dir := aliasFeedbackFixture(t, "type Public = types.A\n", "type A = B\ntype B = A\n", nil)
	_, err := createReview(dir)
	require.ErrorContains(t, err, "cyclic type definition")
}

func TestAliasMethodChainsAndSelfReferences(t *testing.T) {
	dir := aliasFeedbackFixture(t, `
type Slice = (types.Renamed)
type Another = types.Data
type Different = types.DefinedAlias
`, `
import "example.com/aliastest/other"
type Data []byte
type Middle = Data
type hidden = Middle
type Renamed = (hidden)
type Sibling = Data
func (Data) Len() int { return 0 }
func (Middle) ViaMiddle() {}
func (Sibling) ViaSibling() {}
func (d (*Data)) Wrapped(v Data) Data { return v }
func (d *(Data)) Nested() {}
func (Data) Clone(v *Data, all []Data, lookup map[string][]*Renamed, foreign other.Data, extra other.DataExtra) (Renamed, *Data) {
	return nil, nil
}
type Defined Data
type DefinedAlias = Defined
func (Defined) Own(other other.Data) Defined { return nil }
func (DefinedAlias) AliasOwn() {}
`, map[string]string{
		filepath.Join("other", "other.go"): "package other\ntype Data string\ntype DataExtra int\n",
	})
	review, err := createReview(dir)
	require.NoError(t, err)
	for _, name := range []string{"Slice", "Another"} {
		t.Run(name, func(t *testing.T) {
			line := feedbackLine(t, review, "aliastest."+name)
			require.Equal(t, "type"+name+"=[]byte", withoutAliasTarget(feedbackLineText(line)))
			for _, method := range []string{"Len", "ViaMiddle", "ViaSibling", "Wrapped", "Nested"} {
				feedbackMethod(t, line, name, method)
			}
			require.Contains(t, feedbackLineText(feedbackMethod(t, line, name, "Wrapped")), "func(*"+name+")Wrapped(v"+name+")"+name)
			clone := feedbackMethod(t, line, name, "Clone")
			require.Contains(t, feedbackLineText(clone), "foreignother.Data")
			require.Contains(t, feedbackLineText(clone), "extraother.DataExtra")
			require.NotContains(t, feedbackLineText(clone), "Renamed")
			selfReferences := 0
			foreignReference := false
			for _, token := range clone.Tokens {
				if token.Value == name && token.NavigateToID == "aliastest."+name {
					selfReferences++
				}
				if token.Value == "other.Data" && token.NavigateToID == "aliastest/other.Data" {
					foreignReference = true
				}
			}
			require.Equal(t, 5, selfReferences, "parameter and result types must navigate to the public alias")
			require.True(t, foreignReference, "an unrelated type with the same name must not be rewritten")
		})
	}
	line := feedbackLine(t, review, "aliastest.Different")
	require.Equal(t, "typeDifferent=[]byte", withoutAliasTarget(feedbackLineText(line)))
	own := feedbackMethod(t, line, "Different", "Own")
	require.Contains(t, feedbackLineText(own), "otherother.Data")
	require.True(t, searchTokens([]ReviewLine{own}, func(token ReviewToken) bool {
		return token.Value == "Different" && token.NavigateToID == "aliastest.Different"
	}))
	feedbackMethod(t, line, "Different", "AliasOwn")
	for _, method := range []string{"Len", "ViaMiddle", "ViaSibling", "Wrapped", "Nested", "Clone"} {
		require.False(t, searchLines(line.Children, func(child ReviewLine) bool {
			return strings.Contains(feedbackLineText(child), "Different)"+method+"(")
		}), "a newly defined type must not inherit %s", method)
	}
}

func aliasFeedbackFixture(t *testing.T, aliases, definitions string, extra map[string]string) string {
	t.Helper()
	dir := t.TempDir()
	files := map[string]string{
		"go.mod":   "module example.com/aliastest\n\ngo 1.21\n",
		"alias.go": "package aliastest\nimport \"example.com/aliastest/internal/types\"\n" + aliases,
		filepath.Join("internal", "types", "types.go"): "package types\n" + definitions,
	}
	for name, content := range extra {
		files[name] = content
	}
	for name, content := range files {
		p := filepath.Join(dir, name)
		require.NoError(t, os.MkdirAll(filepath.Dir(p), 0755))
		require.NoError(t, os.WriteFile(p, []byte(content), 0644))
	}
	return dir
}

func feedbackLine(t *testing.T, review CodeFile, id string) ReviewLine {
	t.Helper()
	var found ReviewLine
	forAll(review.ReviewLines, func(line ReviewLine) {
		if line.LineID == id {
			found = line
		}
	})
	require.Equal(t, id, found.LineID, "missing definition")
	return found
}

func feedbackLineText(line ReviewLine) string {
	var text strings.Builder
	for _, token := range line.Tokens {
		text.WriteString(token.Value)
	}
	return text.String()
}

func withoutAliasTarget(text string) string {
	if i := strings.Index(text, "/* alias of "); i >= 0 {
		return text[:i]
	}
	return text
}

func feedbackMethod(t *testing.T, line ReviewLine, receiver, name string) ReviewLine {
	t.Helper()
	for _, child := range line.Children {
		if strings.Contains(feedbackLineText(child), receiver+")"+name+"(") {
			return child
		}
	}
	t.Fatalf("missing method %s on %s", name, receiver)
	return ReviewLine{}
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
			require.Equal(t, "1.2.3", review.PackageVersion)
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
	require.Equal(t, "2.0.1", review.PackageVersion)
	require.Equal(t, "test_major_version", review.Name)
	require.Equal(t, 2, len(review.Navigation))
	require.Equal(t, "test_major_version", review.Navigation[0].Text)
	require.Equal(t, "test_major_version/subpackage", review.Navigation[1].Text)
}

func TestVars(t *testing.T) {
	review, err := createReview(filepath.Clean("testdata/test_vars"))
	require.NoError(t, err)
	require.Empty(t, review.PackageVersion)
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
