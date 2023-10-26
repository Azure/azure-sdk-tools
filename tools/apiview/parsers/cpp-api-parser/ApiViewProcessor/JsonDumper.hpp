// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#pragma once
#include "AstDumper.hpp"
#include <iostream>
#include <nlohmann/json.hpp>
#include <string>
#include <string_view>
#include <unordered_set>

using namespace nlohmann::literals;

class JsonDumper : public AstDumper {
  nlohmann::json m_json;

  enum class TokenKinds
  {
    Text = 0,
    Newline = 1,
    Whitespace = 2,
    Punctuation = 3,
    Keyword = 4,
    LineIdMarker = 5, // use this if there are no visible tokens with ID on the line but you still
                      // want to be able to leave a comment for it
    TypeName = 6,
    MemberName = 7,
    StringLiteral = 8,
    Literal = 9,
    Comment = 10,
    DocumentRangeStart = 11,
    DocumentRangeEnd = 12,
    DeprecatedRangeStart = 13,
    DeprecatedRangeEnd = 14,
    SkipDiffRangeStart = 15,
    SkipDiffRangeEnd = 16,
    FoldableSectionHeading = 17,
    FoldableSectionContentStart = 18,
    FoldableSectionContentEnd = 19,
    TableBegin = 20,
    TableEnd = 21,
    TableRowCount = 22,
    TableColumnCount = 23,
    TableColumnName = 24,
    TableCellBegin = 25,
    TableCellEnd = 26,
    LeafSectionPlaceholder = 27,
    ExternalLinkStart = 28,
    ExternalLinkEnd = 29,
    HiddenApiRangeStart = 30,
    HiddenApiRangeEnd = 31
  };

  // Validate that the json we've created won't cause problems for ApiView.
  void ValidateJson()
  {
    std::unordered_set<std::string> definitions;
    for (const auto& token : m_json["Tokens"])
    {
      if (token.contains("DefinitionId") && token["DefinitionId"].is_string())
      {
        auto definitionId = token["DefinitionId"].get<std::string>();
        if (definitions.find(definitionId) != definitions.end())
        {
          throw std::runtime_error("Duplicate DefinitionId: " + definitionId);
        }
        definitions.emplace(definitionId);
      }
      if (!token.contains("Value"))
      {
        throw std::runtime_error("Missing Value in token");
      }
      if (!token.contains("Kind"))
      {
        throw std::runtime_error("Missing Kind in token");
      }
    }
  }

public:
  JsonDumper(
      std::string_view reviewName,
      std::string_view serviceName,
      std::string_view packageName,
      std::string_view packageVersion = "")
      : AstDumper()
  {
    m_json["Name"] = reviewName;
    m_json["Language"] = "C++";
    m_json["ServiceName"] = serviceName;
    m_json["PackageName"] = packageName;
    if (!packageVersion.empty())
    {
      m_json["PackageVersion"] = packageVersion;
    }
    m_json["Tokens"] = nlohmann::json::array();
  }

  void DumpToFile(std::ostream& outfile)
  {
    ValidateJson();
    outfile << m_json;
  }
  nlohmann::json const& GetJson() { return m_json; }

  // Each ApiView node has 4 mandatory members:
  //
  // DefinitionId: A unique value used to represent an entity where comments can be left. This MUST be unique.
  // NavigateToId: ID used in the Navigation pane for type navigation.
  // Value: Value to display in ApiView (mandatory)
  // Kind: Type of node, used for color coding output.

