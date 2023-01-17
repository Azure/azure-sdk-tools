// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#include "AstNode.hpp"
#include "ProcessorImpl.hpp"
#include <clang/AST/ASTConsumer.h>
#include <clang/AST/Comment.h>
#include <clang/AST/CommentVisitor.h>
#include <clang/AST/ExprCXX.h>
#include <clang/AST/RecursiveASTVisitor.h>
#include <clang/AST/Type.h>
#include <clang/AST/TypeVisitor.h>
#include <iostream>
#include <iterator>
#include <list>
#include <vector>

using namespace clang;

class AstMethod;

std::string AccessSpecifierToString(AccessSpecifier specifier)
{

  switch (specifier)
  {
    case AS_none:
      return "none";
    case AS_private:
      return "private";
    case AS_protected:
      return "protected";
    case AS_public:
      return "public";
  }
  throw std::runtime_error(
      "Unknown access specifier: " + std::to_string(static_cast<int>(specifier)));
}

class MyCommentVisitor : public comments::ConstCommentVisitor<MyCommentVisitor, std::string> {
public:
  std::string visitComment(const comments::Comment* comment)
  {
    std::string rv;
    for (auto child = comment->child_begin(); child != comment->child_end(); child++)
    {
      rv += visit(*child);
    }
    return rv;
  };
  std::string visitFullComment(const comments::FullComment* decl)
  {
    std::string val;
    for (auto child = decl->child_begin(); child != decl->child_end(); child++)
    {
      val += visit(*child);
    }
    return val;
  };
  std::string visitBlockCommandComment(const comments::BlockCommandComment* bc)
  {
    //    llvm::outs() << "Block command: "
    //                 << comments::CommandTraits::getBuiltinCommandInfo(bc->getCommandID())->Name
    //                 << "\n";
    std::string rv;
    if (comments::CommandTraits::getBuiltinCommandInfo(bc->getCommandID())->IsBriefCommand)
    {
      for (auto child = bc->child_begin(); child != bc->child_end(); child++)
      {
        rv += visit(*child);
      }
    }
    return rv;
  };
  std::string visitHTMLStartTagComment(const comments::HTMLStartTagComment* startTag)
  {
    std::string rv = "<" + std::string(startTag->getTagName()) + " ";
    auto attributeCount = startTag->getNumAttrs();
    for (auto i = 0ul; i < attributeCount; i += 1)
    {
      auto& attribute{startTag->getAttr(i)};
      rv += std::string(attribute.Name);
      rv += "=";
      rv += "'" + std::string(attribute.Value) + "'";
    }
    rv += ">";

    return rv;
  }
  std::string visitHTMLEndTagComment(const comments::HTMLEndTagComment* startTag)
  {
    std::string rv = "</" + std::string(startTag->getTagName()) + ">";
    return rv;
  }
  std::string visitVerbatimBlockComment(const comments::VerbatimBlockComment* vbc)
  {
    std::string rv;
    for (auto child = vbc->child_begin(); child != vbc->child_end(); child++)
    {
      rv += visit(*child);
    }
    return rv;
  }
  std::string visitVerbatimBlockLineComment(const comments::VerbatimBlockLineComment* vbc)
  {
    std::string rv = std::string(vbc->getText());

    for (auto child = vbc->child_begin(); child != vbc->child_end(); child++)
    {
      rv += visit(*child);
    }
    return rv;
  }
  std::string visitParagraphComment(const comments::ParagraphComment* decl)
  {
    std::string rv;
    for (auto child = decl->child_begin(); child != decl->child_end(); child++)
    {
      rv += visit(*child) + "\n";
    }
    rv += "\n";

    return rv;
  };

  std::string visitTextComment(const comments::TextComment* tc)
  {
    return static_cast<std::string>(tc->getText());
  };

  std::string visitHTMLTagComment(const comments::HTMLTagComment* tag)
  {
    tag->dump();
    return "***HTML Tag Comment***";
  }
  std::string visitInlineContentComment(const comments::InlineContentComment* tag)
  {
    tag->dump();
    return "*** Inline Content Comment ***";
  }
  std::string visitInlineCommandComment(const comments::InlineCommandComment* tag)
  {
    tag->dump();
    return "*** Inline Command Comment ***";
  }
  std::string visitParamCommandComment(const comments::ParamCommandComment* tag)
  {
    tag->dump();
    return "*** Param Command Comment ***";
  }
  std::string visitTParamCommandComment(const comments::TParamCommandComment* tag)
  {
    tag->dump();
    return "*** TParam Command Comment ***";
  }
  std::string visitVerbatimLineComment(const comments::VerbatimLineComment* tag)
  {
    tag->dump();
    return "*** Verbatim Line Comment ***";
  }
};

std::string AstNode::GetCommentForNode(ASTContext& context, Decl const* decl)
{
  auto fullComment{context.getCommentForDecl(decl, nullptr)};
  if (fullComment)
  {
    MyCommentVisitor commentVisitor;
    return commentVisitor.visit(fullComment);
  }
  return "";
}

std::string AstNode::GetCommentForNode(ASTContext& context, Decl const& decl)
{
  auto fullComment{context.getCommentForDecl(&decl, nullptr)};
  if (fullComment)
  {
    MyCommentVisitor commentVisitor;
    return commentVisitor.visit(fullComment);
  }
  return "";
}

AstNode::AstNode(const Decl*) {}

struct AstTerminalNode : public AstNode
{
  AstTerminalNode() : AstNode(nullptr) {}

  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override
  {
    dumper->SetNamespace("");
  }
};

class AstType {

  struct AstTypeVisitor : public TypeVisitor<AstTypeVisitor, std::unique_ptr<AstType>>
  {
    std::unique_ptr<AstType> VisitQualType(QualType qt)
    {
      llvm::outs() << "Visit QualType"
                   << "\n";
      return TypeVisitor::Visit(qt.split().Ty);
    }
    std::unique_ptr<AstType> VisitType(const Type* t)
    {
      llvm::outs() << "Visit Type " << QualType::getAsString(QualType(t, 0).split(), LangOptions())
                   << "Type class: " << t->getTypeClassName() << "\n";
      return nullptr;
    }
    std::unique_ptr<AstType> VisitElaboratedType(const ElaboratedType* et)
    {
      llvm::outs() << "Visit Elaborated type.\n";
      return nullptr;
      //      return std::make_unique<AstType>(QualType(et, 0), xxx);
    }
    std::unique_ptr<AstType> VisitRValueReferenceType(const RValueReferenceType* rv)
    {
      llvm::outs() << "Visit RValueReferenceType" << rv->isSugared() << "\n";
      return VisitQualType(rv->desugar());
    }
    std::unique_ptr<AstType> VisitLValueReferenceType(const LValueReferenceType* lv)
    {
      llvm::outs() << "Visit LValueReferenceType" << lv->isSugared() << "\n";
      if (lv->isSugared())
      {
        return VisitQualType(lv->desugar());
      }
      else
      {
        return VisitQualType(lv->getPointeeType());
      }
    }
  };
  std::string m_internalTypeName;
  bool m_isBuiltinType;
  bool m_isConstQualified;
  bool m_isVolatile;
  bool m_hasQualifiers;
  bool m_isReference;
  bool m_isRValueReference;
  bool m_isPointer;
  std::unique_ptr<AstType> m_underlyingType;

public:
  AstType(QualType type)
      : m_isBuiltinType{type->isBuiltinType()}, m_isConstQualified{type.isLocalConstQualified()},
        m_isVolatile{type.isLocalVolatileQualified()}, m_hasQualifiers{type.hasLocalQualifiers()},
        m_isReference{type.getTypePtr()->isReferenceType()},
        m_isRValueReference(type.getTypePtr()->isRValueReferenceType()),
        m_isPointer{type.getTypePtr()->isPointerType()}
  {
    PrintingPolicy pp{LangOptions{}};
    pp.adjustForCPlusPlus();
    m_internalTypeName = QualType::getAsString(type.split(), pp);
  }
  AstType(QualType type, const ASTContext& context)
      : m_isBuiltinType{type->isBuiltinType()}, m_isConstQualified{type.isLocalConstQualified()},
        m_isVolatile{type.isLocalVolatileQualified()}, m_hasQualifiers{type.hasLocalQualifiers()},
        m_isReference{type.getTypePtr()->isReferenceType()},
        m_isRValueReference(type.getTypePtr()->isRValueReferenceType()),
        m_isPointer{type.getTypePtr()->isPointerType()}
  {

    PrintingPolicy pp{LangOptions{}};
    pp.adjustForCPlusPlus();
    m_internalTypeName = QualType::getAsString(type.split(), pp);
    // Walk the type looking for an inner type which appears to be a reasonable inner type.
    //    if (typePtr->getTypeClass() != Type::Elaborated && typePtr->getTypeClass() !=
    //    Type::Builtin)
    //    {
    //      AstTypeVisitor visitTypes;
    //      m_underlyingType = visitTypes.Visit(typePtr);
    //    }
  }
  void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const;
};

class AstStatement {

public:
  AstStatement(Stmt const* statement, ASTContext& context) {}
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const;
};

class AstExpr : public AstStatement {
protected:
  AstType m_type;
  AstExpr(Expr const* expression, ASTContext& context)
      : AstStatement(expression, context), m_type{expression->getType()}
  {
  }

public:
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override;
  static std::unique_ptr<AstExpr> Create(Stmt const* expression, ASTContext& context);
  virtual bool IsEmptyExpression() const { return false; }
};

class AstIntExpr : public AstExpr {
  int m_intValue{};

public:
  AstIntExpr(Expr const* expression, ASTContext& context) : AstExpr(expression, context)
  {
    Expr::EvalResult result;
    if (expression->EvaluateAsInt(result, context))
    {
      m_intValue = result.Val.getInt().getExtValue();
    }
  }

public:
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override
  {
    dumper->InsertLiteral(std::to_string(m_intValue));
  }
};

class AstStringExpr : public AstExpr {
  std::string m_stringValue;

public:
  AstStringExpr(StringLiteral const* expression, ASTContext& context)
      : AstExpr(expression, context), m_stringValue{expression->getBytes()}
  {
  }
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override
  {
    dumper->InsertPunctuation('"');
    dumper->InsertStringLiteral(m_stringValue);
    dumper->InsertPunctuation('"');
  }
};

class AstFloatExpr : public AstExpr {
  double m_doubleValue{};
  bool m_isFloat{};

public:
  AstFloatExpr(FloatingLiteral const* expression, ASTContext& context)
      : AstExpr(expression, context), m_doubleValue{expression->getValue().convertToDouble()}
  {
    auto const typePtr = expression->getType().getTypePtr();
    if (isa<BuiltinType>(typePtr))
    {
      m_isFloat = cast<BuiltinType>(typePtr)->getKind() == BuiltinType::Kind::Float;
    }
  }
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override
  {
    dumper->InsertLiteral(std::to_string(m_doubleValue));
    if (m_isFloat)
    {
      dumper->InsertLiteral("f");
    }
  }
};

class AstBoolExpr : public AstExpr {
  bool m_boolValue{};

public:
  AstBoolExpr(CXXBoolLiteralExpr const* expression, ASTContext& context)
      : AstExpr(expression, context), m_boolValue{expression->getValue()}
  {
  }
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override
  {
    if (m_boolValue)
    {
      dumper->InsertKeyword("true");
    }
    else
    {
      dumper->InsertKeyword("false");
    }
  }
};

