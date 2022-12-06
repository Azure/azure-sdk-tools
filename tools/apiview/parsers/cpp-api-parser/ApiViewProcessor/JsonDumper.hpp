// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#pragma once
#include "AstDumper.hpp"
#include <iostream>
#include <nlohmann/json.hpp>
#include <string>
#include <string_view>

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
    InheritanceInfoStart = 17,
    InheritanceInfoEnd = 18
  };

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

  void DumpToFile(std::ostream& outfile) { outfile << m_json; }
  nlohmann::json const& GetJson() { return m_json; }

  // Each ApiView node has 4 mandatory members:
  //
  // DefinitionId: ID used in the Navigation pane for type navigation.
  // NavigateToId: ???
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
  }
  virtual void InsertText(std::string_view const& text) override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", text},
         {"Kind", TokenKinds::Text}});
  }
  virtual void InsertPunctuation(const char punctuation) override
  {
    std::string punctuationString{punctuation};
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", punctuationString},
         {"Kind", TokenKinds::Punctuation}});
  }
  virtual void InsertLineIdMarker() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", nullptr},
         {"Kind", TokenKinds::LineIdMarker}}); // Not clear if this is used at all.
  }
  virtual void InsertTypeName(std::string_view const& type, std::string_view const& navigationId)
      override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", navigationId},
         {"NavigateToId", navigationId},
         {"Value", type},
         {"Kind", TokenKinds::TypeName}});
  }
  virtual void InsertMemberName(std::string_view const& member) override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", member},
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
  }
  virtual void InsertLiteral(std::string_view const& str) override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", str},
         {"Kind", TokenKinds::Literal}});
  }
  virtual void InsertComment(std::string_view const& comment) override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", comment},
         {"Kind", TokenKinds::Comment}});
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
  virtual void AddInheritanceInfoStart() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", nullptr},
         {"Kind", TokenKinds::InheritanceInfoStart}});
  }
  virtual void AddInheritanceInfoEnd() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", nullptr},
         {"Kind", TokenKinds::InheritanceInfoEnd}});
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
