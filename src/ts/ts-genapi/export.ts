import {
    ApiModel,
    ApiItem,
    ApiItemKind,
    ApiDeclaredItem,
    ExcerptTokenKind
  } from '@microsoft/api-extractor-model';

import { writeFile } from 'fs';

import { IApiViewFile, IApiViewNavItem } from './models';
import { TokensBuilder } from './tokensBuilder';

function appendMembers(builder: TokensBuilder, navigation: IApiViewNavItem[], item: ApiItem)
{
    builder.lineId(item.canonicalReference.toString());
    builder.indent();
    if (item instanceof ApiDeclaredItem) {
        for (const token of item.excerptTokens) {
            if (token.kind === ExcerptTokenKind.Reference)
            {
                builder.typeReference(token.canonicalReference.toString(), token.text);
            }
            else
            {
                builder.splitAppend(token.text, item.canonicalReference.toString(), item.displayName);
            }
        }
    }

    var navigationItem: IApiViewNavItem;
    var typeKind: string;

    switch (item.kind)
    {
        case ApiItemKind.Interface:
        case ApiItemKind.Class:
            typeKind = item.kind.toLowerCase();
            break
        case ApiItemKind.TypeAlias:
            typeKind = "struct";
            break
    }

    if (typeKind)
    {
        navigationItem = {
            Text: item.displayName,
            NavigationId: item.canonicalReference.toString(),
            Tags: {
                "TypeKind": typeKind
            },
            ChildItems: []
        };
        navigation.push(navigationItem);
    }

    if (item.kind === ApiItemKind.Interface ||
        item.kind === ApiItemKind.Class)
    {
        if (item.members.length > 0)
        {
            builder
                .punct("{")
                .newline()
                .incIndent()
    
            for (const member of item.members) {
                appendMembers(builder,navigationItem.ChildItems, member);
            }

            builder
                .decIndent()
                .indent()
                .punct("}")
                .newline();
        }
        else
        {
            builder
                .punct("{")
                .space()
                .punct("}")
                .newline();
        }
    }
    else
    { 
        builder.newline();
    }
}

const apiModel = new ApiModel();
apiModel.loadPackage(process.argv[2]);

var navigation: IApiViewNavItem[] = [];
var builder = new TokensBuilder();

for (const modelPackage of apiModel.packages) {    
    for (const entryPoint of modelPackage.entryPoints) {
        for (const member of entryPoint.members) {
            appendMembers(builder, navigation, member);
        }
    }
}

var apiViewFile: IApiViewFile = {
    Name: apiModel.packages[0].name,
    Navigation: navigation,
    Tokens: builder.tokens
}

writeFile(process.argv[3], JSON.stringify(apiViewFile), err => {
    if (err) {
        console.error(err);
        return;
    };
})