class AstImplicitCastExpr : public AstExpr {
  AstType m_underlyingType;
  std::unique_ptr<AstExpr> m_castValue;

public:
  AstImplicitCastExpr(ImplicitCastExpr const* expression, ASTContext& context)
      : AstExpr(expression, context), m_underlyingType{expression->getType()},
        m_castValue{AstExpr::Create(*expression->child_begin(), context)}
  {
    // Assert that there is a single child of the ImplicitCastExprobject.
    assert(++expression->child_begin() == expression->child_end());
  }
  std::unique_ptr<AstExpr> const& GetCastValue() const { return m_castValue; }
  AstType const& GetCastType() const { return m_underlyingType; }
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override {}
};

class AstCastExpr : public AstExpr {
  AstType m_underlyingType;
  std::unique_ptr<AstExpr> m_castValue;

public:
  AstCastExpr(CastExpr const* expression, ASTContext& context)
      : AstExpr(expression, context), m_underlyingType{expression->getType()},
        m_castValue{AstExpr::Create(*expression->child_begin(), context)}
  {
    // Assert that there is a single child of the ImplicitCastExprobject.
    assert(++expression->child_begin() == expression->child_end());
  }
  std::unique_ptr<AstExpr> const& GetCastValue() const { return m_castValue; }
  AstType const& GetCastType() const { return m_underlyingType; }
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override
  {
    m_underlyingType.Dump(dumper, dumpOptions);
    dumper->InsertPunctuation('(');
    m_castValue->Dump(dumper, dumpOptions);
    dumper->InsertPunctuation(')');
  }
};

//   static_cast<float>(3.7);
// becomes:
// CXXStaticCastExpr 0x249a20ddb18
// <C:\Users\larryo.REDMOND\source\repos\ParseAzureSdkCpp\out\build\x64-debug\ParseTests\tests\ExpressionTests.cpp:19:22,
// col:44> 'float' static_cast<float> <NoOp>
//`-ImplicitCastExpr 0x249a20ddb00 <col:41> 'float' <FloatingCast> part_of_explicit_cast
//  `-FloatingLiteral 0x249a20ddac0 <col:41> 'double' 3.700000e+00
class AstNamedCastExpr : public AstExpr {
  std::unique_ptr<AstExpr> m_underlyingCast;
  std::string m_castName;

public:
  AstNamedCastExpr(CXXNamedCastExpr const* expression, ASTContext& context)
      : AstExpr(expression, context),
        m_underlyingCast{AstExpr::Create(*expression->child_begin(), context)},
        m_castName{expression->getCastName()}
  {
    // Assert that there is a single child of the CXXStaticCastExpr object.
    assert(++expression->child_begin() == expression->child_end());
  }
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override
  {
    dumper->InsertKeyword(m_castName);
    dumper->InsertPunctuation('<');
    m_type.Dump(dumper, dumpOptions);
    dumper->InsertPunctuation('>');
    dumper->InsertPunctuation('(');
    m_underlyingCast->Dump(dumper, dumpOptions);
    dumper->InsertPunctuation(')');
  }
};

template <typename T>
void DumpList(
    T start,
    T end,
    AstDumper* dumper,
    DumpNodeOptions const& dumpOptions,
    std::function<void(AstDumper* dumper, decltype(*start)& item)> dumpItemFunction)
{
  bool firstArg{true};
  for (T& it = start; it != end; ++it)
  {
    if (!firstArg)
    {
      dumper->InsertPunctuation(',');
      if (dumpOptions.NeedsLeadingNewline)
      {
        dumper->Newline();
      }
      else
      {
        dumper->InsertWhitespace();
      }
    }
    firstArg = false;
    dumpItemFunction(dumper, *it);
  }
}

class AstCtorExpr : public AstExpr {
  std::vector<std::unique_ptr<AstExpr>> m_args;

public:
  AstCtorExpr(CXXConstructExpr const* expression, ASTContext& context)
      : AstExpr(expression, context)
  {
    // Reset the type to the type of the constructor.
    m_type = AstType{expression->getType()};
    int argn = 0;
    for (auto const& arg : expression->arguments())
    {
      m_args.push_back(AstExpr::Create(arg, context));
      argn += 1;
    }
  }
  bool IsEmptyExpression() const override
  {
    for (const auto& initializer : m_args)
    {
      if (!initializer->IsEmptyExpression())
      {
        return false;
      }
    }
    return true;
  }

public:
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override
  {
    if (dumpOptions.DumpListInitializer)
    {
      dumper->InsertPunctuation('{');
    }
    else
    {
      m_type.Dump(dumper, dumpOptions);
      dumper->InsertPunctuation('(');
    }
    DumpList(
        m_args.begin(),
        m_args.end(),
        dumper,
        dumpOptions,
        [&](AstDumper* dumper, std::unique_ptr<AstExpr> const& expr) {
          expr->Dump(dumper, dumpOptions);
        });
    if (dumpOptions.DumpListInitializer)
    {
      dumper->InsertPunctuation('}');
    }
    else
    {
      dumper->InsertPunctuation(')');
    }
  }
};

class AstDeclRefExpr : public AstExpr {
  std::string m_referencedName;

public:
  AstDeclRefExpr(DeclRefExpr const* expression, ASTContext& context)
      : AstExpr(expression, context), m_referencedName{
                                          expression->getFoundDecl()->getNameAsString()}
  {
  }
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override
  {
    m_type.Dump(dumper, dumpOptions);
    dumper->InsertPunctuation(':');
    dumper->InsertPunctuation(':');
    dumper->InsertMemberName(m_referencedName);
  }
};

class AstDependentDeclRefExpr : public AstExpr {
  std::string m_referencedName;

public:
  AstDependentDeclRefExpr(DependentScopeDeclRefExpr const* expression, ASTContext& context)
      : AstExpr(expression, context), m_referencedName{expression->getDeclName().getAsString()}
  {
  }
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override
  {
    m_type.Dump(dumper, dumpOptions);
    dumper->InsertPunctuation(':');
    dumper->InsertPunctuation(':');
    dumper->InsertMemberName(m_referencedName);
  }
};

class AstNullptrRefExpr : public AstExpr {

public:
  AstNullptrRefExpr(CXXNullPtrLiteralExpr const* expression, ASTContext& context)
      : AstExpr(expression, context)
  {
  }
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override
  {
    dumper->InsertKeyword("nullptr");
  }
};

class AstMethodCallExpr : public AstExpr {
  std::string m_calledMethod;
  std::unique_ptr<AstExpr> m_memberAccessor;
  std::list<std::unique_ptr<AstExpr>> m_methodParams;

public:
  AstMethodCallExpr(CXXMemberCallExpr const* expression, ASTContext& context)
      : AstExpr(expression, context)
  {
    m_calledMethod = expression->getMethodDecl()->getNameAsString();
    m_type = AstType{expression->getObjectType()};
    assert(++expression->child_begin() == expression->child_end());
    assert(isa<MemberExpr>(*expression->child_begin()));
    m_memberAccessor = AstExpr::Create(*expression->child_begin(), context);
  }
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override;
};
class AstInitializerList : public AstExpr {
  std::vector<std::unique_ptr<AstExpr>> m_initializerValues;

protected:
  bool IsEmptyExpression() const override
  {
    for (const auto& initializer : m_initializerValues)
    {
      if (!initializer->IsEmptyExpression())
      {
        return false;
      }
    }
    return true;
  }

public:
  AstInitializerList(InitListExpr const* expression, ASTContext& context)
      : AstExpr(expression, context)
  {
    for (const auto& initializer : expression->children())
    {
      m_initializerValues.push_back(AstExpr::Create(initializer, context));
    }
  }
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override;
};

class AstMemberExpr : public AstExpr {
  std::string m_memberMethod;
  std::unique_ptr<AstExpr> m_member;

public:
  AstMemberExpr(MemberExpr const* expression, ASTContext& context)
      : AstExpr(expression, context),
        m_memberMethod{expression->getMemberDecl()->getNameAsString()},
        m_member{AstExpr::Create(*expression->child_begin(), context)}
  {
    assert(++expression->child_begin() == expression->child_end());
  }
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override;
};

class AstCallExpr : public AstExpr {
  std::string m_methodToCall;
  std::list<std::unique_ptr<AstExpr>> m_arguments;

public:
  AstCallExpr(CallExpr const* expression, ASTContext& context) : AstExpr(expression, context)
  {
    m_methodToCall = expression->getDirectCallee()->getQualifiedNameAsString();
    for (auto const& arg : expression->arguments())
    {
      m_arguments.push_back(AstExpr::Create(arg, context));
    }
  }
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override
  {
    if (dumpOptions.DumpListInitializer)
    {
      dumper->InsertPunctuation('{');
    }
    dumper->InsertMemberName(m_methodToCall);
    dumper->InsertPunctuation('(');
    DumpList(
        m_arguments.begin(),
        m_arguments.end(),
        dumper,
        dumpOptions,
        [&](AstDumper* dumper, std::unique_ptr<AstExpr> const& expr) {
          expr->Dump(dumper, dumpOptions);
        });

    dumper->InsertPunctuation(')');
    if (dumpOptions.DumpListInitializer)
    {
      dumper->InsertPunctuation('}');
    }
  };
};

class AstBinaryOperatorExpr : public AstExpr {
  std::unique_ptr<AstExpr> m_leftOperator;
  std::unique_ptr<AstExpr> m_rightOperator;
  BinaryOperator::Opcode m_opcode;
  std::string m_opcodeString;

public:
  AstBinaryOperatorExpr(BinaryOperator const* expression, ASTContext& context)
      : AstExpr(expression, context),
        m_leftOperator{AstExpr::Create(expression->getLHS(), context)},
        m_rightOperator{AstExpr::Create(expression->getRHS(), context)},
        m_opcode(expression->getOpcode()), m_opcodeString(expression->getOpcodeStr())
  {
  }
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override
  {
    m_leftOperator->Dump(dumper, dumpOptions);
    for (const auto ch : m_opcodeString)
    {
      dumper->InsertPunctuation(ch);
    }

    m_rightOperator->Dump(dumper, dumpOptions);
  };
};

class AstUnaryOperatorExpr : public AstExpr {
  std::unique_ptr<AstExpr> m_subExpr;
  bool m_isPrefix;
  bool m_isPostfix;
  std::string m_opcode;

public:
  AstUnaryOperatorExpr(UnaryOperator const* expression, ASTContext& context)
      : AstExpr(expression, context), m_subExpr{AstExpr::Create(expression->getSubExpr(), context)},
        m_opcode(expression->getOpcodeStr(expression->getOpcode())),
        m_isPrefix(expression->isPrefix()), m_isPostfix(expression->isPostfix())
  {
  }
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override
  {
    if (m_isPrefix)
    {
      if (m_opcode.size() == 1)
      {
        dumper->InsertPunctuation(m_opcode[0]);
      }
      else
      {
        dumper->InsertKeyword(m_opcode);
      }
    }
    m_subExpr->Dump(dumper, dumpOptions);
    if (m_isPostfix)
    {
      if (m_opcode.size() == 1)
      {
        dumper->InsertPunctuation(m_opcode[0]);
      }
      else
      {
        dumper->InsertKeyword(m_opcode);
      }
    }
  };
};

class AstScalarValueInit : public AstExpr {
  AstType m_underlyingType;

public:
  AstScalarValueInit(CXXScalarValueInitExpr const* expression, ASTContext& context)
      : AstExpr(expression, context), m_underlyingType{expression->getTypeSourceInfo()->getType()}

  {
  }
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override
  {
    m_underlyingType.Dump(dumper, dumpOptions);
    dumper->InsertPunctuation('(');
    dumper->InsertPunctuation(')');
  }
};