  virtual void InsertWhitespace(int count) override
  {
    std::string whiteSpace(count, ' ');
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", whiteSpace},
         {"Kind", TokenKinds::Whitespace}});
    UpdateCursor(count);
  }
  virtual void InsertNewline() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", nullptr},
         {"Kind", TokenKinds::Newline}});
  }
  virtual void InsertKeyword(std::string_view const& keyword) override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", keyword},
         {"Kind", TokenKinds::Keyword}});
    UpdateCursor(keyword.size());
  }
  virtual void InsertText(std::string_view const& text) override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", text},
         {"Kind", TokenKinds::Text}});
    UpdateCursor(text.size());
  }
  virtual void InsertPunctuation(const char punctuation) override
  {
    std::string punctuationString{punctuation};
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", punctuationString},
         {"Kind", TokenKinds::Punctuation}});
    UpdateCursor(1);
  }
  virtual void InsertLineIdMarker() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", nullptr},
         {"Kind", TokenKinds::LineIdMarker}}); // Not clear if this is used at all.
  }
  virtual void InsertIdentifier(std::string_view const& id) override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", id},
         {"Kind", TokenKinds::TypeName}});
    UpdateCursor(id.size());
  }
  virtual void InsertTypeName(std::string_view const& type, std::string_view const& navigationId)
      override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", navigationId},
         {"NavigateToId", navigationId},
         {"Value", type},
         {"Kind", TokenKinds::TypeName}});
    UpdateCursor(type.size());
  }
  virtual void InsertMemberName(
      std::string_view const& member,
      std::string_view const& memberFullName) override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", memberFullName},
         {"NavigateToId", nullptr},
         {"Value", member},
         {"Kind", TokenKinds::MemberName}});
  }
  virtual void InsertStringLiteral(std::string_view const& str) override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", str},
         {"Kind", TokenKinds::StringLiteral}});
    UpdateCursor(str.size());
  }
  virtual void InsertLiteral(std::string_view const& str) override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", str},
         {"Kind", TokenKinds::Literal}});
    UpdateCursor(str.size());
  }
  virtual void InsertComment(std::string_view const& comment) override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", comment},
         {"Kind", TokenKinds::Comment}});
    UpdateCursor(comment.size());
  }
  virtual void AddDocumentRangeStart() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", nullptr},
         {"Kind", TokenKinds::DocumentRangeStart}});
  }
  virtual void AddDocumentRangeEnd() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", nullptr},
         {"Kind", TokenKinds::DocumentRangeEnd}});
  }
  virtual void AddExternalLinkStart(const std::string_view& url) override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", url},
         {"Kind", TokenKinds::ExternalLinkEnd}});

  }
  virtual void AddExternalLinkEnd()
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", nullptr},
         {"Kind", TokenKinds::ExternalLinkStart}});
  }
  virtual void AddDeprecatedRangeStart() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", nullptr},
         {"Kind", TokenKinds::DeprecatedRangeStart}});
  }
  virtual void AddDeprecatedRangeEnd() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", nullptr},
         {"Kind", TokenKinds::DeprecatedRangeEnd}});
  }
  virtual void AddDiffRangeStart() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", nullptr},
         {"Kind", TokenKinds::SkipDiffRangeStart}});
  }
  virtual void AddDiffRangeEnd() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", nullptr},
         {"Kind", TokenKinds::SkipDiffRangeEnd}});
  }

  nlohmann::json DoDumpTypeHierarchyNode(
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> const& node)
  {
    nlohmann::json newNode;
    newNode["Text"] = node->NodeName;
    newNode["NavigationId"] = node->NavigationId;
    for (auto const& [childName, child] : node->Children)
    {
      newNode["ChildItems"].push_back(DoDumpTypeHierarchyNode(child));
    }
    switch (node->NodeClass)
    {
      case TypeHierarchy::TypeHierarchyClass::Class:
        newNode["Tags"]["TypeKind"] = "class";
        break;
      case TypeHierarchy::TypeHierarchyClass::Assembly:
        newNode["Tags"]["TypeKind"] = "assembly";
        break;
      case TypeHierarchy::TypeHierarchyClass::Delegate:
        newNode["Tags"]["TypeKind"] = "delegate";
        break;
      case TypeHierarchy::TypeHierarchyClass::Enum:
        newNode["Tags"]["TypeKind"] = "enum";
        break;
      case TypeHierarchy::TypeHierarchyClass::Interface:
        newNode["Tags"]["TypeKind"] = "interface";
        break;
      case TypeHierarchy::TypeHierarchyClass::Struct:
        newNode["Tags"]["TypeKind"] = "struct";
        break;
      case TypeHierarchy::TypeHierarchyClass::Namespace:
        newNode["Tags"]["TypeKind"] = "namespace";
        break;
      case TypeHierarchy::TypeHierarchyClass::Unknown:
        newNode["Tags"]["TypeKind"] = "unknown";
        break;
    }
    return newNode;
  }

  virtual void DumpTypeHierarchyNode(
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> const& node) override
  {
    m_json["Navigation"].push_back(DoDumpTypeHierarchyNode(node));
  };

  // Schema for diagnostic nodes (which live under the "Diagnostics" root node:
  //  DiagnosticId:<Unique ID>.
  //  Text: <Diagnostic message>,
  //  HelpLinkUri: <Any URL to be listed on diagnostic.> OPTIONAL.
  //  TargetId: <Definition ID of the token where you want to show the diagnostic>
  //  Level: 1 - Info, 2 - Warning, 3 - Error OPTIONAL.
  //
  // The Diagnostic ID name is specific to the diagnostic being generated. Python and Java creates
  // ID using a counter in format AZ_PY_<Countrervalue> by python
  // parser and AZ_JAVA_<CountrerValue> by Java parser>,

  nlohmann::json DoDumpDiagnosticNode(ApiViewMessage const& error)
  {
    nlohmann::json newNode;
    newNode["DiagnosticId"] = error.DiagnosticId;
    newNode["Text"] = error.DiagnosticText;
    newNode["TargetId"] = error.TargetId;
    if (!error.HelpLinkUri.empty())
    {
      newNode["HelpLinkUri"] = error.HelpLinkUri;
    }
    switch (error.Level)
    {
      case ApiViewMessage::MessageLevel::Info:
        newNode["Level"] = 1;
        break;
      case ApiViewMessage::MessageLevel::Warning:
        newNode["Level"] = 2;
        break;
      case ApiViewMessage::MessageLevel::Error:
        newNode["Level"] = 3;
        break;
      case ApiViewMessage::MessageLevel::None:
        break;
    }
    return newNode;
  }

  virtual void DumpMessageNode(ApiViewMessage const& error) override
  {
    m_json["Diagnostics"].push_back(DoDumpDiagnosticNode(error));
  }
};
