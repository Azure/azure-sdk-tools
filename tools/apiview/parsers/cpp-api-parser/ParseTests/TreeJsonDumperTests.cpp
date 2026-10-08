// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#include "ApiViewProcessor.hpp"
#include "JsonDumper.hpp"
#include "TextDumper.hpp"
#include "TreeJsonDumper.hpp"
#include "gtest/gtest.h"
#include <algorithm>
#include <functional>
#include <sstream>
#include <unordered_set>

using namespace nlohmann::literals;

namespace {
void Walk(nlohmann::json const& lines, std::function<void(nlohmann::json const&)> const& visit)
{
  for (auto const& line : lines)
  {
    visit(line);
    if (line.contains("Children"))
    {
      Walk(line["Children"], visit);
    }
  }
}

std::string Text(nlohmann::json const& line)
{
  std::string text;
  for (auto const& token : line["Tokens"])
  {
    text += token["Value"].get<std::string>();
  }
  return text;
}

void ExpectNavigationTargetsResolve(nlohmann::json const& json)
{
  std::unordered_set<std::string> ids;
  Walk(json["ReviewLines"], [&](auto const& line) {
    if (line.contains("LineId"))
    {
      EXPECT_TRUE(ids.emplace(line["LineId"].template get<std::string>()).second);
    }
  });
  Walk(json["ReviewLines"], [&](auto const& line) {
    for (auto const& token : line["Tokens"])
    {
      auto id = token.value("NavigateToId", "");
      if (!id.empty() && token["Kind"] != 8)
      {
        EXPECT_TRUE(ids.contains(id)) << id;
      }
    }
  });
  std::function<void(nlohmann::json const&)> visit = [&](auto const& items) {
    for (auto const& item : items)
    {
      auto id = item.value("NavigationId", "");
      if (!id.empty())
      {
        EXPECT_TRUE(ids.contains(id)) << id;
      }
      visit(item["ChildItems"]);
    }
  };
  visit(json["Navigation"]);
}
} // namespace

TEST(TreeJsonDumper, SemanticHierarchyAndLegacyCommentIds)
{
  ApiViewProcessor processor("tests", R"({
    "sourceFilesToProcess": ["TreeFormat.hpp"],
    "sourceRootUrl": "https://example.test/sdk",
    "filterNamespace": "Azure::TreeTest::Widget"
  })"_json);
  ASSERT_EQ(processor.ProcessApiView(), 0);
  auto& db = processor.GetClassesDatabase();
  JsonDumper legacy("Review", "Storage", "test-package", "1.2.3");
  TreeJsonDumper tree("Review", "Storage", "test-package", "1.2.3");
  db->DumpClassDatabase(&legacy);
  db->DumpClassDatabase(&tree);
  auto json = tree.GetJson();
  ExpectNavigationTargetsResolve(json);
  EXPECT_FALSE(json.contains("Tokens"));
  EXPECT_EQ(json["ParserVersion"], "1.0.0");
  EXPECT_EQ(json["PackageVersion"], "1.2.3");

  std::vector<std::string> legacyIds, treeIds;
  std::string currentId;
  for (auto const& token : legacy.GetJson()["Tokens"])
  {
    if (token["Kind"] == 1)
    {
      if (!currentId.empty())
      {
        legacyIds.push_back(currentId);
      }
      currentId.clear();
    }
    else if (token["DefinitionId"].is_string())
    {
      currentId = token["DefinitionId"];
    }
  }
  size_t documentationTokens{}, sourceLinks{}, contexts{};
  std::unordered_set<std::string> allLineIds;
  Walk(json["ReviewLines"], [&](auto const& line) {
    if (line.contains("LineId"))
    {
      allLineIds.emplace(line["LineId"].template get<std::string>());
    }
    if (line.contains("LineId")
        && !line["LineId"].template get<std::string>().starts_with("#"))
    {
      treeIds.push_back(line["LineId"]);
    }
    for (auto const& token : line["Tokens"])
    {
      EXPECT_TRUE(token["Value"].is_string());
      EXPECT_GE(token["Kind"].template get<int>(), 0);
      EXPECT_LE(token["Kind"].template get<int>(), 8);
      EXPECT_EQ(token["HasSuffixSpace"], false);
      documentationTokens += token.value("IsDocumentation", false);
      sourceLinks += token["Kind"] == 8 && token.value("SkipDiff", false);
    }
    contexts += line.value("IsContextEndLine", false);
    if (line.value("LineId", "") == "Azure::TreeTest::Widget")
    {
      ASSERT_TRUE(line.contains("Children"));
      EXPECT_EQ(Text(line), "class Widget {");
      bool methodFound = false, nestedFound = false, enumFound = false;
      for (auto const& child : line["Children"])
      {
        auto text = Text(child);
        methodFound |= text == "int GetValue(int input, int other) const;";
        nestedFound |= text == "struct Nested {";
        enumFound |= text.starts_with("enum class Mode") && text.ends_with(" {");
      }
      EXPECT_TRUE(methodFound);
      EXPECT_TRUE(nestedFound);
      EXPECT_TRUE(enumFound);
    }
  });
  EXPECT_EQ(legacyIds, treeIds);
  EXPECT_GT(documentationTokens, 0);
  EXPECT_EQ(sourceLinks, 0);
  EXPECT_GT(contexts, 0);
  size_t protectedWarnings{}, overloadWarnings{}, templateWarnings{};
  for (auto const& diagnostic : json["Diagnostics"])
  {
    auto target = diagnostic["TargetId"].get<std::string>();
    if (diagnostic["DiagnosticId"] == "CPA0006")
    {
      ++protectedWarnings;
      EXPECT_NE(diagnostic["TargetId"], "Azure::TreeTest::Final::ShouldRetry");
    }
    overloadWarnings += target.find("Azure::TreeTest::Overloaded(") != std::string::npos;
    templateWarnings += target.find("Azure::TreeTest::Identity(") != std::string::npos;
    EXPECT_TRUE(allLineIds.contains(target)) << target;
  }
  EXPECT_EQ(protectedWarnings, 1);
  EXPECT_EQ(overloadWarnings, 2);
  EXPECT_EQ(templateWarnings, 1);
  EXPECT_EQ(tree.GetJson(), json);
}

