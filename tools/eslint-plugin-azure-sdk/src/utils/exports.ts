import { Rule } from "eslint";
import { SourceFile, Symbol as TSSymbol } from "typescript";
import { ParserServices } from "@typescript-eslint/experimental-utils";

const getExports = (context: Rule.RuleContext): TSSymbol[] | undefined => {
  const parserServices: ParserServices = context.parserServices;
  if (parserServices.program === undefined) {
    return undefined;
  }

  const program = parserServices.program;
  const typeChecker = program.getTypeChecker();
  const sourceFile = program.getSourceFile(context.settings.main);
  if (sourceFile === undefined) {
    return undefined;
  }

  const symbol = typeChecker.getSymbolAtLocation(sourceFile);
  if (symbol === undefined) {
    return undefined;
  }

  const packageExports = typeChecker.getExportsOfModule(symbol);
  const exportSymbols = packageExports
    .map((packageExport: TSSymbol): TSSymbol | undefined => {
      return typeChecker.getDeclaredTypeOfSymbol(packageExport).getSymbol();
    })
    .filter((exportSymbol: TSSymbol | undefined): boolean => {
      return exportSymbol !== undefined;
    }) as TSSymbol[];
  return exportSymbols;
};

export const isExternal = (symbol: TSSymbol): boolean => {
  const externalRegex = /node_modules/;
  if (symbol.valueDeclaration !== undefined) {
    let parent = symbol.valueDeclaration.parent;
    while (parent.parent !== undefined) {
      parent = parent.parent;
    }
    const sourceFile = parent as SourceFile;
    return externalRegex.test(sourceFile.fileName);
  } else {
    const parentSymbol = symbol as any;
    if (!parentSymbol.parent) {
      return true;
    }
    const parent: TSSymbol = parentSymbol.parent as TSSymbol;
    return externalRegex.test(parent.escapedName as string);
  }
};

const addToSeenLocalExports = (
  exportSymbol: TSSymbol,
  localExports: TSSymbol[]
): void => {
  if (isExternal(exportSymbol) || localExports.includes(exportSymbol)) {
    return;
  }
  localExports.push(exportSymbol);
};

export const getLocalExports = (
  context: Rule.RuleContext
): TSSymbol[] | undefined => {
  const localExports: TSSymbol[] = [];

  const exportSymbols = getExports(context);
  if (exportSymbols === undefined) {
    return exportSymbols;
  }

  exportSymbols.forEach((exportSymbol: TSSymbol): void => {
    addToSeenLocalExports(exportSymbol, localExports);
    if (exportSymbol.exports !== undefined) {
      exportSymbol.exports.forEach((exportedSymbol: TSSymbol): void => {
        addToSeenLocalExports(exportedSymbol, localExports);
      });
    }
    if (exportSymbol.members !== undefined) {
      exportSymbol.members.forEach((memberSymbol: TSSymbol): void => {
        addToSeenLocalExports(memberSymbol, localExports);
      });
    }
  });

  return localExports;
};
