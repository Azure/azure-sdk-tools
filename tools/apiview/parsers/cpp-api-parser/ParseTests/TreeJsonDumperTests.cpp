// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#include "ApiViewProcessor.hpp"
#include "JsonDumper.hpp"
#include "TreeJsonDumper.hpp"
#include "gtest/gtest.h"
#include <algorithm>
#include <functional>
#include <sstream>

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
  Walk(json["ReviewLines"], [&](auto const& line) {
    if (line.contains("LineId") && line["LineId"] != "Azure" && line["LineId"] != "Azure::TreeTest")
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
  EXPECT_GT(sourceLinks, 0);
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
    EXPECT_NE(std::find(treeIds.begin(), treeIds.end(), target), treeIds.end()) << target;
  }
  EXPECT_EQ(protectedWarnings, 1);
  EXPECT_EQ(overloadWarnings, 2);
  EXPECT_EQ(templateWarnings, 1);
  EXPECT_EQ(tree.GetJson(), json);
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