TEST(TreeJsonDumper, SourceCommentsOnlyOmittedFromTree)
{
  for (bool withSourceUrl : {false, true})
  {
    SCOPED_TRACE(withSourceUrl);
    auto settings = R"({
      "sourceFilesToProcess": ["TreeFormat.hpp"],
      "filterNamespace": "Azure::TreeTest::Widget"
    })"_json;
    if (withSourceUrl)
    {
      settings["sourceRootUrl"] = "https://example.test/sdk";
    }
    ApiViewProcessor processor("tests", settings);
    ASSERT_EQ(processor.ProcessApiView(), 0);
    auto& db = processor.GetClassesDatabase();
    JsonDumper legacy("Review", "Storage", "test");
    TreeJsonDumper tree("Review", "Storage", "test");
    std::ostringstream text;
    TextDumper console(text);
    db->DumpClassDatabase(&legacy);
    db->DumpClassDatabase(&tree);
    db->DumpClassDatabase(&console);
    EXPECT_NE(text.str().find("TreeFormat.hpp:"), std::string::npos);
    std::vector<std::string> legacyValues, treeValues;
    bool sourceRange = false;
    size_t sourceComments{}, sourceLinks{}, documentationLinks{};
    for (auto const& token : legacy.GetJson()["Tokens"])
    {
      auto kind = token["Kind"].get<int>();
      if (kind == 15)
      {
        sourceRange = true;
      }
      else if (kind == 16)
      {
        sourceRange = false;
      }
      else if (sourceRange)
      {
        sourceComments += kind == 10
            && token["Value"].get<std::string>().find("TreeFormat.hpp:") != std::string::npos;
        sourceLinks += kind == 29;
      }
      else if (
          kind == 0 || kind == 3 || kind == 4 || kind == 6 || kind == 7 || kind == 8 || kind == 9
          || kind == 10)
      {
        auto value = token["Value"].get<std::string>();
        if (value.find_first_not_of(" \t\r\n") != std::string::npos)
        {
          legacyValues.push_back(value);
        }
      }
    }
    EXPECT_FALSE(sourceRange);
    EXPECT_GT(sourceComments, 0);
    EXPECT_EQ(sourceLinks > 0, withSourceUrl);
    auto json = tree.GetJson();
    ExpectNavigationTargetsResolve(json);
    Walk(json["ReviewLines"], [&](auto const& line) {
      EXPECT_EQ(Text(line).find("TreeFormat.hpp:"), std::string::npos);
      for (auto const& token : line["Tokens"])
      {
        EXPECT_FALSE(token.value("SkipDiff", false));
        auto value = token["Value"].get<std::string>();
        if (value.find_first_not_of(" \t\r\n") != std::string::npos)
        {
          treeValues.push_back(value);
        }
        documentationLinks += token["Kind"] == 8 && token.value("IsDocumentation", false)
            && token.value("NavigateToId", "") == "https://example.test/docs";
      }
    });
    EXPECT_EQ(treeValues, legacyValues);
    EXPECT_GT(documentationLinks, 0);
  }
}

