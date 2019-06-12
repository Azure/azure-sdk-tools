using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Linq;
using TypeList;
using Xunit;
using Assembly = TypeList.Assembly;
using Event = TypeList.Event;
using Field = TypeList.Field;
using Method = TypeList.Method;
using NamedType = TypeList.NamedType;
using Namespace = TypeList.Namespace;
using Parameter = TypeList.Parameter;
using TypeParameter = TypeList.TypeParameter;

namespace TypeListTest
{
    public class UnitTest1
    {
        /**
         * Testing strategy for each type variant in TypeList:
         * 
         * Partition the inputs as follows:
         * 
         * Assembly:
         *    Assembly(IAssemblySymbol symbol):
         *       ensure Assembly creation
         *    AssembliesFromFile(string dllPath):
         *       ensure Assembly creation
         * 
         * Event:
         *    Event(IEventSymbol symbol):
         *       ensure Event creation
         * 
         * Field:
         *    Field(IFieldSymbol symbol):
         *       symbol.HasConstantValue: true, false
         * 
         * Method:
         *    Method(IMethodSymbol symbol):
         *       symbol.Attributes.Length: 0, 1, > 1
         *       symbol.IsStatic: true, false
         *       symbol.IsVirtual: true, false
         *       symbol.IsSealed: true, false
         *       symbol.IsOverride: true, false
         *       symbol.IsAbstract: true, false
         *       symbol.IsExtern: true, false
         *       symbol.IsAsync: true, false
         *       symbol.ReturnType: void, non-void
         *       symbol.TypeParameters.Length: 0, 1, > 1
         *       symbol.Parameters.Length: 0, 1, > 1
         * 
         * NamedType:
         *    NamedType(INamedTypeSymbol symbol):
         *       symbol.TypeKind: class, interface, delegate, enum
         *       symbol declares underlying enum type: yes, no
         *       symbol declares values for enum entries: yes, no
         *       symbol implements interface: yes, no
         *       events in symbol.GetMembers(): 0, > 0
         *          count: 0, > 0
         *          access: public, private, protected, internal, protected internal, private protected
         *       fields in symbol.GetMembers(): 0, > 0
         *          count: 0, > 0
         *          access: public, private, protected, internal, protected internal, private protected
         *       methods in symbol.GetMembers(): 0, > 0
         *          count: 0, > 0
         *          access: public, private, protected, internal, protected internal, private protected
         *       named types in symbol.GetMembers():
         *          count: 0, > 0
         *          access: public, private, protected, internal, protected internal, private protected
         *       properties in symbol.GetMembers():
         *          count: 0, > 0
         *          access: public, private, protected, internal, protected internal, private protected
         * 
         * Namespace:
         *    Namespace(InamespaceSymbol symbol):
         *       symbol: global namespace, non-global namespace
         *       named types in symbol.GetMembers():
         *          count: 0, > 0
         *          access: public, internal
         *       namespaces in symbol.GetMembers(): 0, > 0
         * 
         * Parameter:
         *    Parameter(IParameterSymbol symbol):
         *       symbol.Type: has reference type, doesn't have reference type
         *       symbol.HasExplicitDefaultValue: true, false
         *       symbol.ExplicitDefaultValue.Type: string, other
         *       
         * Property:
         *    Property(IPropertySymbol symbol):
         *       symbol.SetMethod: null, not null
         * 
         * TypeParameter:
         *    TypeParameter(ITypeParameterSymbol symbol):
         *       ensure TypeParameter creation
         * 
         * Covering each part of each input/output partition.
         */

        // testing DLL: C:\Users\t-mcpat\Documents\azure-sdk-tools\artifacts\bin\TestLibrary\Debug\netcoreapp2.2\TestLibrary.dll

        [Fact]
        public void AssemblyTestAssembly()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            Assembly assembly = new Assembly(a);
            var eventSymbol = (IEventSymbol)a.GetTypeByMetadataName("Program.Bla").GetMembers("myEvent").Single();
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            Assert.Single(globalNamespace.Namespaces);
        }

