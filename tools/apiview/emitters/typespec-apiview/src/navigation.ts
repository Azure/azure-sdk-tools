import {
  AliasStatementNode,
  EnumStatementNode,
  InterfaceStatementNode,
  IntersectionExpressionNode,
  ModelExpressionNode,
  ModelStatementNode,
  OperationStatementNode,
  ProjectionModelExpressionNode,
  ScalarStatementNode,
  SyntaxKind,
  UnionExpressionNode,
  UnionStatementNode,
} from "@typespec/compiler";
import { ApiView, NamespaceStack } from "./apiview.js";
import { NamespaceModel } from "./namespace-model.js";

export class ApiViewNavigation {
  Text: string;
  NavigationId: string | undefined;
  ChildItems: ApiViewNavigation[];
  Tags: ApiViewNavigationTag;

  constructor(
    objNode:
      | AliasStatementNode
      | NamespaceModel
      | ModelStatementNode
      | OperationStatementNode
      | InterfaceStatementNode
      | EnumStatementNode
      | ModelExpressionNode
      | IntersectionExpressionNode
      | ProjectionModelExpressionNode
      | ScalarStatementNode
      | UnionStatementNode
      | UnionExpressionNode,
      stack: NamespaceStack
  ) {
    let obj;
    switch (objNode.kind) {
      case SyntaxKind.NamespaceStatement:
        stack.push(objNode.name);
        this.Text = objNode.name;
        this.Tags = { TypeKind: ApiViewNavigationKind.Module };
        const operationItems = new Array<ApiViewNavigation>();
        for (const node of objNode.operations.values()) {
          operationItems.push(new ApiViewNavigation(node, stack));
        }
        const resourceItems = new Array<ApiViewNavigation>();
        for (const node of objNode.resources.values()) {
          resourceItems.push(new ApiViewNavigation(node, stack));
        }
        const modelItems = new Array<ApiViewNavigation>();
        for (const node of objNode.models.values()) {
          modelItems.push(new ApiViewNavigation(node, stack));
        }
        const aliasItems = new Array<ApiViewNavigation>();
        for (const node of objNode.aliases.values()) {
            aliasItems.push(new ApiViewNavigation(node, stack));
        }
        this.ChildItems = [];
        if (operationItems.length) {
          this.ChildItems.push({ Text: "Operations", ChildItems: operationItems, Tags: { TypeKind: ApiViewNavigationKind.Method }, NavigationId: "" });
        }
        if (resourceItems.length) {
          this.ChildItems.push({ Text: "Resources", ChildItems: resourceItems, Tags: { TypeKind: ApiViewNavigationKind.Class }, NavigationId: "" });
        }
        if (modelItems.length) {
          this.ChildItems.push({ Text: "Models", ChildItems: modelItems, Tags: { TypeKind: ApiViewNavigationKind.Class }, NavigationId: "" });
        }
        if (aliasItems.length) {
            this.ChildItems.push({ Text: "Aliases", ChildItems: aliasItems, Tags: { TypeKind: ApiViewNavigationKind.Class }, NavigationId: "" });
        }
        break;
      case SyntaxKind.ModelStatement:
        obj = objNode as ModelStatementNode;
        stack.push(obj.id.sv);
        this.Text = obj.id.sv;
        this.Tags = { TypeKind: ApiViewNavigationKind.Class };
        this.ChildItems = [];
        break;
      case SyntaxKind.EnumStatement:
        obj = objNode as EnumStatementNode;
        stack.push(obj.id.sv);
        this.Text = obj.id.sv;
        this.Tags = { TypeKind: ApiViewNavigationKind.Enum };
        this.ChildItems = [];
        break;
      case SyntaxKind.OperationStatement:
        obj = objNode as OperationStatementNode;
        stack.push(obj.id.sv);
        this.Text = obj.id.sv;
        this.Tags = { TypeKind: ApiViewNavigationKind.Method };
        this.ChildItems = [];
        break;
      case SyntaxKind.InterfaceStatement:
        obj = objNode as InterfaceStatementNode;
        stack.push(obj.id.sv);
        this.Text = obj.id.sv;
        this.Tags = { TypeKind: ApiViewNavigationKind.Method };
        this.ChildItems = [];
        for (const child of obj.operations) {
          this.ChildItems.push(new ApiViewNavigation(child, stack));
        }
        break;
      case SyntaxKind.UnionStatement:
        obj = objNode as UnionStatementNode;
        stack.push(obj.id.sv);
        this.Text = obj.id.sv;
        this.Tags = { TypeKind: ApiViewNavigationKind.Enum };
        this.ChildItems = [];
        break;
      case SyntaxKind.AliasStatement:
        obj = objNode as AliasStatementNode;
        stack.push(obj.id.sv);
        this.Text = obj.id.sv;
        this.Tags = { TypeKind: ApiViewNavigationKind.Class };
        this.ChildItems = [];
        break;
      case SyntaxKind.ModelExpression:
        throw new Error(`Navigation unsupported for "ModelExpression".`);
      case SyntaxKind.IntersectionExpression:
        throw new Error(`Navigation unsupported for "IntersectionExpression".`);
      case SyntaxKind.ProjectionModelExpression:
        throw new Error(`Navigation unsupported for "ProjectionModelExpression".`);
      case SyntaxKind.ScalarStatement:
        obj = objNode as ScalarStatementNode;
        stack.push(obj.id.sv);
        this.Text = obj.id.sv;
        this.Tags = { TypeKind: ApiViewNavigationKind.Class };
        this.ChildItems = [];
        break;
      default:
        throw new Error(`Navigation unsupported for "${objNode.kind.toString()}".`);
    }
    this.NavigationId = stack.value();
    stack.pop();
  }
}

export interface ApiViewNavigationTag {
  TypeKind: ApiViewNavigationKind;
}

export const enum ApiViewNavigationKind {
  Class = "class",
  Enum = "enum",
  Method = "method",
  Module = "namespace",
  Package = "assembly",
}
