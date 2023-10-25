// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#include "AstDumper.hpp"
#include <iostream>
#include <list>
#include <ranges>
#include <sstream>
#include <string>
#include <string_view>
#include <vector>

constexpr int namespaceIndent = 2;

std::vector<std::string> SplitNamespace(std::string_view const& namespaceName)
{
  std::vector<std::string> namespaceComponents;
  if (!namespaceName.empty())
  {
    std::string ns{namespaceName};
    int separator;
    do
    {
      separator = ns.find("::");
      if (separator != std::string::npos)
      {
        namespaceComponents.push_back(ns.substr(0, separator));
        ns.erase(0, separator + 2);
      }
    } while (separator != std::string::npos);
    namespaceComponents.push_back(ns);
  }
  return namespaceComponents;
}

void AstDumper::OpenNamespaces(
    std::vector<std::string> const& namespaceComponents,
    std::vector<std::string>::iterator& current)
{
  for (; current != namespaceComponents.end(); current++)
  {
    OpenNamespace(*current);
  }
}

void AstDumper::OpenNamespace(std::string_view const& namespaceName)
{
  LeftAlign();
  InsertKeyword("namespace");
  InsertWhitespace();
  InsertIdentifier(namespaceName);
  InsertWhitespace();
  InsertPunctuation('{');
  AdjustIndent(namespaceIndent);
  Newline();
}
void AstDumper::CloseNamespaces(
    std::vector<std::string> const& namespaceComponents,
    std::vector<std::string>::iterator& current)
{
  // Back out the depth of the namespaces we're closing.
  std::list<std::string_view> namespacesToClose;
  for (; current != namespaceComponents.end(); current++)
  {
    namespacesToClose.push_back(*current);
    AdjustIndent(-namespaceIndent);
  }
  LeftAlign();
  std::stringstream ss;
  ss << "// namespace ";

  bool firstNs = true;
  for (auto const& nsToClose : namespacesToClose)
  {
    InsertPunctuation('}');
    if (!firstNs)
    {
      ss << "::";
    }
    firstNs = false;
    ss << nsToClose;
  }
  InsertWhitespace();

  InsertComment(ss.str());
  Newline();
  Newline();
}

void AstDumper::CloseNamespace(std::string_view const& namespaceName)
{
  AdjustIndent(-namespaceIndent);
  LeftAlign();
  InsertPunctuation('}');
  InsertWhitespace();
  InsertComment("// ");
  InsertComment(namespaceName);
  Newline();
}

void AstDumper::SetNamespace(std::string_view const& newNamespace)
{
  if (m_currentNamespace != newNamespace)
  {
    auto oldComponents = SplitNamespace(m_currentNamespace);
    auto newComponents = SplitNamespace(newNamespace);

    auto oldNs = oldComponents.begin();
    auto newNs = newComponents.begin();
    do
    {
      // We ran out of the new NS elements. Everything is now closing out old NS elements.
      if (newNs == newComponents.end())
      {
        CloseNamespaces(oldComponents, oldNs);
      }
      // We ran out of the old NS elements. Everything is now a new NS.
      else if (oldNs == oldComponents.end())
      {
        OpenNamespaces(newComponents, newNs);
      }
      else if (*newNs != *oldNs)
      {
        CloseNamespaces(oldComponents, oldNs);
        OpenNamespaces(newComponents, newNs);
      }
      else
      {
        oldNs++;
        newNs++;
      }
    } while (oldNs != oldComponents.end() || newNs != newComponents.end());
    // We've now resynchronized the old namespace with the new namespace, update the local state.
    m_currentNamespace = newNamespace;
  }
}

void AstDumper::Newline()
{
  InsertNewline();
  m_currentCursor = 0;
}

void AstDumper::LeftAlign()
{
  InsertWhitespace(m_indentationLevel);
  m_currentCursor = m_indentationLevel;
}

void AstDumper::AdjustIndent(int indentDelta) { m_indentationLevel += indentDelta; }
