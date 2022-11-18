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

public:
  JsonDumper(
      std::string_view reviewName,
      std::string_view serviceName,
      std::string_view packageName)
      : AstDumper()
  {
    m_json["Name"] = reviewName;
    m_json["Language"] = "C++";
    m_json["ServiceName"] = serviceName;
    m_json["PackageName"] = packageName;
    m_json["Tokens"] = nlohmann::json::array();
  }

  void DumpToFile(std::ostream& outfile) { outfile << m_json; }
  nlohmann::json const& GetJson() { return m_json; }

  virtual void InsertWhitespace(int count) override
  {
    std::string whiteSpace(count, ' ');
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr}, {"NavigateToId", nullptr}, {"Value", whiteSpace}, {"Kind", 4}});
  }
  virtual void InsertNewline() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr}, {"NavigateToId", nullptr}, {"Value", nullptr}, {"Kind", 1}});
  }
  virtual void InsertKeyword(std::string_view const& keyword) override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr}, {"NavigateToId", nullptr}, {"Value", keyword}, {"Kind", 4}});
  }
  virtual void InsertText(std::string_view const& text) override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr}, {"NavigateToId", nullptr}, {"Value", text}, {"Kind", 0}});
  }
  virtual void InsertPunctuation(const char punctuation) override
  {
    std::string punctuationString{punctuation};
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", punctuationString},
         {"Kind", 3}});
  }
  virtual void InsertLineIdMarker() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr},
         {"NavigateToId", nullptr},
         {"Value", nullptr},
         {"Kind", 5}}); // Not clear if this is used at all.
  }
  virtual void InsertTypeName(std::string_view const& type, std::string_view const& navigationId)
      override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", type}, {"NavigateToId", navigationId}, {"Value", type}, {"Kind", 6}});
  }
  virtual void InsertMemberName(std::string_view const& member) override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", member}, {"NavigateToId", nullptr}, {"Value", member}, {"Kind", 7}});
  }
  virtual void InsertStringLiteral(std::string_view const& str) override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr}, {"NavigateToId", nullptr}, {"Value", str}, {"Kind", 8}});
  }
  virtual void InsertLiteral(std::string_view const& str) override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr}, {"NavigateToId", nullptr}, {"Value", str}, {"Kind", 9}});
  }
  virtual void InsertComment(std::string_view const& comment) override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr}, {"NavigateToId", nullptr}, {"Value", comment}, {"Kind", 10}});
  }
  virtual void AddDocumentRangeStart() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr}, {"NavigateToId", nullptr}, {"Value", nullptr}, {"Kind", 11}});
  }
  virtual void AddDocumentRangeEnd() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr}, {"NavigateToId", nullptr}, {"Value", nullptr}, {"Kind", 12}});
  }
  virtual void AddDeprecatedRangeStart() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr}, {"NavigateToId", nullptr}, {"Value", nullptr}, {"Kind", 13}});
  }
  virtual void AddDeprecatedRangeEnd() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr}, {"NavigateToId", nullptr}, {"Value", nullptr}, {"Kind", 14}});
  }
  virtual void AddDiffRangeStart() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr}, {"NavigateToId", nullptr}, {"Value", nullptr}, {"Kind", 15}});
  }
  virtual void AddDiffRangeEnd() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr}, {"NavigateToId", nullptr}, {"Value", nullptr}, {"Kind", 16}});
  }
  virtual void AddInheritanceInfoStart() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr}, {"NavigateToId", nullptr}, {"Value", nullptr}, {"Kind", 17}});
  }
  virtual void AddInheritanceInfoEnd() override
  {
    m_json["Tokens"].push_back(
        {{"DefinitionId", nullptr}, {"NavigateToId", nullptr}, {"Value", nullptr}, {"Kind", 18}});
  }

  nlohmann::json DoDumpTypeHierarchyNode(std::shared_ptr<TypeHierarchy::TypeHierarchyNode> const& node)
  {
    nlohmann::json newNode;
    newNode["Text"] = node->NodeName;
    newNode["NavigationId"] = node->NavigationId;
    for (auto const& child : node->Children)
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
      case TypeHierarchy::TypeHierarchyClass::Unknown:
        newNode["Tags"]["TypeKind"] = "unknown";
        break;
    }
    return newNode;
  }
  
  virtual void DumpTypeHierarchyNode(
          std::shared_ptr<TypeHierarchy::TypeHierarchyNode> const& node){
    m_json["Navigation"].push_back(DoDumpTypeHierarchyNode(node));

  };
};
