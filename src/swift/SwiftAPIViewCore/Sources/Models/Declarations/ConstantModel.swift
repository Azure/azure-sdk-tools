// --------------------------------------------------------------------------
//
// Copyright (c) Microsoft Corporation. All rights reserved.
//
// The MIT License (MIT)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the ""Software""), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
//
// --------------------------------------------------------------------------

import Foundation
import AST

/// Grammar Summary:
///     constant-declaration → attributes opt declaration-modifiers opt let pattern-initializer-list
///     pattern-initializer-list → pattern-initializer | pattern-initializer , pattern-initializer-list
///     pattern-initializer → pattern initializer opt
///     initializer → = expression
class ConstantModel: Tokenizable {

    var attributes: AttributesModel
    //var declarationModifiers: ModifiersModel
    var accessLevel: String
    var name: String
    var typeModel: String
    var defaultValue: String

    init(from decl: ConstantDeclaration) {
        //        for item in decl.initializerList {
        //            if let typeModel = item.typeModel {
        //                let accessLevel = decl.modifiers.accessLevel ?? overridingAccess ?? .internal
        //                let defId = defId(forName: item.name, withPrefix: defIdPrefix)
        //                return processMember(name: item.name, defId: defId, attributes: decl.attributes, modifiers: decl.modifiers, typeModel: typeModel, isConst: true, defaultValue: item.defaultValue, accessLevel: accessLevel)
        //            }
        //        }
        //        SharedLogger.fail("Type information not found.")
        self.attributes = AttributesModel(from: decl.attributes)
        //self.declarationModifiers = [String]()
        self.accessLevel = ""
        self.name = ""
        self.typeModel = ""
        self.defaultValue = ""
    }

    //    private func process(_ decl: ConstantDeclaration, defIdPrefix: String, overridingAccess: AccessLevelModifier? = nil) -> Bool {
    //        for item in decl.initializerList {
    //            if let typeModel = item.typeModel {
    //                let accessLevel = decl.modifiers.accessLevel ?? overridingAccess ?? .internal
    //                let defId = defId(forName: item.name, withPrefix: defIdPrefix)
    //                return processMember(name: item.name, defId: defId, attributes: decl.attributes, modifiers: decl.modifiers, typeModel: typeModel, isConst: true, defaultValue: item.defaultValue, accessLevel: accessLevel)
    //            }
    //        }
    //        SharedLogger.fail("Type information not found.")
    //    }


    func tokenize() -> [Token] {
        var t = [Token]()
        //guard publicModifiers.contains(accessLevel) else { return false }
        //t.handle(attributes: attributes, defId: defId)
        //t.handle(modifiers: modifiers)
        t.keyword("let")
        t.whitespace()
        t.member(name: name, definitionId: nil)
        t.punctuation(":")
        t.whitespace()
        //t.typeModel.tokenize()
        //if let defaultValue = defaultValue {
        //            whitespace()
        //            punctuation("=")
        //            whitespace()
        //            stringLiteral(defaultValue)
        //        }
        //        newLine()
        //        return true
        //    }
        return t
    }
}