TEST(TreeJsonDumper, UsingNamespacePreservesDefinitionReferencesAndDiagnostic)
{
  ApiViewProcessor processor("tests", R"({
    "sourceFilesToProcess": ["UsingNamespace.cpp"]
  })"_json);
  ASSERT_EQ(processor.ProcessApiView(), 0);
  auto& db = processor.GetClassesDatabase();
  JsonDumper legacy("Review", "Storage", "test");
  TreeJsonDumper tree("Review", "Storage", "test");
  db->DumpClassDatabase(&legacy);
  std::ostringstream serializedLegacy;
  legacy.DumpToFile(serializedLegacy);
  db->DumpClassDatabase(&tree);
  auto json = tree.GetJson();
  ExpectNavigationTargetsResolve(json);
  bool directiveFound = false;
  Walk(json["ReviewLines"], [&](auto const& line) {
    if (Text(line) == "using namespace Test::Inner;")
    {
      directiveFound = true;
      EXPECT_EQ(line["LineId"], "Test::Inner");
      for (auto const& token : line["Tokens"])
      {
        if (token["Value"] == "Test::Inner")
        {
          EXPECT_EQ(token["NavigateToId"], "Test::Inner");
        }
      }
    }
  });
  EXPECT_TRUE(directiveFound);
  bool diagnosticFound = false;
  for (auto const& diagnostic : json["Diagnostics"])
  {
    if (diagnostic["DiagnosticId"] == "CPA000A")
    {
      diagnosticFound = true;
      EXPECT_EQ(diagnostic["TargetId"], "Test::Inner");
    }
  }
  EXPECT_TRUE(diagnosticFound);
  EXPECT_EQ(json["Diagnostics"], legacy.GetJson()["Diagnostics"]);
}

TEST(TreeJsonDumper, RepeatedNamespaceScopesHaveDistinctIdsAndResolvableAliases)
{
  TreeJsonDumper tree("Review", "Storage", "test");
  for (int scope = 0; scope != 2; ++scope)
  {
    tree.SetNamespace("Test::Inner");
    tree.SetNamespace("");
  }
  auto node = std::make_shared<TypeHierarchy::TypeHierarchyNode>(
      "Inner", "Test::Inner", TypeHierarchy::TypeHierarchyClass::Namespace);
  tree.DumpTypeHierarchyNode(node);
  auto json = tree.GetJson();
  ExpectNavigationTargetsResolve(json);
  std::unordered_set<std::string> ids;
  Walk(json["ReviewLines"], [&](auto const& line) {
    if (line.contains("LineId"))
    {
      EXPECT_TRUE(ids.emplace(line["LineId"].template get<std::string>()).second);
    }
    if (line.contains("RelatedToLine"))
    {
      EXPECT_TRUE(ids.contains(line["RelatedToLine"].template get<std::string>()));
    }
  });
  EXPECT_EQ(ids.size(), 4);
  tree.InsertTypeName("Test::Inner", "Test::Inner");
  tree.Newline();
  json = tree.GetJson();
  ExpectNavigationTargetsResolve(json);
  EXPECT_EQ(json["Navigation"][0]["NavigationId"], "Test::Inner");
  EXPECT_EQ(json["ReviewLines"].back()["LineId"], "Test::Inner");
  tree.SetNamespace("Test::Inner");
  tree.SetNamespace("");
  json = tree.GetJson();
  ExpectNavigationTargetsResolve(json);
  EXPECT_EQ(json["Navigation"][0]["NavigationId"], "Test::Inner");
}

