// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#pragma once
#include "ApiViewProcessor.hpp"
#include <memory>
#include <string>
#include <string_view>
#include <vector>

class AstDumper {
  std::string m_currentNamespace;
  std::vector<std::string> m_namespaceComponents;
  int m_indentationLevel{};

  void OpenNamespace(std::string_view const& namespaceName);
  void OpenNamespaces(
      std::vector<std::string> const& namespaceComponents,
      std::vector<std::string>::iterator& current);
  void CloseNamespace(std::string_view const& namespaceName);
  void CloseNamespaces(
      std::vector<std::string> const& namespaceComponents,
      std::vector<std::string>::iterator& current);

public:
  // Note about the different functions.
  // Functions named "InsertXxx" and "AddXxx" are intended to insert elements into the output
  // stream. Functions without "Insert" or "Add" implement higher level constructs like changing the
  // relative indent on new lines, inserting new lines, managing namespaces, etc.
  void AdjustIndent(int indentDelta = 0);
  void LeftAlign();
  void Newline();
  void SetNamespace(std::string_view const& currentNamespace);

  virtual void InsertNewline() = 0;
  virtual void InsertWhitespace(int count = 1) = 0;
  virtual void InsertKeyword(std::string_view const& keyword) = 0;
  virtual void InsertText(std::string_view const& text) = 0;
  virtual void InsertPunctuation(char punctuation) = 0;
  virtual void InsertLineIdMarker() = 0;
  virtual void InsertTypeName(
      std::string_view const& type,
      std::string_view const& typeNavigationId)
      = 0;
  virtual void InsertMemberName(std::string_view const& member) = 0;
  virtual void InsertStringLiteral(std::string_view const& str) = 0;
  virtual void InsertLiteral(std::string_view const& str) = 0;
  virtual void InsertComment(std::string_view const& comment) = 0;
  virtual void AddDocumentRangeStart() = 0;
  virtual void AddDocumentRangeEnd() = 0;
  virtual void AddDeprecatedRangeStart() = 0;
  virtual void AddDeprecatedRangeEnd() = 0;
  virtual void AddDiffRangeStart() = 0;
  virtual void AddDiffRangeEnd() = 0;
  virtual void AddInheritanceInfoStart() = 0;
  virtual void AddInheritanceInfoEnd() = 0;

  virtual void DumpTypeHierarchyNode(std::shared_ptr<TypeHierarchy::TypeHierarchyNode> const& node)
      = 0;
  virtual void DumpMessageNode(ApiViewMessage const&) = 0;
};