class AstDefaultInitExpr : public AstExpr {

  bool IsEmptyExpression() const override { return true; }

public:
  AstDefaultInitExpr(CXXDefaultInitExpr const* expression, ASTContext& context)
      : AstExpr(expression, context)
  {
  }
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override
  {
    //    dumper->InsertPunctuation('{');
    //    dumper->InsertPunctuation('}');
  }
};

class AstDefaultArgExpr : public AstExpr {

  bool IsEmptyExpression() const override { return true; }

public:
  AstDefaultArgExpr(CXXDefaultArgExpr const* expression, ASTContext& context)
      : AstExpr(expression, context)
  {
  }
  virtual void Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override
  {
    //    dumper->InsertPunctuation('{');
    //    dumper->InsertPunctuation('}');
  }
};

std::unique_ptr<AstExpr> AstExpr::Create(Stmt const* statement, ASTContext& context)
{
  if (statement)
  {
    if (isa<Expr>(statement))
    {
      auto expression = cast<Expr>(statement);
      const Expr* actualExpr = expression->IgnoreUnlessSpelledInSource();
      switch (actualExpr->getStmtClass())
      {
        case Stmt::IntegerLiteralClass:
          return std::make_unique<AstIntExpr>(cast<IntegerLiteral>(actualExpr), context);
        case Stmt::StringLiteralClass:
          return std::make_unique<AstStringExpr>(cast<StringLiteral>(actualExpr), context);
        case Stmt::FloatingLiteralClass:
          return std::make_unique<AstFloatExpr>(cast<FloatingLiteral>(actualExpr), context);
        case Stmt::CXXBoolLiteralExprClass:
          return std::make_unique<AstBoolExpr>(cast<CXXBoolLiteralExpr>(actualExpr), context);
        case Stmt::ImplicitCastExprClass:
          return std::make_unique<AstImplicitCastExpr>(cast<ImplicitCastExpr>(actualExpr), context);
        case Stmt::CXXDefaultInitExprClass:
          return std::make_unique<AstDefaultInitExpr>(
              cast<CXXDefaultInitExpr>(actualExpr), context);
        case Stmt::CXXDefaultArgExprClass:
          return std::make_unique<AstDefaultArgExpr>(cast<CXXDefaultArgExpr>(actualExpr), context);
        case Stmt::CXXConstructExprClass:
          return std::make_unique<AstCtorExpr>(cast<CXXConstructExpr>(actualExpr), context);
        case Stmt::DependentScopeDeclRefExprClass:
          return std::make_unique<AstDependentDeclRefExpr>(
              cast<DependentScopeDeclRefExpr>(actualExpr), context);
        case Stmt::DeclRefExprClass:
          return std::make_unique<AstDeclRefExpr>(cast<DeclRefExpr>(actualExpr), context);
        case Stmt::CXXNullPtrLiteralExprClass:
          return std::make_unique<AstNullptrRefExpr>(
              cast<CXXNullPtrLiteralExpr>(actualExpr), context);
        case Stmt::MemberExprClass:
          return std::make_unique<AstMemberExpr>(cast<MemberExpr>(actualExpr), context);
        case Stmt::CXXMemberCallExprClass:
          return std::make_unique<AstMethodCallExpr>(cast<CXXMemberCallExpr>(actualExpr), context);
        case Stmt::InitListExprClass:
          return std::make_unique<AstInitializerList>(cast<InitListExpr>(actualExpr), context);
        case Stmt::UnaryOperatorClass:
          return std::make_unique<AstUnaryOperatorExpr>(cast<UnaryOperator>(actualExpr), context);
        case Stmt::BinaryOperatorClass:
          return std::make_unique<AstBinaryOperatorExpr>(cast<BinaryOperator>(actualExpr), context);
        case Stmt::CXXScalarValueInitExprClass:
          return std::make_unique<AstScalarValueInit>(
              cast<CXXScalarValueInitExpr>(actualExpr), context);
        case Stmt::MaterializeTemporaryExprClass:
          // Assert that there is a single child of the MaterializeTemporaryExpr object.
          assert(++actualExpr->child_begin() == actualExpr->child_end());
          return Create(*actualExpr->child_begin(), context);

        case Stmt::CXXTemporaryObjectExprClass:
          return std::make_unique<AstCtorExpr>(cast<CXXConstructExpr>(actualExpr), context);

        case Stmt::CXXConstCastExprClass:
        case Stmt::CXXStaticCastExprClass:
        case Stmt::CXXFunctionalCastExprClass:
          return std::make_unique<AstCastExpr>(cast<CastExpr>(actualExpr), context);

        case Stmt::CXXStdInitializerListExprClass:
          // Assert that there is a single child of the CxxStdInitializerListExpr object.
          assert(++actualExpr->child_begin() == actualExpr->child_end());
          return Create(*actualExpr->child_begin(), context);
        case Stmt::CallExprClass:
          return std::make_unique<AstCallExpr>(cast<CallExpr>(actualExpr), context);
        case Stmt::ExprWithCleanupsClass:
          // Assert that there is a single child of the ExprWithCleanupsClass.
          assert(++actualExpr->child_begin() == actualExpr->child_end());
          return Create(*actualExpr->child_begin(), context);

        default:
          llvm::errs() << raw_ostream::Colors::RED
                       << "Unknown expression type : " << actualExpr->getStmtClassName()
                       << raw_ostream::Colors::RESET << "\n ";
          actualExpr->dump(llvm::outs(), context);
          return nullptr;
      }
      // else if (isa<CXXNamedCastExpr>(actualExpr))
      //{
      //   return std::make_unique<AstNamedCastExpr>(cast<CXXNamedCastExpr>(actualExpr), context);
      // }
      // else if (isa<CastExpr>(actualExpr))
      //{
      //   return std::make_unique<AstCastExpr>(cast<CastExpr>(actualExpr), context);
      // }
    }
    else
    {
      assert(isa<Stmt>(statement));
      llvm::errs() << raw_ostream::Colors::RED
                   << "Unknown statement type : " << statement->getStmtClassName()
                   << raw_ostream::Colors::RESET << "\n ";
      statement->dump(llvm::outs(), context);
      return nullptr;
    }
  }
  else
  {
    return nullptr;
  }
}

AstNamedNode::AstNamedNode(
    NamedDecl const* namedDecl,
    AzureClassesDatabase* const database,
    std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
    : AstNode(namedDecl),
      m_namespace{AstNode::GetNamespaceForDecl(namedDecl)}, m_name{namedDecl->getNameAsString()},
      m_classDatabase(database), m_navigationId{namedDecl->getQualifiedNameAsString()},
      m_nodeDocumentation{AstNode::GetCommentForNode(namedDecl->getASTContext(), namedDecl)},
      m_nodeAccess{namedDecl->getAccess()}
{
}

class AstBaseClass {
  AstType m_baseClass;
  AccessSpecifier m_access;

public:
  AstBaseClass(CXXBaseSpecifier const& base)
      : m_baseClass{base.getType()}, m_access{base.getAccessSpecifierAsWritten()} {};

  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions);
};

void AstBaseClass::DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions)
{
  if (m_access != AS_none)
  {
    dumper->InsertKeyword(AccessSpecifierToString(m_access));
    dumper->InsertWhitespace();
  }
  m_baseClass.Dump(dumper, dumpOptions);
}