        [Fact]
        public void AssemblyTestAssembliesFromFile()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            Assert.Single(globalNamespace.Namespaces);
        }

        [Fact]
        public void EventTestCreation()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<Namespace> namespaces = globalNamespace.Namespaces;
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedType> classes = testLibNamespace.NamedTypes;
            NamedType publicClass = null;
            foreach (NamedType n in classes)
            {
                if (n.Name.Equals("PublicClass"))
                    publicClass = n;
            }
            Assert.NotNull(publicClass);
            Assert.Equal("PublicClass", publicClass.Name);

            ImmutableArray<Event> events = publicClass.Events;
            Assert.Single(events);
            Assert.Equal("PublicEvent", events[0].Name);

            Assert.Contains("public event EventHandler PublicEvent;", events[0].ToString());
        }

        [Fact]
        public void FieldTestVariable()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<Namespace> namespaces = globalNamespace.Namespaces;
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedType> classes = testLibNamespace.NamedTypes;
            NamedType publicClass = null;
            foreach (NamedType n in classes)
            {
                if (n.Name.Equals("PublicClass"))
                    publicClass = n;
            }
            Assert.NotNull(publicClass);
            Assert.Equal("PublicClass", publicClass.Name);

            ImmutableArray<Field> fields = publicClass.Fields;
            Assert.NotEmpty(fields);

            Field field = null;
            foreach (Field f in fields)
            {
                if (f.Name.Equals("publicField"))
                    field = f;
            }
            Assert.NotNull(field);
            Assert.Equal("publicField", field.Name);
            Assert.Equal("int", field.Type);
            Assert.False(field.IsConstant);

            Assert.Contains("public int publicField;", field.ToString());
        }

        [Fact]
        public void FieldTestConstant()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<Namespace> namespaces = globalNamespace.Namespaces;
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedType> classes = testLibNamespace.NamedTypes;
            NamedType publicClass = null;
            foreach (NamedType n in classes)
            {
                if (n.Name.Equals("PublicClass"))
                    publicClass = n;
            }
            Assert.NotNull(publicClass);
            Assert.Equal("PublicClass", publicClass.Name);

            ImmutableArray<Field> fields = publicClass.Fields;
            Assert.NotEmpty(fields);

            Field field = null;
            foreach (Field f in fields)
            {
                if (f.Name.Equals("publicString"))
                    field = f;
            }
            Assert.NotNull(field);
            Assert.Equal("publicString", field.Name);
            Assert.Equal("string", field.Type);
            Assert.True(field.IsConstant);
            Assert.False(field.IsReadOnly);
            Assert.False(field.IsStatic);
            Assert.False(field.IsVolatile);
            Assert.Equal("constant string", field.Value);

            Assert.Contains("public const string publicString = \"constant string\";", field.ToString());
        }

        [Fact]
        public void FieldTestNonConstantDeclaredValue()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<Namespace> namespaces = globalNamespace.Namespaces;
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedType> classes = testLibNamespace.NamedTypes;
            NamedType publicClass = null;
            foreach (NamedType n in classes)
            {
                if (n.Name.Equals("PublicClass"))
                    publicClass = n;
            }
            Assert.NotNull(publicClass);
            Assert.Equal("PublicClass", publicClass.Name);

            ImmutableArray<Field> fields = publicClass.Fields;
            Assert.NotEmpty(fields);

            Field field = null;
            foreach (Field f in fields)
            {
                if (f.Name.Equals("publicField"))
                    field = f;
            }
            Assert.NotNull(field);
            Assert.Equal("publicField", field.Name);
            Assert.Equal("int", field.Type);
            Assert.False(field.IsConstant);
            Assert.True(field.IsReadOnly);
            Assert.False(field.IsStatic);
            Assert.False(field.IsVolatile);

            Assert.Contains("public readonly int publicField;", field.ToString());
        }

        [Fact]
        public void MethodTestNoAttributesOneTypeParamMultipleParams()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<Namespace> namespaces = globalNamespace.Namespaces;
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedType> namedTypes = testLibNamespace.NamedTypes;
            NamedType publicInterface = null;
            foreach (NamedType NamedType in namedTypes)
            {
                if (NamedType.Name.Equals("PublicInterface"))
                    publicInterface = NamedType;
            }
            Assert.False(publicInterface == null);
            Assert.Equal("PublicInterface", publicInterface.Name);

            ImmutableArray<Method> methods = publicInterface.Methods;
            Method method = null;
            foreach (Method m in methods)
            {
                if (m.Name.Equals("TypeParamParamsMethod"))
                    method = m;
            }

            Assert.False(method == null);
            Assert.False(method.IsStatic);
            Assert.False(method.IsVirtual);
            Assert.False(method.IsSealed);
            Assert.False(method.IsOverride);
            Assert.True(method.IsAbstract);
            Assert.False(method.IsExtern);
            Assert.Equal("int", method.ReturnType);
            
            ImmutableArray<TypeParameter> typeParameters = method.TypeParameters;
            Assert.Single(typeParameters);
            Assert.Equal("T", typeParameters[0].Name);

            Assert.Contains("int TypeParamParamsMethod<T>(T param, string str = \"hello\");", method.ToString());
        }

        [Fact]
        public void MethodTestOneAttributeNoTypeParamsOneParam()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<Namespace> namespaces = globalNamespace.Namespaces;
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.NamedTypes;
            NamedType publicClass = null;
            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("PublicClass"))
                    publicClass = NamedType;
            }
            Assert.False(publicClass == null);
            Assert.Equal("PublicClass", publicClass.Name);

            ImmutableArray<Method> methods = publicClass.Methods;
            Method method = null;
            foreach (Method m in methods)
            {
                if (m.Name.Equals("StaticVoid"))
                    method = m;
            }

            Assert.False(method == null);
            Assert.True(method.IsStatic);
            Assert.False(method.IsVirtual);
            Assert.False(method.IsSealed);
            Assert.False(method.IsOverride);
            Assert.False(method.IsAbstract);
            Assert.False(method.IsExtern);
            Assert.Equal("void", method.ReturnType);
            
            ImmutableArray<AttributeData> attributes = method.Attributes;
            Assert.Single(attributes);

            ImmutableArray<Parameter> parameters = method.Parameters;
            Assert.Single(parameters);
            Assert.Equal("args", parameters[0].Name);
            Assert.Equal("string[]", parameters[0].Type);

            string stringRep = method.ToString();
            Assert.Contains("[Conditional(\"DEBUG\")]", stringRep);
            Assert.Contains("public static void StaticVoid(string[] args) {", stringRep);
        }

        [Fact]
        public void MethodTestMultipleAttributesMultipleTypeParamsNoParams()
        {
            //TODO
        }

        [Fact]
        public void NamedTypeTestClassSomeEventsSomeFieldsNoMethodsSomeNamedTypes()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<Namespace> namespaces = globalNamespace.Namespaces;
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.NamedTypes;
            NamedType publicClass = null;

            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("SomeEventsSomeFieldsNoMethodsSomeNamedTypes"))
                    publicClass = NamedType;
            }
            Assert.False(publicClass == null);
            Assert.Equal("SomeEventsSomeFieldsNoMethodsSomeNamedTypes", publicClass.Name);
            Assert.Equal("class", publicClass.Type);

            ImmutableArray<Event> events = publicClass.Events;
            Assert.Single(events);

            ImmutableArray<Field> fields = publicClass.Fields;
            Assert.Single(fields);

            ImmutableArray<Method> methods = publicClass.Methods;
            Assert.Empty(methods);

            NamedTypes = publicClass.NamedTypes;
            Assert.Single(NamedTypes);

            Assert.Contains("public class SomeEventsSomeFieldsNoMethodsSomeNamedTypes {", publicClass.ToString());
        }

        [Fact]
        public void NamedTypeTestInterfaceNoEventsNoFieldsSomeMethodsNoNamedTypes()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<Namespace> namespaces = globalNamespace.Namespaces;
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.NamedTypes;
            NamedType publicInterface = null;

            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("PublicInterface"))
                    publicInterface = NamedType;
            }
            Assert.False(publicInterface == null);
            Assert.Equal("PublicInterface", publicInterface.Name);
            Assert.Equal("interface", publicInterface.Type);

            ImmutableArray<Event> events = publicInterface.Events;
            Assert.Empty(events);

            ImmutableArray<Field> fields = publicInterface.Fields;
            Assert.Empty(fields);

            ImmutableArray<Method> methods = publicInterface.Methods;
            Assert.Equal(2, methods.Length);

            NamedTypes = publicInterface.NamedTypes;
            Assert.Empty(NamedTypes);

            Assert.Contains("public interface PublicInterface {", publicInterface.ToString());
        }

        [Fact]
        public void NamedTypeTestImplementsInterface()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<Namespace> namespaces = globalNamespace.Namespaces;
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.NamedTypes;
            NamedType implementer = null;

            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("ImplementingClass"))
                    implementer = NamedType;
            }
            Assert.False(implementer == null);
            Assert.Equal("ImplementingClass", implementer.Name);
            Assert.Equal("class", implementer.Type);

            ImmutableArray<string> implementations = implementer.Implementations;
            Assert.Single(implementations);
            Assert.Equal("PublicInterface<int>", implementations[0]);

            Assert.Contains("public class ImplementingClass : PublicInterface<int> {", implementer.ToString());
        }

        [Fact]
        public void NamedTypeEnumDefaultUnderlyingType()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<Namespace> namespaces = globalNamespace.Namespaces;
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.NamedTypes;
            NamedType publicClass = null;

            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("PublicClass"))
                    publicClass = NamedType;
            }
            Assert.False(publicClass == null);
            Assert.Equal("PublicClass", publicClass.Name);

            NamedTypes = publicClass.NamedTypes;
            NamedType publicEnum = null;

            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("PublicEnum"))
                    publicEnum = NamedType;
            }
            Assert.False(publicEnum == null);
            Assert.Equal("PublicEnum", publicEnum.Name);
            Assert.Equal("enum", publicEnum.Type);
            Assert.Equal("int", publicEnum.EnumUnderlyingType);

            string stringRep = publicEnum.ToString();
            Assert.Contains("public enum PublicEnum {", stringRep);
            Assert.Contains("One = 0,", stringRep);
            Assert.Contains("Two = 1,", stringRep);
            Assert.Contains("Three = 2", stringRep);
        }

        [Fact]
        public void NamedTypeEnumDeclaredUnderlyingType()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<Namespace> namespaces = globalNamespace.Namespaces;
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.NamedTypes;
            NamedType publicClass = null;

            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("PublicClass"))
                    publicClass = NamedType;
            }
            Assert.False(publicClass == null);
            Assert.Equal("PublicClass", publicClass.Name);

            NamedTypes = publicClass.NamedTypes;
            NamedType publicEnum = null;

            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("PublicEnum"))
                    publicEnum = NamedType;
            }
            Assert.False(publicEnum == null);
            Assert.Equal("PublicEnum", publicEnum.Name);
            Assert.Equal("enum", publicEnum.Type);
            Assert.Equal("long", publicEnum.EnumUnderlyingType);

            string stringRep = publicEnum.ToString();
            Assert.Contains("public enum PublicEnum : long {", stringRep);
            Assert.Contains("One = 1,", stringRep);
            Assert.Contains("Two = 2,", stringRep);
            Assert.Contains("Three = 3", stringRep);
        }

        [Fact]
        public void NamedTypeDelegate()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<Namespace> namespaces = globalNamespace.Namespaces;
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.NamedTypes;
            NamedType publicDelegate = null;

            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("publicDelegate"))
                    publicDelegate = NamedType;
            }
            Assert.False(publicDelegate == null);
            Assert.Equal("publicDelegate", publicDelegate.Name);
            Assert.Equal("delegate", publicDelegate.Type);

            Assert.Contains("public delegate int publicDelegate(int num = 10);", publicDelegate.ToString());
        }

        [Fact]
        public void NamespaceTestGlobalNoNamedTypesSomenamespaces()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<NamedType> namedTypes = globalNamespace.NamedTypes;
            Assert.Empty(namedTypes);

            ImmutableArray<Namespace> namespaces = globalNamespace.Namespaces;
            Assert.Single(namespaces);
        }

        [Fact]
        public void NamespaceTestNonGlobalSomeNamedTypesNonamespaces()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;

            ImmutableArray<Namespace> namespaces = globalNamespace.Namespaces;
            Assert.Single(namespaces);

            Namespace nestednamespace = namespaces[0];
            Assert.Equal("TestLibrary", nestednamespace.Name);

            ImmutableArray<NamedType> NamedTypes = nestednamespace.NamedTypes;
            Assert.NotEmpty(NamedTypes);

            namespaces = nestednamespace.Namespaces;
            Assert.Empty(namespaces);

            Assert.Contains("namespace TestLibrary {", nestednamespace.ToString());
        }

        [Fact]
        public void ParameterTestNoRefKindStringDefaultValue()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<Namespace> namespaces = globalNamespace.Namespaces;
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.NamedTypes;
            NamedType publicInterface = null;
            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("PublicInterface"))
                    publicInterface = NamedType;
            }
            Assert.False(publicInterface == null);
            Assert.Equal("PublicInterface", publicInterface.Name);

            ImmutableArray<Method> methods = publicInterface.Methods;
            Method method = null;
            foreach (Method m in methods)
            {
                if (m.Name.Equals("TypeParamParamsMethod"))
                    method = m;
            }

            ImmutableArray<Parameter> parameters = method.Parameters;
            Assert.Equal(2, parameters.Length);

            Parameter param = null;
            Parameter num = null;
            foreach (Parameter p in parameters)
            {
                if (p.Name.Equals("param"))
                    param = p;
                else
                    num = p;
            }

            Assert.False(param == null || num == null);
            Assert.Equal("T", param.Type);
            Assert.Equal("param", param.Name);
            Assert.Null(param.ExplicitDefaultValue);

            Assert.Equal("string", num.Type);
            Assert.Equal("str", num.Name);
            Assert.Equal("hello", num.ExplicitDefaultValue);

            Assert.Contains("int TypeParamParamsMethod<T>(T param, string str = \"hello\");", method.ToString());
        }

        [Fact]
        public void ParameterTestSomeRefKindNoDefaultValue()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<Namespace> namespaces = globalNamespace.Namespaces;
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.NamedTypes;
            NamedType publicInterface = null;
            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("PublicInterface"))
                    publicInterface = NamedType;
            }
            Assert.False(publicInterface == null);
            Assert.Equal("PublicInterface", publicInterface.Name);

            ImmutableArray<Method> methods = publicInterface.Methods;
            Method method = null;
            foreach (Method m in methods)
            {
                if (m.Name.Equals("RefKindParamMethod"))
                    method = m;
            }

            ImmutableArray<Parameter> parameters = method.Parameters;
            Assert.Single(parameters);

            Assert.Equal("ref string", parameters[0].Type);
            Assert.Equal("str", parameters[0].Name);
            Assert.Null(parameters[0].ExplicitDefaultValue);

            Assert.Contains("string RefKindParamMethod(ref string str);", method.ToString());
        }

        [Fact]
        public void PropertyTestNoSetter()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<Namespace> namespaces = globalNamespace.Namespaces;
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.NamedTypes;
            NamedType publicClass = null;
            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("PublicClass"))
                    publicClass = NamedType;
            }
            Assert.NotNull(publicClass);
            Assert.Equal("PublicClass", publicClass.Name);

            ImmutableArray<Property> properties = publicClass.Properties;
            Property property = null;
            foreach (Property p in properties)
            {
                if (p.Name.Equals("propertyGet"))
                    property = p;
            }
            Assert.NotNull(property);
            Assert.Equal("propertyGet", property.Name);
            Assert.Equal("uint", property.Type);
            Assert.False(property.HasSetMethod);

            Assert.Contains("public uint propertyGet { get; }", property.ToString());
        }

        [Fact]
        public void PropertyTestHasSetter()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<Namespace> namespaces = globalNamespace.Namespaces;
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.NamedTypes;
            NamedType publicClass = null;
            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("PublicClass"))
                    publicClass = NamedType;
            }
            Assert.NotNull(publicClass);
            Assert.Equal("PublicClass", publicClass.Name);

            ImmutableArray<Property> properties = publicClass.Properties;
            Property property = null;
            foreach (Property p in properties)
            {
                if (p.Name.Equals("propertyBoth"))
                    property = p;
            }
            Assert.NotNull(property);
            Assert.Equal("propertyBoth", property.Name);
            Assert.Equal("int", property.Type);
            Assert.True(property.HasSetMethod);

            Assert.Contains("public int propertyBoth { get; set; }", property.ToString());
        }

        [Fact]
        public void TypeParameterTestCreation()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            Namespace globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<Namespace> namespaces = globalNamespace.Namespaces;
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.NamedTypes;
            NamedType publicInterface = null;
            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.Name.Equals("PublicInterface"))
                    publicInterface = NamedType;
            }
            Assert.False(publicInterface == null);
            Assert.Equal("PublicInterface", publicInterface.Name);

            ImmutableArray<Method> methods = publicInterface.Methods;
            Method method = null;
            foreach (Method m in methods)
            {
                if (m.Name.Equals("TypeParamParamsMethod"))
                    method = m;
            }

            ImmutableArray<TypeParameter> parameters = method.TypeParameters;
            Assert.Single(parameters);
            Assert.Equal("T", parameters[0].Name);
        }
    }
}
