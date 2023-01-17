// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#pragma once
#include "ApiViewMessage.hpp"
#include "AstDumper.hpp"
#include <iostream>
#include <string>
#include <string_view>

class TextDumper : public AstDumper {
  std::ostream& m_stream;

public:
  TextDumper(std::ostream& stream) : m_stream(stream) {}

  virtual void InsertWhitespace(int count) override { m_stream << std::string(count, ' '); }
  virtual void InsertNewline() override { m_stream << std::endl; }
  virtual void InsertKeyword(std::string_view const& keyword) override { m_stream << keyword; }
  virtual void InsertText(std::string_view const& text) override { m_stream << text; }
  virtual void InsertPunctuation(const char punctuation) override { m_stream << punctuation; }
  virtual void InsertLineIdMarker() override { m_stream << "// "; }
  virtual void InsertTypeName(std::string_view const& type, std::string_view const&) override
  {
    m_stream << type;
  }
  virtual void InsertMemberName(std::string_view const& member) override { m_stream << member; }
  virtual void InsertStringLiteral(std::string_view const& str) override { m_stream << str; }
  virtual void InsertLiteral(std::string_view const& str) override { m_stream << str; }
  virtual void InsertComment(std::string_view const& comment) override { m_stream << comment; }
  virtual void AddDocumentRangeStart() override { m_stream << "/*"; }
  virtual void AddDocumentRangeEnd() override { m_stream << "*/"; }
  virtual void AddDeprecatedRangeStart() override { m_stream << "/* ** DEPRECATED **"; }
  virtual void AddDeprecatedRangeEnd() override { m_stream << "/* ** DEPRECATED ** */"; }
  virtual void AddDiffRangeStart() override { m_stream << "/* ** DIFF **"; }
  virtual void AddDiffRangeEnd() override { m_stream << " ** DIFF ** */"; }
  virtual void AddInheritanceInfoStart() override { m_stream << "/* ** INHERITANCE **"; }
  virtual void AddInheritanceInfoEnd() override { m_stream << " ** INHERITANCE ** */"; }

  void DoDumpHierarchyNode(
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> const& node,
      int indentation)
  {
    std::string prefix(indentation, ' ');
    m_stream << prefix << "/* ** HIERARCHY NODE START ** */" << std::endl;
    m_stream << prefix << "/* Type: ";
    switch (node->NodeClass)
    {
      case TypeHierarchy::TypeHierarchyClass::Assembly:
        m_stream << prefix << "Assembly";
        break;
      case TypeHierarchy::TypeHierarchyClass::Class:
        m_stream << prefix << "Class";
        break;
      case TypeHierarchy::TypeHierarchyClass::Interface:
        m_stream << prefix << "Interface";
        break;
      case TypeHierarchy::TypeHierarchyClass::Struct:
        m_stream << prefix << "Struct";
        break;
      case TypeHierarchy::TypeHierarchyClass::Enum:
        m_stream << prefix << "Enum";
        break;
      case TypeHierarchy::TypeHierarchyClass::Delegate:
        m_stream << prefix << "Delegate";
        break;
      case TypeHierarchy::TypeHierarchyClass::Namespace:
        m_stream << prefix << "Namespace";
        break;
    };
    m_stream << " */" << std::endl;

    m_stream << prefix << "/* Navigation:" << node->NavigationId << " */" << std::endl;
    m_stream << prefix << "/* Name: " << node->NodeName << " */" << std::endl;
    for (auto const& child : node->Children)
    {
      DoDumpHierarchyNode(child.second, indentation + 2);
    }
    m_stream << prefix << "/* ** HIERARCHY NODE END ** */" << std::endl;
  }

  virtual void DumpTypeHierarchyNode(
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> const& node) override
  {
    DoDumpHierarchyNode(node, 0);
  }

  virtual void DumpMessageNode(ApiViewMessage const& message) override
  {
    m_stream << "/* ** DIAGNOSTIC START ** */" << std::endl;
    m_stream << "/* Type: " << message.DiagnosticId << " */" << std::endl;
    m_stream << "/* NodeId: " << message.TargetId << " */" << std::endl;
    if (!message.HelpLinkUri.empty())
    {
      m_stream << "/* HelpUri: " << message.HelpLinkUri << " */" << std::endl;
    }
    m_stream << "/* Text: " << message.DiagnosticText << " */" << std::endl;
    m_stream << "/* Level: ";
    if (message.Level != ApiViewMessage::MessageLevel::None)
    {
      switch (message.Level)
      {
        case ApiViewMessage::MessageLevel::Error:
          m_stream << "Error";
          break;
        case ApiViewMessage::MessageLevel::Warning:
          m_stream << "Warning";
          break;
        case ApiViewMessage::MessageLevel::Info:
          m_stream << "Info";
          break;
      }
      m_stream << " */" << std::endl;
    }
    m_stream << "/* ** DIAGNOSTIC END ** */" << std::endl;
  }
};