class AstParamVariable : public AstNamedNode {
  std::string m_typeAsString;
  AstType m_type;
  bool m_isStatic{};
  bool m_isArray{};
  std::string m_variableInitializer;
  std::unique_ptr<AstExpr> m_defaultExpression;

public:
  AstParamVariable(
      ParmVarDecl const* var,
      AzureClassesDatabase* const database,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
      : AstNamedNode(var, database, parentNode), m_type{var->getType(), var->getASTContext()},
        m_isStatic(var->isStaticDataMember())

  {
    clang::PrintingPolicy pp{LangOptions{}};
    pp.adjustForCPlusPlus();

    m_typeAsString = QualType::getAsString(var->getType().split(), pp);

    if (var->getType().getTypePtr()->isArrayType())
    {
      m_isArray = true;
      m_typeAsString = QualType::getAsString(
          QualType{var->getType().getTypePtr()->getArrayElementTypeNoTypeQual(), 0}.split(), pp);
    }

    auto value = var->getEvaluatedValue();
    if (value)
    {
      llvm::raw_string_ostream os{m_variableInitializer};
      value->printPretty(os, var->getASTContext(), var->getType());
    }
    if (var->getDefaultArg())
    {
      m_defaultExpression = AstExpr::Create(var->getDefaultArg(), var->getASTContext());
    }
  }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override
  {
    if (dumpOptions.NeedsLeftAlign)
    {
      dumper->LeftAlign();
    }
    if (m_isStatic)
    {
      dumper->InsertKeyword("static");
      dumper->InsertWhitespace();
    }
    dumper->InsertLiteral(m_typeAsString);
    dumper->InsertWhitespace();
    dumper->InsertMemberName(Name());
    if (m_isArray)
    {
      dumper->InsertPunctuation('[');
      dumper->InsertPunctuation(']');
    }
    if (m_defaultExpression || !m_variableInitializer.empty())
    {
      dumper->InsertWhitespace();
      dumper->InsertPunctuation('=');
      dumper->InsertWhitespace();
      if (m_defaultExpression)
      {
        m_defaultExpression->Dump(dumper, dumpOptions);
      }
      else
      {
        dumper->InsertLiteral(m_variableInitializer);
      }
    }
    if (dumpOptions.NeedsTrailingSemi)
    {
      dumper->InsertPunctuation(';');
    }
    if (dumpOptions.NeedsTrailingNewline)
    {
      dumper->Newline();
    }
  }
};

class AstVariable : public AstNamedNode {
  std::string m_typeAsString;
  AstType m_type;
  bool m_isStatic{};
  bool m_isConstexpr{};
  bool m_isConst{};
  bool m_isArray{};
  std::string m_variableInitializer;

public:
  AstVariable(
      VarDecl const* var,
      AzureClassesDatabase* const database,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
      : AstNamedNode(var, database, parentNode), m_type{var->getType(), var->getASTContext()},
        m_isStatic(var->isStaticDataMember()), m_isConstexpr(var->isConstexpr()),
        m_isConst(var->getType().isConstQualified())
  {
    clang::PrintingPolicy pp{LangOptions{}};
    pp.adjustForCPlusPlus();
    m_typeAsString = QualType::getAsString(var->getType().split(), pp);

    if (var->getType().getTypePtr()->isArrayType())
    {
      m_isArray = true;
      m_typeAsString = QualType::getAsString(
          QualType{var->getType().getTypePtr()->getArrayElementTypeNoTypeQual(), 0}.split(), pp);
    }

    auto value = var->getEvaluatedValue();
    if (value)
    {
      llvm::raw_string_ostream os{m_variableInitializer};
      value->printPretty(os, var->getASTContext(), var->getType());
    }

    if (m_isStatic && !(m_isConstexpr || m_isConst))
    {
      database->CreateApiViewMessage(ApiViewMessages::NonConstStaticFields, m_navigationId);
    }
  }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override
  {
    if (dumpOptions.NeedsLeftAlign)
    {
      dumper->LeftAlign();
    }
    if (m_isStatic)
    {
      dumper->InsertKeyword("static");
      dumper->InsertWhitespace();
    }
    if (m_isConstexpr)
    {
      dumper->InsertKeyword("constexpr");
      dumper->InsertWhitespace();
    }
    dumper->InsertLiteral(m_typeAsString);
    dumper->InsertWhitespace();
    dumper->InsertMemberName(Name());
    if (m_isArray)
    {
      dumper->InsertPunctuation('[');
      dumper->InsertPunctuation(']');
    }
    if (!m_variableInitializer.empty())
    {
      dumper->InsertWhitespace();
      dumper->InsertPunctuation('=');
      dumper->InsertWhitespace();
      dumper->InsertLiteral(m_variableInitializer);
    }
    if (dumpOptions.NeedsTrailingSemi)
    {
      dumper->InsertPunctuation(';');
    }
    if (dumpOptions.NeedsTrailingNewline)
    {
      dumper->Newline();
    }
  }
};

class AstTemplateParameter : public AstNamedNode {
  bool m_wasDeclaredWithTypename{};
  bool m_isParameterPack{};
  std::string m_paramName;
  std::unique_ptr<AstType> m_defaultValue;

public:
  AstTemplateParameter(
      TemplateTypeParmDecl const* param,
      AzureClassesDatabase* const database,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
      : AstNamedNode(param, database, parentNode),
        m_wasDeclaredWithTypename{param->wasDeclaredWithTypename()},
        m_paramName{param->getNameAsString()}, m_isParameterPack{param->isParameterPack()}
  {
    if (param->hasDefaultArgument())
    {
      m_defaultValue
          = std::make_unique<AstType>(param->getDefaultArgument(), param->getASTContext());
    }

    for (auto attr : param->attrs())
    {
      llvm::outs() << "Attribute: " << attr->getSpelling() << "\n";
    }
  }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override
  {
    if (m_wasDeclaredWithTypename)
    {
      dumper->InsertKeyword("typename");
    }
    else
    {
      dumper->InsertKeyword("class");
    }
    if (m_isParameterPack)
    {
      dumper->InsertPunctuation('.');
      dumper->InsertPunctuation('.');
      dumper->InsertPunctuation('.');
    }
    if (!m_paramName.empty())
    {
      dumper->InsertWhitespace();
      dumper->InsertMemberName(m_paramName);
      if (m_defaultValue)
      {
        dumper->InsertWhitespace();
        dumper->InsertPunctuation('=');
        m_defaultValue->Dump(dumper, dumpOptions);
      }
    }
  }
};

// Template parameters which are template declarations. For example:
//   template <typename T, template <typename> class U = UniqueHandleHelper>
//   using UniqueHandle = typename U<T>::type;
// Also "T" in
//   @code
//       template <template <typename> class T> class container { };
//   @endcode
class AstTemplateTemplateParameter : public AstNamedNode {
  bool m_isParameterPack{};
  std::string m_paramName;
  std::list<std::unique_ptr<AstNode>> m_parameters;
  std::unique_ptr<AstNode> m_templateBody;
  std::string m_defaultTypeName;

public:
  AstTemplateTemplateParameter(
      TemplateTemplateParmDecl const* templateParam,
      AzureClassesDatabase* const database,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
      : AstNamedNode(templateParam, database, parentNode),
        m_paramName{templateParam->getNameAsString()}, m_isParameterPack{
                                                           templateParam->isParameterPack()}
  {
    for (auto attr : templateParam->attrs())
    {
      llvm::outs() << "Attribute: " << attr->getSpelling() << "\n";
    }

    for (auto& param : templateParam->getTemplateParameters()->asArray())
    {
      m_parameters.push_back(AstNode::Create(param, database, parentNode));
    }

    if (templateParam->hasDefaultArgument())
    {
      auto defaultArg = templateParam->getDefaultArgument().getArgument();
      switch (defaultArg.getKind())
      {

        case TemplateArgument::Template: {
          llvm::raw_string_ostream os(m_defaultTypeName);
          clang::PrintingPolicy pp{LangOptions{}};
          pp.adjustForCPlusPlus();

          defaultArg.getAsTemplate().print(os, pp);
        }
        break;
        default:
          llvm::errs() << "Unknown TemplateTemplate parameter default argument type: "
                       << defaultArg.getKind() << "\n";
          break;
      }
    }
  }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override
  {
    dumper->InsertKeyword("template");
    dumper->InsertWhitespace();
    dumper->InsertPunctuation('<');

    {
      DumpNodeOptions innerOptions{dumpOptions};
      innerOptions.NeedsLeftAlign = false;
      innerOptions.NeedsTrailingNewline = false;
      innerOptions.NeedsTrailingSemi = false;
      innerOptions.NeedsLeadingNewline = false;
      DumpList(
          m_parameters.begin(),
          m_parameters.end(),
          dumper,
          innerOptions,
          [&](AstDumper* dumper, std::unique_ptr<AstNode>& param) {
            param->DumpNode(dumper, innerOptions);
          });
    }
    dumper->InsertPunctuation('>');
    if (m_isParameterPack)
    {
      dumper->InsertPunctuation('.');
      dumper->InsertPunctuation('.');
      dumper->InsertPunctuation('.');
    }
    dumper->InsertWhitespace();
    dumper->InsertKeyword("class");
    dumper->InsertWhitespace();
    dumper->InsertMemberName(m_paramName);
    if (!m_defaultTypeName.empty())
    {
      dumper->InsertWhitespace();
      dumper->InsertPunctuation('=');
      dumper->InsertWhitespace();
      dumper->InsertMemberName(m_defaultTypeName);
    }
  }
};

class AstNonTypeTemplateParam : public AstNamedNode {
  std::unique_ptr<AstExpr> m_defaultArgument;
  AstType m_templateType;

public:
  AstNonTypeTemplateParam(
      NonTypeTemplateParmDecl const* param,
      AzureClassesDatabase* const database,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
      : AstNamedNode(param, database, parentNode),
        m_defaultArgument(AstExpr::Create(param->getDefaultArgument(), param->getASTContext())),
        m_templateType{param->getType()}
  {
  }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override
  {
    m_templateType.Dump(dumper, dumpOptions);
    if (m_defaultArgument)
    {
      dumper->InsertWhitespace();
      dumper->InsertPunctuation('=');
      dumper->InsertWhitespace();
      m_defaultArgument->Dump(dumper, dumpOptions);
    }
  }
};

// using rep = int64_t becomes:
//
// TypeAliasDecl 0x122e6738270
// <G:\Az\LarryO\azure-sdk-for-cpp\sdk\core\azure-core\inc\azure/core/datetime.hpp:22:5,
// col:17> col:11 referenced rep 'int64_t':'long long'
//`- TypedefType 0x122e5ed47f0 'int64_t' sugar
//    | -Typedef 0x122e505f3e0 'int64_t'
//  `
//        - BuiltinType 0x122e4762c70 'long long'

class AstTypeAlias : public AstNamedNode {
  AstType m_aliasedType;

public:
  AstTypeAlias(
      TypeAliasDecl const* alias,
      AzureClassesDatabase* const database,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
      : AstNamedNode(alias, database, parentNode), m_aliasedType{alias->getUnderlyingType()}

  {
  }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override
  {
    if (dumpOptions.NeedsNamespaceAdjustment)
    {
      dumper->SetNamespace(Namespace());
    }
    if (dumpOptions.NeedsLeftAlign)
    {
      dumper->LeftAlign();
    }
    dumper->InsertKeyword("using");
    dumper->InsertWhitespace();
    dumper->InsertMemberName(Name());
    dumper->InsertWhitespace();
    dumper->InsertPunctuation('=');
    dumper->InsertWhitespace();
    m_aliasedType.Dump(dumper, dumpOptions);
    dumper->InsertPunctuation(';');
    dumper->Newline();
  }
};

class AstFunction : public AstNamedNode {
protected:
  bool m_isConstexpr;
  bool m_isStatic;
  std::vector<std::unique_ptr<AstNode>> m_parameters;
  AstType m_returnValue;
  bool m_isMemberOfClass;
  bool m_isSpecialFunction;
  ExceptionSpecificationType m_exceptionSpecification;
  std::string m_exceptionExpression;
  std::string m_parentClass;

protected:
  void DumpExceptionSpecification(AstDumper* dumper, DumpNodeOptions const& dumpOptions)
  {
    switch (m_exceptionSpecification)
    {
      case EST_None: ///< no exception specification
      case EST_NoThrow: ///< Microsoft __declspec(nothrow) extension
        break;

      case EST_DynamicNone: ///< throw()
        dumper->InsertWhitespace();
        dumper->InsertKeyword("throw");
        dumper->InsertPunctuation('(');
        dumper->InsertPunctuation(')');
        break;
      case EST_Dynamic: ///< throw(T1, T2)
        break;
      case EST_MSAny: ///< Microsoft throw(...) extension
        dumper->InsertWhitespace();
        dumper->InsertKeyword("throw");
        dumper->InsertPunctuation('(');
        dumper->InsertPunctuation('.');
        dumper->InsertPunctuation('.');
        dumper->InsertPunctuation('.');
        dumper->InsertPunctuation(')');
        break;
      case EST_BasicNoexcept: ///< noexcept
        dumper->InsertWhitespace();
        dumper->InsertKeyword("noexcept");
        break;
      case EST_NoexceptFalse: ///< noexcept(expression), evals to 'false'
        dumper->InsertWhitespace();
        dumper->InsertKeyword("noexcept");
        dumper->InsertPunctuation('(');
        dumper->InsertKeyword("false");
        dumper->InsertPunctuation(')');
        break;
      case EST_NoexceptTrue: ///< noexcept(expression), evals to 'true'
        dumper->InsertWhitespace();
        dumper->InsertKeyword("noexcept");
        dumper->InsertPunctuation('(');
        dumper->InsertKeyword("true");
        dumper->InsertPunctuation(')');
        break;
      case EST_DependentNoexcept: ///< noexcept(expression), value-dependent
      {
        dumper->InsertWhitespace();
        dumper->InsertKeyword("noexcept");
        dumper->InsertPunctuation('(');
        dumper->InsertLiteral(m_exceptionExpression);
        dumper->InsertPunctuation(')');
        break;
      }
      default:
        llvm::errs() << "Unknown exception specification: " << m_exceptionSpecification << "\n";
        break;
    }
  }

public:
  AstFunction(
      FunctionDecl const* func,
      AzureClassesDatabase* const database,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
      : AstNamedNode(func, database, parentNode), m_isConstexpr(func->isConstexpr()),
        m_isStatic(func->isStatic()), m_returnValue(func->getReturnType(), func->getASTContext()),
        m_isMemberOfClass{func->isCXXClassMember()},
        m_isSpecialFunction{
            func->getKind() == Decl::CXXConstructor || func->getKind() == Decl::CXXDestructor},
        m_exceptionSpecification{func->getExceptionSpecType()}
  {
    if (m_exceptionSpecification == EST_DependentNoexcept)
    {
      auto typePtr = func->getType().getTypePtr();
      if (isa<FunctionProtoType>(typePtr))
      {
        auto functionPrototype = cast<FunctionProtoType>(typePtr);
        if (functionPrototype->getNoexceptExpr())
        {
          clang::PrintingPolicy pp{LangOptions{}};
          pp.adjustForCPlusPlus();
          llvm::raw_string_ostream os(m_exceptionExpression);
          functionPrototype->getNoexceptExpr()->printPretty(os, nullptr, pp);
        }
      }
    }

    for (auto param : func->parameters())
    {
      m_parameters.push_back(AstNode::Create(param, database, parentNode));
    }

    if (Namespace().empty())
    {
      database->CreateApiViewMessage(
          ApiViewMessages::TypeDeclaredInGlobalNamespace, m_navigationId);
    }
  }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override
  {
    if (dumpOptions.NeedsNamespaceAdjustment)
    {
      dumper->SetNamespace(Namespace());
    }
    if (dumpOptions.NeedsLeftAlign)
    {
      dumper->LeftAlign();
    }
    if (m_exceptionSpecification == EST_NoThrow)
    {
      dumper->InsertKeyword("__declspec");
      dumper->InsertPunctuation('(');
      dumper->InsertKeyword("nothrow");
      dumper->InsertPunctuation(')');
      dumper->InsertWhitespace();
    }
    if (!m_isSpecialFunction)
    {
      if (m_isStatic)
      {
        dumper->InsertKeyword("static");
        dumper->InsertWhitespace();
      }
      if (m_isConstexpr)
      {
        dumper->InsertKeyword("constexpr");
        dumper->InsertWhitespace();
      }
      m_returnValue.Dump(dumper, dumpOptions);
      dumper->InsertWhitespace();
    }
    if (dumpOptions.IncludeNamespace)
    {
      dumper->InsertMemberName(Namespace());
      dumper->InsertPunctuation(':');
      dumper->InsertPunctuation(':');
    }
    if (dumpOptions.IncludeContainingClass && !m_parentClass.empty())
    {
      dumper->InsertMemberName(m_parentClass);
      dumper->InsertPunctuation(':');
      dumper->InsertPunctuation(':');
    }
    dumper->InsertTypeName(Name(), m_navigationId);
    dumper->InsertPunctuation('(');
    {
      DumpNodeOptions innerOptions{dumpOptions};

      innerOptions.NeedsLeftAlign = false;
      innerOptions.NeedsTrailingNewline = false;
      innerOptions.NeedsTrailingSemi = false;
      innerOptions.NeedsLeadingNewline = false;

      DumpList(
          m_parameters.begin(),
          m_parameters.end(),
          dumper,
          innerOptions,
          [&](AstDumper* dumper, std::unique_ptr<AstNode>& node) {
            node->DumpNode(dumper, innerOptions);
          });
    }
    dumper->InsertPunctuation(')');
    if (!m_isMemberOfClass)
    {
      DumpExceptionSpecification(dumper, dumpOptions);
    }
    if (dumpOptions.NeedsTrailingSemi)
    {
      dumper->InsertPunctuation(';');
    }
    if (dumpOptions.NeedsTrailingNewline)
    {
      dumper->Newline();
    }
  }
};

class AstMethod : public AstFunction {
protected:
  bool m_isVirtual;
  bool m_isConst;
  bool m_isPure;
  RefQualifierKind m_refQualifier;

public:
  AstMethod(
      CXXMethodDecl const* method,
      AzureClassesDatabase* const database,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
      : AstFunction(method, database, parentNode), m_isVirtual(method->isVirtual()),
        m_isPure(method->isPure()), m_isConst(method->isConst())
  {
    auto typePtr = method->getType().getTypePtr()->castAs<FunctionProtoType>();
    m_refQualifier = typePtr->getRefQualifier();
    m_parentClass = method->getParent()->getNameAsString();
  }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override
  {
    if (dumpOptions.NeedsLeftAlign)
    {
      dumper->LeftAlign();
    }
    if (m_isVirtual)
    {
      dumper->InsertKeyword("virtual");
      dumper->InsertWhitespace();
    }
    {
      DumpNodeOptions innerOptions{dumpOptions};

      innerOptions.NeedsLeftAlign = false;
      innerOptions.NeedsTrailingNewline = false;
      innerOptions.NeedsTrailingSemi = false;
      AstFunction::DumpNode(dumper, innerOptions);
    }
    if (m_isConst)
    {
      dumper->InsertWhitespace();
      dumper->InsertKeyword("const");
    }
    switch (m_refQualifier)
    {
      case RQ_None:
        break;
      case RQ_RValue:
        dumper->InsertWhitespace();
        dumper->InsertPunctuation('&');
        dumper->InsertPunctuation('&');
        break;
      case RQ_LValue:
        dumper->InsertWhitespace();
        dumper->InsertPunctuation('&');
        break;
    }
    DumpExceptionSpecification(dumper, dumpOptions);
    if (m_isPure)
    {
      dumper->InsertWhitespace();
      dumper->InsertPunctuation('=');
      dumper->InsertWhitespace();
      dumper->InsertLiteral("0");
    }
    if (dumpOptions.NeedsTrailingSemi)
    {
      dumper->InsertPunctuation(';');
    }
    if (dumpOptions.NeedsTrailingNewline)
    {
      dumper->Newline();
    }
  }
};

class AstConstructor : public AstMethod {
  bool m_isDefault{false};
  bool m_isDeleted{false};
  bool m_isExplicit{false};
  bool m_isExplicitlyDefaulted{false};

public:
  AstConstructor(
      CXXConstructorDecl const* ctor,
      AzureClassesDatabase* const database,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
      : AstMethod(ctor, database, parentNode), m_isDefault{ctor->isDefaulted()},
        m_isDeleted{ctor->isDeleted()}, m_isExplicit{ctor->isExplicit()},
        m_isExplicitlyDefaulted{ctor->isExplicitlyDefaulted()}
  {
    if (m_isDefault && m_isDeleted)
    {
      llvm::outs() << "?? Defaulted deleted constructor?";
    }
    // Noisy diagnostic. Consider making it less noisy in the future.
    if (!m_isExplicit)
    {
      if (!ctor->getParent()->isEffectivelyFinal())
      {
        database->CreateApiViewMessage(ApiViewMessages::ImplicitConstructor, m_navigationId);
      }
    }
  }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override
  {
    if (dumpOptions.NeedsLeftAlign)
    {
      dumper->LeftAlign();
    }
    if (m_isExplicit)
    {
      dumper->InsertKeyword("explicit");
      dumper->InsertWhitespace();
    }
    {
      DumpNodeOptions innerOptions{dumpOptions};

      innerOptions.NeedsLeftAlign = false;
      innerOptions.NeedsTrailingNewline = false;
      innerOptions.NeedsTrailingSemi = false;
      AstMethod::DumpNode(dumper, innerOptions);
    }
    if (m_isDefault)
    {
      dumper->InsertWhitespace();
      dumper->InsertPunctuation('=');
      dumper->InsertWhitespace();
      dumper->InsertKeyword("default");
    }
    if (m_isDeleted)
    {
      dumper->InsertWhitespace();
      dumper->InsertPunctuation('=');
      dumper->InsertWhitespace();
      dumper->InsertKeyword("delete");
    }
    if (dumpOptions.NeedsTrailingSemi)
    {
      dumper->InsertPunctuation(';');
    }
    if (dumpOptions.NeedsTrailingNewline)
    {
      dumper->Newline();
    }
  }
};

class AstDestructor : public AstMethod {
  bool m_isDefault{false};
  bool m_isDeleted{false};
  bool m_isExplicitlyDefaulted{false};

public:
  AstDestructor(
      CXXDestructorDecl const* dtor,
      AzureClassesDatabase* const database,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
      : AstMethod(dtor, database, parentNode), m_isDefault{dtor->isDefaulted()},
        m_isDeleted{dtor->isDeleted()}, m_isExplicitlyDefaulted{dtor->isExplicitlyDefaulted()}
  {
  }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override
  {
    if (dumpOptions.NeedsLeftAlign)
    {
      dumper->LeftAlign();
    }
    {
      DumpNodeOptions innerOptions{dumpOptions};

      innerOptions.NeedsLeftAlign = false;
      innerOptions.NeedsTrailingNewline = false;
      innerOptions.NeedsTrailingSemi = false;
      AstMethod::DumpNode(dumper, innerOptions);
    }
    if (m_isDefault)
    {
      dumper->InsertWhitespace();
      dumper->InsertPunctuation('=');
      dumper->InsertWhitespace();
      dumper->InsertKeyword("default");
    }
    if (m_isDeleted)
    {
      dumper->InsertWhitespace();
      dumper->InsertPunctuation('=');
      dumper->InsertWhitespace();
      dumper->InsertKeyword("delete");
    }
    if (dumpOptions.NeedsTrailingSemi)
    {
      dumper->InsertPunctuation(';');
    }
    if (dumpOptions.NeedsTrailingNewline)
    {
      dumper->Newline();
    }
  }
};

class AstAccessSpec : public AstNode {
  AccessSpecifier m_accessSpecifier;

public:
  AstAccessSpec(clang::AccessSpecDecl const* accessSpec, AzureClassesDatabase* const)
      : AstNode(accessSpec), m_accessSpecifier{accessSpec->getAccess()}
  {
  }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override;
};

void AstAccessSpec::DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions)
{
  // We want to left-indent the "public:", "private:" and "protected" items so they stick
  // out from the fields in the class.
  dumper->AdjustIndent(-2);
  if (dumpOptions.NeedsLeftAlign)
  {
    dumper->LeftAlign();
  }
  dumper->InsertKeyword(AccessSpecifierToString(m_accessSpecifier));
  dumper->InsertPunctuation(':');
  dumper->AdjustIndent(2);
  dumper->Newline();
}

/**
 * Represents an AST class or structure.
 */
class AstClassLike : public AstNamedNode {
  bool m_isFinal{};
  bool m_hasDefinition{};
  bool m_isForwardDeclaration{};
  bool m_isAnonymousNamedStruct{};
  TagDecl::TagKind m_tagUsed;
  std::string m_anonymousNamedStructName;