TEST(TreeJsonDumper, ForwardDeclarationsRemainNavigableWithAndWithoutDefinitions)
{
  ApiViewProcessor processor("tests", R"({
    "sourceFilesToProcess": ["TreeFormat.hpp"]
  })"_json);
  ASSERT_EQ(processor.ProcessApiView(), 0);
  TreeJsonDumper tree("Review", "Storage", "test");
  processor.GetClassesDatabase()->DumpClassDatabase(&tree);
  auto json = tree.GetJson();
  ExpectNavigationTargetsResolve(json);
  bool forwardFound = false, definedFound = false, enumForwardFound = false;
  Walk(json["ReviewLines"], [&](auto const& line) {
    auto text = Text(line);
    if (text == "class Forward;")
    {
      forwardFound = true;
      EXPECT_TRUE(line.contains("LineId"));
      for (auto const& token : line["Tokens"])
      {
        if (token["Value"] == "Forward")
        {
          EXPECT_EQ(token["NavigateToId"], line["LineId"]);
        }
      }
    }
    if (text == "class Defined {")
    {
      definedFound = true;
      EXPECT_EQ(line["LineId"], "Azure::TreeTest::Defined");
    }
    if (text == "enum class ForwardEnum;")
    {
      enumForwardFound = true;
      EXPECT_TRUE(line.contains("LineId"));
    }
  });
  EXPECT_TRUE(forwardFound);
  EXPECT_TRUE(definedFound);
  EXPECT_TRUE(enumForwardFound);
}

TEST(TreeJsonDumper, ForwardAliasesPreferDefinitionsInEitherOrder)
{
  for (bool definitionFirst : {false, true})
  {
    TreeJsonDumper tree("Review", "Storage", "test");
    auto definition = [&]() {
      tree.InsertTypeName("Defined", "Test::Defined");
      tree.Newline();
    };
    if (definitionFirst)
    {
      definition();
    }
    for (int declaration = 0; declaration != 2; ++declaration)
    {
      tree.InsertForwardDeclaration("Defined", "Test::Defined");
      tree.Newline();
    }
    if (!definitionFirst)
    {
      definition();
    }
    auto node = std::make_shared<TypeHierarchy::TypeHierarchyNode>(
        "Defined", "Test::Defined", TypeHierarchy::TypeHierarchyClass::Class);
    tree.DumpTypeHierarchyNode(node);
    auto json = tree.GetJson();
    ExpectNavigationTargetsResolve(json);
    EXPECT_EQ(json["Navigation"][0]["NavigationId"], "Test::Defined");
    for (auto const& line : json["ReviewLines"])
    {
      EXPECT_EQ(line["Tokens"][0]["NavigateToId"], "Test::Defined");
    }
  }
}

TEST(TreeJsonDumper, EnumUnderlyingTypesNavigationAndDiagnosticsResolve)
{
  ApiViewProcessor processor("tests", R"({
    "sourceFilesToProcess": ["EnumUnderlyingTypes.cpp"],
    "filterNamespace": ["Byte", "Word"]
  })"_json);
  ASSERT_EQ(processor.ProcessApiView(), 0);
  JsonDumper legacy("Review", "Test", "test");
  TreeJsonDumper tree("Review", "Test", "test");
  auto& db = processor.GetClassesDatabase();
  db->DumpClassDatabase(&legacy);
  db->DumpClassDatabase(&tree);
  auto json = tree.GetJson();
  ExpectNavigationTargetsResolve(json);
  std::unordered_set<std::string> ids;
  Walk(json["ReviewLines"], [&](auto const& line) {
    if (line.contains("LineId"))
    {
      ids.emplace(line["LineId"].template get<std::string>());
    }
  });
  auto const& legacyDiagnostics = legacy.GetJson()["Diagnostics"];
  ASSERT_FALSE(legacyDiagnostics.empty());
  ASSERT_EQ(json["Diagnostics"].size(), legacyDiagnostics.size());
  for (size_t index = 0; index != legacyDiagnostics.size(); ++index)
  {
    auto expected = legacyDiagnostics[index];
    auto const& diagnostic = json["Diagnostics"][index];
    auto target = diagnostic["TargetId"].get<std::string>();
    EXPECT_TRUE(ids.contains(target)) << target;
    EXPECT_NE(target.find(expected["TargetId"].get<std::string>()), std::string::npos);
    expected["TargetId"] = target;
    EXPECT_EQ(diagnostic, expected);
  }
}