  std::vector<std::unique_ptr<AstBaseClass>> m_baseClasses;
  std::vector<std::unique_ptr<AstNode>> m_children;

private:
  void DumpTag(AstDumper* dumper, DumpNodeOptions const& options)
  {
    switch (m_tagUsed)
    {
        /// The "struct" keyword.
      case TTK_Struct:
        dumper->InsertKeyword("struct");
        break;
        /// The "__interface" keyword.
      case TTK_Interface:
        dumper->InsertKeyword("__interface");
        break;
        /// The "union" keyword.
      case TTK_Union:
        dumper->InsertKeyword("union");
        break;
        /// The "class" keyword.
      case TTK_Class:
        dumper->InsertKeyword("class");
        break;
        /// The "enum" keyword.
      case TTK_Enum:
        dumper->InsertKeyword("enum");
        break;
      default:
        throw std::runtime_error("Unknown tagKind: " + std::to_string(static_cast<int>(m_tagUsed)));
    }
  }
  virtual void DumpTemplateSpecializationArguments(
      AstDumper* dumper,
      DumpNodeOptions const& options)
  {
  }

public:
  AstClassLike(
      CXXRecordDecl const* decl,
      AzureClassesDatabase* const azureClassesDatabase,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode);
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override;
};

class AstClassTemplate : public AstNamedNode {
  std::vector<std::unique_ptr<AstNode>> m_parameters;
  std::unique_ptr<AstNode> m_templateBody;

public:
  AstClassTemplate(
      ClassTemplateDecl const* templateDecl,
      AzureClassesDatabase* const azureClassesDatabase,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
      : AstNamedNode(templateDecl, azureClassesDatabase, parentNode)
  {
    for (auto param : templateDecl->getTemplateParameters()->asArray())
    {
      m_parameters.push_back(AstNode::Create(param, azureClassesDatabase, parentNode));
    }
    m_templateBody
        = AstNode::Create(templateDecl->getTemplatedDecl(), azureClassesDatabase, parentNode);
  }

  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override
  {
    if (!Namespace().empty())
    {
      dumper->SetNamespace(Namespace());
    }

    if (dumpOptions.NeedsLeftAlign)
    {
      dumper->LeftAlign();
    }
    dumper->InsertKeyword("template");
    dumper->InsertWhitespace();
    dumper->InsertPunctuation('<');
    {
      DumpNodeOptions innerOptions{dumpOptions};
      innerOptions.NeedsLeadingNewline = false;

      DumpList(
          m_parameters.begin(),
          m_parameters.end(),
          dumper,
          innerOptions,
          [&](AstDumper* dumper, std::unique_ptr<AstNode>& param) {
            param->DumpNode(dumper, innerOptions);
          });
    }

    dumper->InsertPunctuation('>');
    dumper->Newline();
    {
      DumpNodeOptions innerOptions{dumpOptions};
      innerOptions.NeedsLeftAlign = true;
      innerOptions.NeedsLeadingNewline = false;
      m_templateBody->DumpNode(dumper, innerOptions);
    }
  }
};

class AstFunctionTemplate : public AstNamedNode {
  std::vector<std::unique_ptr<AstNode>> m_parameters;
  std::unique_ptr<AstNode> m_functionNode;

public:
  AstFunctionTemplate(
      FunctionTemplateDecl const* functionTemplate,
      AzureClassesDatabase* const azureClassesDatabase,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
      : AstNamedNode(functionTemplate, azureClassesDatabase, parentNode),
        m_functionNode{
            AstNode::Create(functionTemplate->getTemplatedDecl(), azureClassesDatabase, parentNode)}
  {
    for (auto param : functionTemplate->getTemplateParameters()->asArray())
    {
      m_parameters.push_back(AstNode::Create(param, azureClassesDatabase, parentNode));
    }
  }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override
  {
    if (!Namespace().empty())
    {
      if (dumpOptions.NeedsNamespaceAdjustment)
      {
        dumper->SetNamespace(Namespace());
      }
    }

    if (dumpOptions.NeedsLeftAlign)
    {
      dumper->LeftAlign();
    }
    dumper->InsertKeyword("template");
    dumper->InsertWhitespace();
    dumper->InsertPunctuation('<');
    DumpList(
        m_parameters.begin(),
        m_parameters.end(),
        dumper,
        dumpOptions,
        [&](AstDumper* dumper, std::unique_ptr<AstNode>& param) {
          param->DumpNode(dumper, dumpOptions);
        });
    dumper->InsertPunctuation('>');
    dumper->Newline();
    m_functionNode->DumpNode(dumper, dumpOptions);
  }
};

class AstTypeAliasTemplate : public AstNamedNode {
  std::vector<std::unique_ptr<AstNode>> m_parameters;
  std::unique_ptr<AstNode> m_typeAliasNode;

public:
  AstTypeAliasTemplate(
      TypeAliasTemplateDecl const* typeAliasTemplate,
      AzureClassesDatabase* const azureClassesDatabase,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
      : AstNamedNode(typeAliasTemplate, azureClassesDatabase, parentNode),
        m_typeAliasNode{AstNode::Create(
            typeAliasTemplate->getTemplatedDecl(),
            azureClassesDatabase,
            parentNode)}
  {
    for (auto param : typeAliasTemplate->getTemplateParameters()->asArray())
    {
      m_parameters.push_back(AstNode::Create(param, azureClassesDatabase, parentNode));
      if (!m_parameters.back())
      {
        llvm::outs() << raw_ostream::Colors::CYAN << "Unknown or unsupported AST node type."
                     << raw_ostream::Colors::RESET << "\n";
        param->dump(llvm::outs());
      }
    }
  }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override
  {
    if (!Namespace().empty())
    {
      if (dumpOptions.NeedsNamespaceAdjustment)
      {
        dumper->SetNamespace(Namespace());
      }
    }

    if (dumpOptions.NeedsLeftAlign)
    {
      dumper->LeftAlign();
    }
    dumper->InsertKeyword("template");
    dumper->InsertWhitespace();
    dumper->InsertPunctuation('<');
    {
      DumpNodeOptions innerOptions{dumpOptions};
      innerOptions.NeedsLeadingNewline = false;

      DumpList(
          m_parameters.begin(),
          m_parameters.end(),
          dumper,
          innerOptions,
          [&](AstDumper* dumper, std::unique_ptr<AstNode>& param) {
            param->DumpNode(dumper, innerOptions);
          });
    }
    dumper->InsertPunctuation('>');
    dumper->Newline();
    m_typeAliasNode->DumpNode(dumper, dumpOptions);
  }
};

class AstClassTemplateSpecialization : public AstClassLike {
  std::vector<std::unique_ptr<AstType>> m_arguments;

  virtual void DumpTemplateSpecializationArguments(
      AstDumper* dumper,
      DumpNodeOptions const& dumpOptions) override
  {
    dumper->InsertPunctuation('<');
    for (auto const& arg : m_arguments)
    {
      arg->Dump(dumper, dumpOptions);
    }
    dumper->InsertPunctuation('>');
  }

public:
  AstClassTemplateSpecialization(
      ClassTemplateSpecializationDecl const* templateDecl,
      AzureClassesDatabase* const azureClassesDatabase,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
      : AstClassLike(templateDecl, azureClassesDatabase, parentNode)

  {
    for (const auto& arg : templateDecl->getTemplateArgs().asArray())
    {
      m_arguments.push_back(std::make_unique<AstType>(arg.getAsType()));
    }
  }

  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override
  {
    if (!Namespace().empty())
    {
      if (dumpOptions.NeedsNamespaceAdjustment)
      {
        dumper->SetNamespace(Namespace());
      }
    }

    if (dumpOptions.NeedsLeftAlign)
    {
      dumper->LeftAlign();
    }
    dumper->InsertKeyword("template");
    dumper->InsertWhitespace();
    dumper->InsertPunctuation('<');
    dumper->InsertPunctuation('>');
    AstClassLike::DumpNode(dumper, dumpOptions);
  }
};

class AstConversion : public AstNamedNode {
  bool m_isExplicit;
  bool m_isConstexpr;
  AstType m_conversionType;

public:
  AstConversion(
      CXXConversionDecl const* conversion,
      AzureClassesDatabase* const azureClassesDatabase,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
      : AstNamedNode(conversion, azureClassesDatabase, parentNode),
        m_isExplicit{conversion->isExplicit()}, m_isConstexpr{conversion->isConstexpr()},
        m_conversionType{conversion->getConversionType(), conversion->getASTContext()}
  {
  }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override
  {
    if (dumpOptions.NeedsLeftAlign)
    {
      dumper->LeftAlign();
    }
    if (m_isConstexpr)
    {
      dumper->InsertKeyword("constexpr");
      dumper->InsertWhitespace();
    }
    if (m_isExplicit)
    {
      dumper->InsertKeyword("explicit");
      dumper->InsertWhitespace();
    }
    dumper->InsertKeyword("operator");
    dumper->InsertWhitespace();
    m_conversionType.Dump(dumper, dumpOptions);
    dumper->InsertPunctuation('(');
    dumper->InsertPunctuation(')');
    if (dumpOptions.NeedsTrailingSemi)
    {
      dumper->InsertPunctuation(';');
    }
    if (dumpOptions.NeedsTrailingNewline)
    {
      dumper->Newline();
    }
  };
};

class AstField : public AstNamedNode {
  AstType m_fieldType;
  std::unique_ptr<AstExpr> m_initializer;
  InClassInitStyle m_classInitializerStyle;
  bool m_hasDefaultMemberInitializer{};
  bool m_isMutable{};
  bool m_isConst{};

public:
  AstField(
      FieldDecl const* fieldDecl,
      AzureClassesDatabase* const azureClassesDatabase,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
      : AstNamedNode(fieldDecl, azureClassesDatabase, parentNode),
        m_fieldType{fieldDecl->getType()}, m_initializer{AstExpr::Create(
                                               fieldDecl->getInClassInitializer(),
                                               fieldDecl->getASTContext())},
        m_classInitializerStyle{fieldDecl->getInClassInitStyle()},
        m_hasDefaultMemberInitializer{fieldDecl->hasInClassInitializer()},
        m_isMutable{fieldDecl->isMutable()}, m_isConst{fieldDecl->getType().isConstQualified()}
  {
  }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override;
};

class AstFriend : public AstNode {
  std::string m_friendType;
  std::unique_ptr<AstNode> m_friendFunction;

public:
  AstFriend(
      FriendDecl const* friendDecl,
      AzureClassesDatabase* const azureClassesDatabase,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
      : AstNode(friendDecl)
  {
    if (friendDecl->getFriendType())
    {
      m_friendType
          = QualType::getAsString(friendDecl->getFriendType()->getType().split(), LangOptions{});
    }
    else if (friendDecl->getFriendDecl())
    {
      m_friendFunction
          = AstNode::Create(friendDecl->getFriendDecl(), azureClassesDatabase, parentNode);
    }
  }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override
  {
    if (dumpOptions.NeedsLeftAlign)
    {
      dumper->LeftAlign();
    }
    dumper->InsertKeyword("friend");
    dumper->InsertWhitespace();
    if (m_friendFunction)
    {
      DumpNodeOptions innerOptions{dumpOptions};
      innerOptions.NeedsLeftAlign = false;
      innerOptions.NeedsNamespaceAdjustment = false;
      //      innerOptions.IncludeContainingClass = true;
      //      innerOptions.IncludeNamespace = true;
      m_friendFunction->DumpNode(dumper, innerOptions);
    }
    else
    {
      dumper->InsertTypeName(m_friendType, m_friendType);
      if (dumpOptions.NeedsTrailingSemi)
      {
        dumper->InsertPunctuation(';');
      }
      if (dumpOptions.NeedsTrailingNewline)
      {
        dumper->Newline();
      }
    }
  }
};

class AstEnumerator : public AstNamedNode {
  std::unique_ptr<AstExpr> m_initializer;

public:
  AstEnumerator(
      EnumConstantDecl const* enumerator,
      AzureClassesDatabase* const azureClassesDatabase,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
      : AstNamedNode(enumerator, azureClassesDatabase, parentNode)
  {
    if (enumerator->getInitExpr())
    {
      m_initializer = AstExpr::Create(enumerator->getInitExpr(), enumerator->getASTContext());
    }
  }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override
  {
    dumper->LeftAlign();
    dumper->InsertMemberName(Name());
    if (m_initializer)
    {
      dumper->InsertWhitespace();
      dumper->InsertPunctuation('=');
      dumper->InsertWhitespace();
      m_initializer->Dump(dumper, dumpOptions);
    }
  }
};

class AstEnum : public AstNamedNode {
  //  std::vector<std::tuple<std::string, std::unique_ptr<AstExpr>>> m_enumerators;
  std::vector<std::unique_ptr<AstNode>> m_enumerators;
  std::string m_underlyingType;
  bool m_isScoped;
  bool m_isScopedWithClass;
  bool m_isFixed;

public:
  AstEnum(
      EnumDecl const* enumDecl,
      AzureClassesDatabase* const azureClassesDatabase,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
      : AstNamedNode(enumDecl, azureClassesDatabase, parentNode),
        m_underlyingType{enumDecl->getIntegerType().getAsString()},
        m_isScoped{enumDecl->isScoped()},
        m_isScopedWithClass{enumDecl->isScopedUsingClassTag()}, m_isFixed{enumDecl->isFixed()}
  {
    if (!m_isScoped)
    {
      azureClassesDatabase->CreateApiViewMessage(
          ApiViewMessages::UnscopedEnumeration, m_navigationId);
    }
    // All the types created under this node use a newly created node for their parent.
    if (parentNode)
    {
      parentNode = parentNode->InsertChildNode(
          Name(), m_navigationId, TypeHierarchy::TypeHierarchyClass::Enum);
    }
    for (auto enumerator : enumDecl->enumerators())
    {
      m_enumerators.push_back(AstNode::Create(enumerator, azureClassesDatabase, parentNode));
    }
  }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override;
};

AstClassLike::AstClassLike(
    CXXRecordDecl const* decl,
    AzureClassesDatabase* const azureClassesDatabase,
    std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
    : AstNamedNode(decl, azureClassesDatabase, parentNode), m_tagUsed{decl->getTagKind()},
      m_hasDefinition{decl->hasDefinition()}, m_isForwardDeclaration{decl != decl->getDefinition()}
{
  // All the types created under this node use a newly created node for their parent.
  TypeHierarchy::TypeHierarchyClass classType;
  switch (m_tagUsed)
  {
    case TagDecl::TagKind::TTK_Class:
      classType = TypeHierarchy::TypeHierarchyClass::Class;
      break;
    case TagDecl::TagKind::TTK_Enum:
      classType = TypeHierarchy::TypeHierarchyClass::Enum;
      break;
    case TagDecl::TagKind::TTK_Interface:
      classType = TypeHierarchy::TypeHierarchyClass::Interface;
      break;
    case TagDecl::TagKind::TTK_Struct:
      classType = TypeHierarchy::TypeHierarchyClass::Struct;
      break;
    case TagDecl::TagKind::TTK_Union:
      classType = TypeHierarchy::TypeHierarchyClass::Unknown;
      break;
    default:
      break;
  }
  // We want to special case anonymous structures which are embedded in another type. It's
  // possible that the following declaration is a field declaration referencing the
  // anonymous structure:
  //
  // struct Foo {
  //   int Field1;
  //   struct {
  //     bool InnerField1;
  //   } InnerStruct
  // };
  //
  // This is parsed as an anonymous struct containing a single field named "InnerField1"
  // followed by a field declaration referencing the anonymous struct.
  if (Name().empty() && decl->isEmbeddedInDeclarator()
      && decl->getNextDeclInContext()->getKind() == Decl::Kind::Field)
  {
    m_isAnonymousNamedStruct = true;
    m_anonymousNamedStructName = cast<FieldDecl>(decl->getNextDeclInContext())->getNameAsString();
  }
  if (parentNode)
  {
    parentNode = parentNode->InsertChildNode(
        !m_isAnonymousNamedStruct ? Name() : m_anonymousNamedStructName, m_navigationId, classType);
  }

  for (auto& attr : decl->attrs())
  {
    switch (attr->getKind())
    {
      case attr::Final:
        m_isFinal = true;
        break;
        // This is an implicit attribute that won't ever appear explicitly, so we can
        // ignore it.
      case attr::MaxFieldAlignment:
        break;
      default:
        llvm::outs() << "Unknown Attribute: ";
        attr->printPretty(llvm::outs(), LangOptions());
        llvm::outs() << "\n";
        break;
    }
  }

  if (decl->hasDefinition())
  {
    for (auto const& base : decl->bases())
    {
      m_baseClasses.push_back(std::make_unique<AstBaseClass>(base));
    }
    bool shouldSkipNextChild = false;
    for (auto child : decl->decls())
    {
      // We want to ignore any and all auto-generated types - we only care about explictly
      // mentioned types.
      bool shouldIncludeChild = !child->isImplicit();
      // If the child is private and we're not including private types, don't include it.
      if (shouldIncludeChild)
      {
        if (child->getAccess() == AS_private) //&& !options.IncludePrivate)
        {
          shouldIncludeChild = false;
        }
      }
      if (shouldIncludeChild)
      {
        if (shouldSkipNextChild)
        {
          shouldIncludeChild = false;
          shouldSkipNextChild = false;
        }
      }

      // If the class is final, don't include protected fields.
      if (shouldIncludeChild)
      {
      }
      if (shouldIncludeChild)
      {
        switch (child->getKind())
        {
          case Decl::Kind::Var: {
            m_children.push_back(AstNode::Create(child, azureClassesDatabase, parentNode));
            break;
          }
          case Decl::Kind::CXXRecord: {
            m_children.push_back(std::make_unique<AstClassLike>(
                cast<CXXRecordDecl>(child), azureClassesDatabase, parentNode));
            // For an anonymous named structure, we want to skip the next field because
            // it's been embedded in the anonymous struct definition.
            if (static_cast<AstClassLike*>(m_children.back().get())->m_isAnonymousNamedStruct)
            {
              assert(child->getNextDeclInContext()->getKind() == Decl::Kind::Field);
              shouldSkipNextChild = true;
            }
            break;
          }
          case Decl::Kind::CXXMethod: {
            m_children.push_back(AstNode::Create(child, azureClassesDatabase, parentNode));
            break;
          }
          case Decl::Kind::CXXConstructor: {

            m_children.push_back(AstNode::Create(child, azureClassesDatabase, parentNode));
            break;
          }
          case Decl::Kind::CXXDestructor: {
            m_children.push_back(AstNode::Create(child, azureClassesDatabase, parentNode));
            break;
          }
          case Decl::Kind::Field: {
            m_children.push_back(AstNode::Create(child, azureClassesDatabase, parentNode));
            break;
          }
          case Decl::Kind::AccessSpec: {
            m_children.push_back(AstNode::Create(child, azureClassesDatabase, parentNode));
            break;
          }
          case Decl::Kind::FunctionTemplate: {
            m_children.push_back(AstNode::Create(child, azureClassesDatabase, parentNode));
            break;
          }
          case Decl::Kind::Friend: {
            m_children.push_back(AstNode::Create(child, azureClassesDatabase, parentNode));
            break;
          }
          case Decl::Kind::Enum: {
            m_children.push_back(AstNode::Create(child, azureClassesDatabase, parentNode));
            break;
          }
          case Decl::Kind::TypeAlias: {
            m_children.push_back(AstNode::Create(child, azureClassesDatabase, parentNode));
            break;
          }
          case Decl::Kind::CXXConversion: {
            m_children.push_back(AstNode::Create(child, azureClassesDatabase, parentNode));
            break;
          }
          case Decl::Kind::StaticAssert: {
            // static_assert nodes are generated after the preprocessor and they don't
            // really add any value to the ApiView.
            break;
          }
          default: {
            llvm::errs() << raw_ostream::Colors::RED
                         << "Unhandled Decl Type: " << std::string(child->getDeclKindName())
                         << raw_ostream::Colors::RESET << "\n";
          }
        }
        if (m_isFinal && child->getAccess() == AS_protected)
        {
          // protected types in final classes should be flagged.
          if (child->getKind() != Decl::AccessSpec && cast<NamedDecl>(child)->getIdentifier())
          {
            azureClassesDatabase->CreateApiViewMessage(
                ApiViewMessages::ProtectedFieldsInFinalClass,
                cast<NamedDecl>(child)->getQualifiedNameAsString());
          }
        }
      }
    }
  }
}

void AstClassLike::DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions)
{
  if (!Namespace().empty())
  {
    if (dumpOptions.NeedsNamespaceAdjustment)
    {
      dumper->SetNamespace(Namespace());
    }
  }

  // If we're a templated class, don't insert the extra newline before the class
  // definition.
  if (dumpOptions.NeedsLeadingNewline)
  {
    dumper->Newline();
  }
  if (dumpOptions.NeedsLeftAlign)
  {
    dumper->LeftAlign();
  }
  DumpTag(dumper, dumpOptions);
  dumper->InsertWhitespace();
  dumper->InsertTypeName(Name(), m_navigationId);
  DumpTemplateSpecializationArguments(dumper, dumpOptions);
  if (!m_isForwardDeclaration)
  {
    if (m_hasDefinition)
    {
      if (m_isFinal)
      {
        dumper->InsertWhitespace();
        dumper->InsertKeyword("final");
      }
      if (!m_baseClasses.empty())
      {
        dumper->InsertWhitespace();
        dumper->InsertPunctuation(':');
        dumper->InsertWhitespace();
        // Enumerate the base classes, dumping each of them.
        DumpList(
            m_baseClasses.begin(),
            m_baseClasses.end(),
            dumper,
            dumpOptions,
            [&](AstDumper* dumper, std::unique_ptr<AstBaseClass>& base) {
              base->DumpNode(dumper, dumpOptions);
            });
      }

      dumper->Newline();
      dumper->LeftAlign();
      dumper->InsertPunctuation('{');
      dumper->AdjustIndent(2);
      dumper->Newline();
      for (auto const& child : m_children)
      {
        assert(child);
        DumpNodeOptions innerOptions{dumpOptions};
        innerOptions.NeedsLeadingNewline = false;
        child->DumpNode(dumper, innerOptions);
      }
      dumper->AdjustIndent(-2);
      dumper->LeftAlign();
      dumper->InsertPunctuation('}');
    }
    if (m_isAnonymousNamedStruct && !m_anonymousNamedStructName.empty())
    {
      dumper->InsertWhitespace();
      dumper->InsertTypeName(m_anonymousNamedStructName, m_navigationId);
    }
  }
  if (dumpOptions.NeedsTrailingSemi)
  {
    dumper->InsertPunctuation(';');
  }
  if (dumpOptions.NeedsTrailingNewline)
  {
    dumper->Newline();
  }
}

void AstEnum::DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions)
{
  if (!Namespace().empty())
  {
    if (dumpOptions.NeedsNamespaceAdjustment)
    {
      dumper->SetNamespace(Namespace());
    }
  }
  if (dumpOptions.NeedsLeftAlign)
  {
    dumper->LeftAlign();
  }
  dumper->InsertKeyword("enum");
  dumper->InsertWhitespace();
  if (m_isScoped)
  {
    if (m_isScopedWithClass)
    {
      dumper->InsertKeyword("class");
    }
    else
    {
      dumper->InsertKeyword("struct");
    }
  }
  dumper->InsertWhitespace();
  dumper->InsertTypeName(Name(), m_navigationId);
  if (m_isFixed)
  {
    dumper->InsertWhitespace();
    dumper->InsertPunctuation(':');
    dumper->InsertWhitespace();
    dumper->InsertMemberName(m_underlyingType);
  }
  dumper->Newline();
  dumper->LeftAlign();
  dumper->InsertPunctuation('{');
  dumper->AdjustIndent(2);
  dumper->Newline();

  {
    DumpNodeOptions innerOptions{dumpOptions};
    innerOptions.NeedsLeadingNewline = true;
    DumpList(
        m_enumerators.begin(),
        m_enumerators.end(),
        dumper,
        innerOptions,
        [&](AstDumper* dumper, std::unique_ptr<AstNode>& enumerator) {
          enumerator->DumpNode(dumper, dumpOptions);
        });
  }
  dumper->Newline();
  dumper->AdjustIndent(-2);
  dumper->LeftAlign();
  dumper->InsertPunctuation('}');
  if (dumpOptions.NeedsTrailingSemi)
  {
    dumper->InsertPunctuation(';');
  }
  if (dumpOptions.NeedsTrailingNewline)
  {
    dumper->Newline();
  }
}

void AstField::DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions)
{
  if (dumpOptions.NeedsLeftAlign)
  {
    dumper->LeftAlign();
  }
  m_fieldType.Dump(dumper, dumpOptions);
  dumper->InsertWhitespace();
  dumper->InsertMemberName(Name());
  // if (m_initializer)
  //{
  //   DumpNodeOptions innerOptions{dumpOptions};
  //   if (m_classInitializerStyle == ICIS_CopyInit)
  //   {
  //     dumper->InsertWhitespace();
  //     dumper->InsertPunctuation('=');
  //     dumper->InsertWhitespace();
  //   }
  //   else if (m_classInitializerStyle == ICIS_ListInit)
  //   {
  //     innerOptions.DumpListInitializer = true;
  //   }
  //   m_initializer->Dump(dumper, innerOptions);
  // }
  if (dumpOptions.NeedsTrailingSemi)
  {
    dumper->InsertPunctuation(';');
  }
  if (dumpOptions.NeedsTrailingNewline)
  {
    dumper->Newline();
  }
}

void AstType::Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const
{
  dumper->InsertMemberName(m_internalTypeName);
}

void AstExpr::Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const
{
  dumper->InsertComment("/* UNSUPPORTED EXPRESSION */");
}
void AstStatement::Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const
{
  dumper->InsertComment("/* UNSUPPORTED STATEMENT */");
}
void AstMethodCallExpr::Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const
{
  // Dump the class and member field to be called.
  m_memberAccessor->Dump(dumper, dumpOptions);
}
void AstMemberExpr::Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const
{
  if (dumpOptions.DumpListInitializer)
  {
    dumper->InsertPunctuation('{');
  }
  m_member->Dump(dumper, dumpOptions);
  dumper->InsertPunctuation('.');
  dumper->InsertMemberName(m_memberMethod);
  dumper->InsertPunctuation('(');
  dumper->InsertPunctuation(')');
  if (dumpOptions.DumpListInitializer)
  {
    dumper->InsertPunctuation('}');
  }
}

void AstInitializerList::Dump(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const
{
  if (!m_initializerValues.empty())
  {
    if (IsEmptyExpression())
    {
      dumper->InsertPunctuation('{');
      dumper->InsertPunctuation('}');
    }
    else
    {

      // If the initializer list has multiple values, dump them as a list, one per line.
      if (m_initializerValues.size() != 1)
      {
        dumper->InsertPunctuation('{');
        dumper->AdjustIndent(4);
        DumpList(
            m_initializerValues.begin(),
            m_initializerValues.end(),
            dumper,
            dumpOptions,
            [&](AstDumper* dumper, std::unique_ptr<AstExpr> const& initializer) {
              dumper->Newline();
              dumper->LeftAlign();
              initializer->Dump(dumper, dumpOptions);
            });
        dumper->AdjustIndent(-4);
        dumper->InsertPunctuation('}');
      }
      else
      {
        // If the initializer list has only a single value, just dump it.
        m_initializerValues.front()->Dump(dumper, dumpOptions);
      }
    }
  }
}

std::unique_ptr<AstNode> AstNode::Create(
    clang::Decl const* decl,
    AzureClassesDatabase* const azureClassesDatabase,
    std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode)
{
  switch (decl->getKind())
  {
    case Decl::Kind::CXXConstructor:
      return std::make_unique<AstConstructor>(
          cast<CXXConstructorDecl>(decl), azureClassesDatabase, parentNode);

    case Decl::Kind::CXXDestructor:
      return std::make_unique<AstDestructor>(
          cast<CXXDestructorDecl>(decl), azureClassesDatabase, parentNode);
    case Decl::Kind::CXXConversion:
      return std::make_unique<AstConversion>(
          cast<CXXConversionDecl>(decl), azureClassesDatabase, parentNode);

    case Decl::Kind::CXXMethod:
      return std::make_unique<AstMethod>(
          cast<CXXMethodDecl>(decl), azureClassesDatabase, parentNode);

    case Decl::Kind::Function:
      return std::make_unique<AstFunction>(
          cast<FunctionDecl>(decl), azureClassesDatabase, parentNode);

    case Decl::Kind::ParmVar: {
      return std::make_unique<AstParamVariable>(
          cast<ParmVarDecl>(decl), azureClassesDatabase, parentNode);
    }
    case Decl::Kind::Var:
      return std::make_unique<AstVariable>(cast<VarDecl>(decl), azureClassesDatabase, parentNode);

    case Decl::Kind::ClassTemplateSpecialization:
      return std::make_unique<AstClassTemplateSpecialization>(
          cast<ClassTemplateSpecializationDecl>(decl), azureClassesDatabase, parentNode);

    case Decl::Kind::Enum:
      return std::make_unique<AstEnum>(cast<EnumDecl>(decl), azureClassesDatabase, parentNode);

    case Decl::Kind::EnumConstant:
      return std::make_unique<AstEnumerator>(
          cast<EnumConstantDecl>(decl), azureClassesDatabase, parentNode);

    case Decl::Kind::Field:
      return std::make_unique<AstField>(cast<FieldDecl>(decl), azureClassesDatabase, parentNode);

    case Decl::Kind::FunctionTemplate:
      return std::make_unique<AstFunctionTemplate>(
          cast<FunctionTemplateDecl>(decl), azureClassesDatabase, parentNode);

    case Decl::Kind::ClassTemplate:
      return std::make_unique<AstClassTemplate>(
          cast<ClassTemplateDecl>(decl), azureClassesDatabase, parentNode);

    case Decl::Kind::TemplateTypeParm:
      return std::make_unique<AstTemplateParameter>(
          cast<TemplateTypeParmDecl>(decl), azureClassesDatabase, parentNode);

    case Decl::Kind::TemplateTemplateParm:
      return std::make_unique<AstTemplateTemplateParameter>(
          cast<TemplateTemplateParmDecl>(decl), azureClassesDatabase, parentNode);

    case Decl::Kind::NonTypeTemplateParm:
      return std::make_unique<AstNonTypeTemplateParam>(
          cast<NonTypeTemplateParmDecl>(decl), azureClassesDatabase, parentNode);

    case Decl::Kind::TypeAliasTemplate:
      return std::make_unique<AstTypeAliasTemplate>(
          cast<TypeAliasTemplateDecl>(decl), azureClassesDatabase, parentNode);

    case Decl::Kind::TypeAlias:
      return std::make_unique<AstTypeAlias>(
          cast<TypeAliasDecl>(decl), azureClassesDatabase, parentNode);

    case Decl::Kind::CXXRecord:
      return std::make_unique<AstClassLike>(
          cast<CXXRecordDecl>(decl), azureClassesDatabase, parentNode);

    case Decl::Kind::AccessSpec:
      return std::make_unique<AstAccessSpec>(cast<AccessSpecDecl>(decl), azureClassesDatabase);

    case Decl::Kind::Friend:
      return std::make_unique<AstFriend>(cast<FriendDecl>(decl), azureClassesDatabase, parentNode);

    case Decl::Kind::NamespaceAlias:
      return nullptr;
      //    return std::make_unique<AstNamespaceAlias>(cast<NamespaceAliasDecl>(decl,
      //    azureClassesDatabase));

    case Decl::Kind::Namespace:
      return nullptr;
      //    return std::make_unique<AstNamespace>(cast<NamespaceDecl>(decl,
      //    azureClassesDatabase));
    case Decl::Kind::Using:
      return nullptr;
      //   return std::make_unique<AstUsing>(cast<UsingDecl>(decl));
    default: {
      llvm::errs() << raw_ostream::Colors::RED << "Unknown DECL node "
                   << cast<NamedDecl>(decl)->getNameAsString()
                   << " type : " << decl->getDeclKindName() << raw_ostream::Colors::RESET << "\n ";
      return nullptr;
    }
  }
}

std::string AstNode::GetNamespaceForDecl(Decl const* decl)
{
  auto typeNamespace{decl->getDeclContext()->getEnclosingNamespaceContext()};
  if (typeNamespace->isNamespace())
  {
    return cast<NamespaceDecl>(typeNamespace)->getQualifiedNameAsString();
  }
  return "";
}

// Classes database implementation.

bool AzureClassesDatabase::IsMemberOfObject(NamedDecl const* decl)
{
  if (decl->isCXXClassMember())
  {
    return true;
  }

  // If this object is the target of a friend declaration, then there is a friend
  // declaration that actually defines the object.
  if (decl->getFriendObjectKind() != clang::Decl::FOK_None)
  {
    return true;
  }

  // Not strictly true, but if this decl has a describing template, then it's covered by
  // another node type.
  if (decl->getDescribedTemplate() != nullptr)
  {
    return true;
  }

  // Method or template parameters or enumerators are by definition members of an object.
  if (decl->getKind() == Decl::ParmVar || decl->getKind() == Decl::EnumConstant
      || decl->getKind() == Decl::TemplateTypeParm || decl->getKind() == Decl::NonTypeTemplateParm
      || decl->getKind() == Decl::TemplateTemplateParm)
  {
    return true;
  }
  // Local variables are obviously members of objects.
  if (decl->getKind() == Decl::Var)
  {
    if (cast<VarDecl>(decl)->isLocalVarDecl())
    {
      return true;
    }
  }

  // If this node has a parent function or method, it's a member of something.
  auto parent = decl->getParentFunctionOrMethod();
  if (parent)
  {
    return true;
  }

  return false;
}

void AzureClassesDatabase::CreateAstNode()
{
  // Create a terminal node to force closing of all outstanding namespaces.
  m_typeList.push_back(std::make_unique<AstTerminalNode>());
}

void AzureClassesDatabase::CreateAstNode(clang::NamedDecl* namedDecl)
{
  if (m_processor->IncludePrivate() || namedDecl->getAccess() != AS_private)
  {
    // Perform a couple of verification checks that should apply to every type we add to the API
    // Review, regardless of the type of object:
    //
    // 1) If there's a namespace filter specified, flag all types outside the namespace filter.
    // 2) If the type is in the _internal namespace, flag it if we're not allowed to have _internal
    // types.
    //
    // We don't want to consider namespaces in these checks, since they're not discrete entries in
    // our resulting output.
    if (namedDecl->getKind() != Decl::Namespace)
    {
      const std::string typeName{namedDecl->getQualifiedNameAsString()};
      if (!m_processor->FilterNamespaces().empty())
      {
        // We have a namespace filter set. Verify that the type name starts with one of the filter
        // namespaces.
        if (std::find_if(
                std::begin(m_processor->FilterNamespaces()),
                std::end(m_processor->FilterNamespaces()),
                [&typeName](std::string const& ns) { return typeName.find(ns) == 0; })
            == std::end(m_processor->FilterNamespaces()))
        {
          bool generateError = true;
          // Don't flag "using" declarations or forward declarations, because they don't introduce
          // new types.
          if ((namedDecl->getKind() == Decl::Using) || (namedDecl->getKind() == Decl::TypeAlias)
              || (namedDecl->getKind() == Decl::CXXRecord
                  && cast<CXXRecordDecl>(namedDecl)->getDefinition() != namedDecl))
          {
            generateError = false;
          }
          if (generateError)
          {
            m_processor->GetClassesDatabase()->CreateApiViewMessage(
                ApiViewMessages::TypeDeclaredInNamespaceOutsideFilter, typeName);
          }
        }
      }

      // If the type is in the _internal namespace, then we want to flag it unless we are
      // allowing internal types.
      if (typeName.find("::_internal") != std::string::npos)
      {
        if (!m_processor->AllowInternal())
        {
          m_processor->GetClassesDatabase()->CreateApiViewMessage(
              ApiViewMessages::InternalTypesInNonCorePackage, typeName);
        }
      }
    }

    // Now create the node. Note that for some types AstNode::Create will return null, so we skip
    // adding them before we add them.
    auto node = AstNode::Create(
        namedDecl, this, m_typeHierarchy.GetNamespaceRoot(AstNode::GetNamespaceForDecl(namedDecl)));
    if (node)
    {
      m_typeList.push_back(std::move(node));
    }
  }
}