TEST(TreeJsonDumper, RangeFlagsSpacingAndDiagnosticTargets)
{
  TreeJsonDumper tree("Review", "Storage", "test");
  tree.AddDeprecatedRangeStart();
  tree.InsertTypeName("Function", "function");
  tree.InsertPunctuation('(');
  tree.InsertMemberName("first", "first");
  tree.InsertPunctuation(',');
  tree.InsertWhitespace(2);
  tree.InsertMemberName("last", "last");
  tree.InsertPunctuation(')');
  tree.AddDeprecatedRangeEnd();
  tree.Newline();
  tree.AddSkipDiffRangeStart();
  tree.InsertComment("// source ");
  tree.AddExternalLinkStart("https://example.test/header.hpp#L10");
  tree.InsertText("header.hpp:10");
  tree.AddExternalLinkEnd();
  tree.AddSkipDiffRangeEnd();
  tree.Newline();
  ApiViewMessage message;
  message.DiagnosticId = "CPA000C";
  message.TargetId = "first";
  message.DiagnosticText = "Parameter warning";
  message.Level = ApiViewMessage::MessageLevel::Warning;
  tree.DumpMessageNode(message);
  auto json = tree.GetJson();
  EXPECT_EQ(json["ReviewLines"][0]["LineId"], "last");
  EXPECT_EQ(Text(json["ReviewLines"][0]), "Function(first,  last)");
  EXPECT_EQ(json["Diagnostics"][0]["DiagnosticId"], "CPA000C");
  EXPECT_EQ(json["Diagnostics"][0]["TargetId"], "last");
  EXPECT_EQ(json["ReviewLines"][0]["Tokens"][0]["NavigateToId"], "last");
  for (auto const& token : json["ReviewLines"][0]["Tokens"])
  {
    EXPECT_EQ(token["IsDeprecated"], true);
  }
  auto const& link = json["ReviewLines"][1]["Tokens"][1];
  EXPECT_EQ(link["Kind"], 8);
  EXPECT_EQ(link["Value"], "header.hpp:10");
  EXPECT_EQ(link["NavigateToId"], "https://example.test/header.hpp#L10");
  EXPECT_EQ(link["SkipDiff"], true);
}

TEST(TreeJsonDumper, RejectsMalformedScopesRangesAndDuplicateIds)
{
  TreeJsonDumper tree("Review", "Storage", "test");
  EXPECT_THROW(tree.EndChildScope(), std::runtime_error);
  EXPECT_THROW(tree.AddDocumentRangeEnd(), std::runtime_error);
  tree.InsertTypeName("Widget", "Widget");
  EXPECT_THROW(tree.InsertTypeName("Widget", "Widget"), std::runtime_error);
  tree.Newline();
  tree.BeginChildScope("", false);
  EXPECT_THROW(tree.GetJson(), std::runtime_error);
}

TEST(TreeJsonDumper, PreservesContinuationAlignmentBeyondTreeIndentation)
{
  TreeJsonDumper tree("Review", "Storage", "test");
  tree.InsertTypeName("Widget", "Widget");
  tree.Newline();
  tree.BeginChildScope("", false);
  tree.InsertWhitespace(6);
  tree.InsertKeyword("int");
  tree.InsertWhitespace(1);
  tree.InsertMemberName("value", "Widget::value");
  tree.Newline();
  tree.EndChildScope();
  tree.InsertPunctuation('}');
  tree.Newline();
  EXPECT_EQ(Text(tree.GetJson()["ReviewLines"][0]["Children"][0]), "    int value");
}
