using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
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
         *       symbol.GetAttributes().Length: 0, 1, > 1
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
         * namespace:
         *    namespace(InamespaceSymbol symbol):
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

        // testing DLL: C:\Users\t-mcpat\Documents\azure-sdk-tools\artifacts\bin\TestLibrary\Debug\netstandard2.0\TestLibrary.dll

        [Fact]
        public void AssemblyTestAssembly()
        {
            var reference = MetadataReference.CreateFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);

            Assembly assembly = new Assembly(compilation.SourceModule.ReferencedAssemblySymbols[0]);
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            Assert.Single(globalNamespace.GetNamespaces());
        }

        [Fact]
        public void AssemblyTestAssembliesFromFile()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            Assert.Single(globalNamespace.GetNamespaces());
        }

        [Fact]
        public void EventTestCreation()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            ImmutableArray<Namespace> namespaces = globalNamespace.GetNamespaces();
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.GetName());

            ImmutableArray<NamedType> classes = testLibNamespace.GetNamedTypes();
            NamedType publicClass = null;
            foreach (NamedType n in classes)
            {
                if (n.GetName().Equals("PublicClass"))
                    publicClass = n;
            }
            Assert.NotNull(publicClass);
            Assert.Equal("PublicClass", publicClass.GetName());

            ImmutableArray<Event> events = publicClass.GetEvents();
            Assert.Single(events);
            Assert.Equal("PublicEvent", events[0].GetName());

            Assert.Contains("public event EventHandler PublicEvent;", events[0].ToString());
        }

        [Fact]
        public void FieldTestVariable()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            ImmutableArray<Namespace> namespaces = globalNamespace.GetNamespaces();
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.GetName());

            ImmutableArray<NamedType> classes = testLibNamespace.GetNamedTypes();
            NamedType publicClass = null;
            foreach (NamedType n in classes)
            {
                if (n.GetName().Equals("PublicClass"))
                    publicClass = n;
            }
            Assert.NotNull(publicClass);
            Assert.Equal("PublicClass", publicClass.GetName());

            ImmutableArray<Field> fields = publicClass.GetFields();
            Assert.NotEmpty(fields);

            Field field = null;
            foreach (Field f in fields)
            {
                if (f.GetName().Equals("publicField"))
                    field = f;
            }
            Assert.NotNull(field);
            Assert.Equal("publicField", field.GetName());
            Assert.Equal("int", field.GetFieldType());
            Assert.False(field.IsConstant());

            Assert.Contains("public int publicField;", field.ToString());
        }

        [Fact]
        public void FieldTestConstant()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            ImmutableArray<Namespace> namespaces = globalNamespace.GetNamespaces();
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.GetName());

            ImmutableArray<NamedType> classes = testLibNamespace.GetNamedTypes();
            NamedType publicClass = null;
            foreach (NamedType n in classes)
            {
                if (n.GetName().Equals("PublicClass"))
                    publicClass = n;
            }
            Assert.NotNull(publicClass);
            Assert.Equal("PublicClass", publicClass.GetName());

            ImmutableArray<Field> fields = publicClass.GetFields();
            Assert.NotEmpty(fields);

            Field field = null;
            foreach (Field f in fields)
            {
                if (f.GetName().Equals("publicString"))
                    field = f;
            }
            Assert.NotNull(field);
            Assert.Equal("publicString", field.GetName());
            Assert.Equal("string", field.GetFieldType());
            Assert.True(field.IsConstant());
            Assert.False(field.IsReadOnly());
            Assert.False(field.IsStatic());
            Assert.False(field.IsVolatile());
            Assert.Equal("constant string", field.GetValue());

            Assert.Contains("public const string publicString = \"constant string\";", field.ToString());
        }

        [Fact]
        public void FieldTestNonConstantDeclaredValue()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            ImmutableArray<Namespace> namespaces = globalNamespace.GetNamespaces();
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.GetName());

            ImmutableArray<NamedType> classes = testLibNamespace.GetNamedTypes();
            NamedType publicClass = null;
            foreach (NamedType n in classes)
            {
                if (n.GetName().Equals("PublicClass"))
                    publicClass = n;
            }
            Assert.NotNull(publicClass);
            Assert.Equal("PublicClass", publicClass.GetName());

            ImmutableArray<Field> fields = publicClass.GetFields();
            Assert.NotEmpty(fields);

            Field field = null;
            foreach (Field f in fields)
            {
                if (f.GetName().Equals("publicField"))
                    field = f;
            }
            Assert.NotNull(field);
            Assert.Equal("publicField", field.GetName());
            Assert.Equal("int", field.GetFieldType());
            Assert.False(field.IsConstant());
            Assert.True(field.IsReadOnly());
            Assert.False(field.IsStatic());
            Assert.False(field.IsVolatile());

            Assert.Contains("public readonly int publicField;", field.ToString());
        }

        [Fact]
        public void MethodTestNoAttributesOneTypeParamMultipleParams()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            ImmutableArray<Namespace> namespaces = globalNamespace.GetNamespaces();
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.GetName());

            ImmutableArray<NamedType> namedTypes = testLibNamespace.GetNamedTypes();
            NamedType publicInterface = null;
            foreach (NamedType NamedType in namedTypes)
            {
                if (NamedType.GetName().Equals("PublicInterface"))
                    publicInterface = NamedType;
            }
            Assert.False(publicInterface == null);
            Assert.Equal("PublicInterface", publicInterface.GetName());

            ImmutableArray<Method> methods = publicInterface.GetMethods();
            Method method = null;
            foreach (Method m in methods)
            {
                if (m.GetName().Equals("TypeParamParamsMethod"))
                    method = m;
            }

            Assert.False(method == null);
            Assert.False(method.IsStatic());
            Assert.False(method.IsVirtual());
            Assert.False(method.IsSealed());
            Assert.False(method.IsOverride());
            Assert.True(method.IsAbstract());
            Assert.False(method.IsExtern());
            Assert.Equal("int", method.GetReturnType());
            
            ImmutableArray<TypeParameter> typeParameters = method.GetTypeParameters();
            Assert.Single(typeParameters);
            Assert.Equal("T", typeParameters[0].GetName());

            Assert.Contains("int TypeParamParamsMethod<T>(T param, string str = \"hello\");", method.ToString());
        }

        [Fact]
        public void MethodTestOneAttributeNoTypeParamsOneParam()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            ImmutableArray<Namespace> namespaces = globalNamespace.GetNamespaces();
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.GetName());

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.GetNamedTypes();
            NamedType publicClass = null;
            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.GetName().Equals("PublicClass"))
                    publicClass = NamedType;
            }
            Assert.False(publicClass == null);
            Assert.Equal("PublicClass", publicClass.GetName());

            ImmutableArray<Method> methods = publicClass.GetMethods();
            Method method = null;
            foreach (Method m in methods)
            {
                if (m.GetName().Equals("StaticVoid"))
                    method = m;
            }

            Assert.False(method == null);
            Assert.True(method.IsStatic());
            Assert.False(method.IsVirtual());
            Assert.False(method.IsSealed());
            Assert.False(method.IsOverride());
            Assert.False(method.IsAbstract());
            Assert.False(method.IsExtern());
            Assert.Equal("void", method.GetReturnType());
            
            ImmutableArray<AttributeData> attributes = method.GetAttributes();
            Assert.Single(attributes);

            ImmutableArray<Parameter> parameters = method.GetParameters();
            Assert.Single(parameters);
            Assert.Equal("args", parameters[0].GetName());
            Assert.Equal("string[]", parameters[0].GetParameterType());

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
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            ImmutableArray<Namespace> namespaces = globalNamespace.GetNamespaces();
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.GetName());

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.GetNamedTypes();
            NamedType publicClass = null;

            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.GetName().Equals("SomeEventsSomeFieldsNoMethodsSomeNamedTypes"))
                    publicClass = NamedType;
            }
            Assert.False(publicClass == null);
            Assert.Equal("SomeEventsSomeFieldsNoMethodsSomeNamedTypes", publicClass.GetName());
            Assert.Equal("class", publicClass.GetNamedType());

            ImmutableArray<Event> events = publicClass.GetEvents();
            Assert.Single(events);

            ImmutableArray<Field> fields = publicClass.GetFields();
            Assert.Single(fields);

            ImmutableArray<Method> methods = publicClass.GetMethods();
            Assert.Empty(methods);

            NamedTypes = publicClass.GetNamedTypes();
            Assert.Single(NamedTypes);

            Assert.Contains("public class SomeEventsSomeFieldsNoMethodsSomeNamedTypes {", publicClass.ToString());
        }

        [Fact]
        public void NamedTypeTestInterfaceNoEventsNoFieldsSomeMethodsNoNamedTypes()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            ImmutableArray<Namespace> namespaces = globalNamespace.GetNamespaces();
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.GetName());

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.GetNamedTypes();
            NamedType publicInterface = null;

            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.GetName().Equals("PublicInterface"))
                    publicInterface = NamedType;
            }
            Assert.False(publicInterface == null);
            Assert.Equal("PublicInterface", publicInterface.GetName());
            Assert.Equal("interface", publicInterface.GetNamedType());

            ImmutableArray<Event> events = publicInterface.GetEvents();
            Assert.Empty(events);

            ImmutableArray<Field> fields = publicInterface.GetFields();
            Assert.Empty(fields);

            ImmutableArray<Method> methods = publicInterface.GetMethods();
            Assert.Equal(2, methods.Length);

            NamedTypes = publicInterface.GetNamedTypes();
            Assert.Empty(NamedTypes);

            Assert.Contains("public interface PublicInterface {", publicInterface.ToString());
        }

        [Fact]
        public void NamedTypeTestImplementsInterface()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            ImmutableArray<Namespace> namespaces = globalNamespace.GetNamespaces();
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.GetName());

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.GetNamedTypes();
            NamedType implementer = null;

            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.GetName().Equals("ImplementingClass"))
                    implementer = NamedType;
            }
            Assert.False(implementer == null);
            Assert.Equal("ImplementingClass", implementer.GetName());
            Assert.Equal("class", implementer.GetNamedType());

            ImmutableArray<string> implementations = implementer.GetImplementations();
            Assert.Single(implementations);
            Assert.Equal("PublicInterface<int>", implementations[0]);

            Assert.Contains("public class ImplementingClass : PublicInterface<int> {", implementer.ToString());
        }

        [Fact]
        public void NamedTypeEnumDefaultUnderlyingType()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            ImmutableArray<Namespace> namespaces = globalNamespace.GetNamespaces();
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.GetName());

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.GetNamedTypes();
            NamedType publicClass = null;

            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.GetName().Equals("PublicClass"))
                    publicClass = NamedType;
            }
            Assert.False(publicClass == null);
            Assert.Equal("PublicClass", publicClass.GetName());

            NamedTypes = publicClass.GetNamedTypes();
            NamedType publicEnum = null;

            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.GetName().Equals("PublicEnum"))
                    publicEnum = NamedType;
            }
            Assert.False(publicEnum == null);
            Assert.Equal("PublicEnum", publicEnum.GetName());
            Assert.Equal("enum", publicEnum.GetNamedType());
            Assert.Equal("int", publicEnum.GetEnumUnderlyingType());

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
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            ImmutableArray<Namespace> namespaces = globalNamespace.GetNamespaces();
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.GetName());

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.GetNamedTypes();
            NamedType publicClass = null;

            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.GetName().Equals("PublicClass"))
                    publicClass = NamedType;
            }
            Assert.False(publicClass == null);
            Assert.Equal("PublicClass", publicClass.GetName());

            NamedTypes = publicClass.GetNamedTypes();
            NamedType publicEnum = null;

            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.GetName().Equals("PublicEnum"))
                    publicEnum = NamedType;
            }
            Assert.False(publicEnum == null);
            Assert.Equal("PublicEnum", publicEnum.GetName());
            Assert.Equal("enum", publicEnum.GetNamedType());
            Assert.Equal("long", publicEnum.GetEnumUnderlyingType());

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
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            ImmutableArray<Namespace> namespaces = globalNamespace.GetNamespaces();
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.GetName());

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.GetNamedTypes();
            NamedType publicDelegate = null;

            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.GetName().Equals("publicDelegate"))
                    publicDelegate = NamedType;
            }
            Assert.False(publicDelegate == null);
            Assert.Equal("publicDelegate", publicDelegate.GetName());
            Assert.Equal("delegate", publicDelegate.GetNamedType());

            Assert.Contains("public delegate int publicDelegate(int num = 10);", publicDelegate.ToString());
        }

        [Fact]
        public void NamespaceTestGlobalNoNamedTypesSomenamespaces()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            ImmutableArray<NamedType> namedTypes = globalNamespace.GetNamedTypes();
            Assert.Empty(namedTypes);

            ImmutableArray<Namespace> namespaces = globalNamespace.GetNamespaces();
            Assert.Single(namespaces);
        }

        [Fact]
        public void NamespaceTestNonGlobalSomeNamedTypesNonamespaces()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();

            ImmutableArray<Namespace> namespaces = globalNamespace.GetNamespaces();
            Assert.Single(namespaces);

            Namespace nestednamespace = namespaces[0];
            Assert.Equal("TestLibrary", nestednamespace.GetName());

            ImmutableArray<NamedType> NamedTypes = nestednamespace.GetNamedTypes();
            Assert.NotEmpty(NamedTypes);

            namespaces = nestednamespace.GetNamespaces();
            Assert.Empty(namespaces);

            Assert.Contains("namespace TestLibrary {", nestednamespace.ToString());
        }

        [Fact]
        public void ParameterTestNoRefKindStringDefaultValue()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            ImmutableArray<Namespace> namespaces = globalNamespace.GetNamespaces();
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.GetName());

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.GetNamedTypes();
            NamedType publicInterface = null;
            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.GetName().Equals("PublicInterface"))
                    publicInterface = NamedType;
            }
            Assert.False(publicInterface == null);
            Assert.Equal("PublicInterface", publicInterface.GetName());

            ImmutableArray<Method> methods = publicInterface.GetMethods();
            Method method = null;
            foreach (Method m in methods)
            {
                if (m.GetName().Equals("TypeParamParamsMethod"))
                    method = m;
            }

            ImmutableArray<Parameter> parameters = method.GetParameters();
            Assert.Equal(2, parameters.Length);

            Parameter param = null;
            Parameter num = null;
            foreach (Parameter p in parameters)
            {
                if (p.GetName().Equals("param"))
                    param = p;
                else
                    num = p;
            }

            Assert.False(param == null || num == null);
            Assert.Equal("T", param.GetParameterType());
            Assert.Equal("param", param.GetName());
            Assert.Null(param.GetExplicitDefaultValue());

            Assert.Equal("string", num.GetParameterType());
            Assert.Equal("str", num.GetName());
            Assert.Equal("hello", num.GetExplicitDefaultValue());

            Assert.Contains("int TypeParamParamsMethod<T>(T param, string str = \"hello\");", method.ToString());
        }

        [Fact]
        public void ParameterTestSomeRefKindNoDefaultValue()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            ImmutableArray<Namespace> namespaces = globalNamespace.GetNamespaces();
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.GetName());

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.GetNamedTypes();
            NamedType publicInterface = null;
            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.GetName().Equals("PublicInterface"))
                    publicInterface = NamedType;
            }
            Assert.False(publicInterface == null);
            Assert.Equal("PublicInterface", publicInterface.GetName());

            ImmutableArray<Method> methods = publicInterface.GetMethods();
            Method method = null;
            foreach (Method m in methods)
            {
                if (m.GetName().Equals("RefKindParamMethod"))
                    method = m;
            }

            ImmutableArray<Parameter> parameters = method.GetParameters();
            Assert.Single(parameters);

            Assert.Equal("ref string", parameters[0].GetParameterType());
            Assert.Equal("str", parameters[0].GetName());
            Assert.Null(parameters[0].GetExplicitDefaultValue());

            Assert.Contains("string RefKindParamMethod(ref string str);", method.ToString());
        }

        [Fact]
        public void PropertyTestNoSetter()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            ImmutableArray<Namespace> namespaces = globalNamespace.GetNamespaces();
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.GetName());

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.GetNamedTypes();
            NamedType publicClass = null;
            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.GetName().Equals("PublicClass"))
                    publicClass = NamedType;
            }
            Assert.NotNull(publicClass);
            Assert.Equal("PublicClass", publicClass.GetName());

            ImmutableArray<Property> properties = publicClass.GetProperties();
            Property property = null;
            foreach (Property p in properties)
            {
                if (p.GetName().Equals("propertyGet"))
                    property = p;
            }
            Assert.NotNull(property);
            Assert.Equal("propertyGet", property.GetName());
            Assert.Equal("uint", property.GetPropertyType());
            Assert.False(property.HasSetMethod());

            Assert.Contains("public uint propertyGet { get; }", property.ToString());
        }

        [Fact]
        public void PropertyTestHasSetter()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            ImmutableArray<Namespace> namespaces = globalNamespace.GetNamespaces();
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.GetName());

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.GetNamedTypes();
            NamedType publicClass = null;
            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.GetName().Equals("PublicClass"))
                    publicClass = NamedType;
            }
            Assert.NotNull(publicClass);
            Assert.Equal("PublicClass", publicClass.GetName());

            ImmutableArray<Property> properties = publicClass.GetProperties();
            Property property = null;
            foreach (Property p in properties)
            {
                if (p.GetName().Equals("propertyBoth"))
                    property = p;
            }
            Assert.NotNull(property);
            Assert.Equal("propertyBoth", property.GetName());
            Assert.Equal("int", property.GetPropertyType());
            Assert.True(property.HasSetMethod());

            Assert.Contains("public int propertyBoth { get; set; }", property.ToString());
        }

        [Fact]
        public void TypeParameterTestCreation()
        {
            Assembly assembly = Assembly.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.GetName());

            Namespace globalNamespace = assembly.GetGlobalNamespace();
            ImmutableArray<Namespace> namespaces = globalNamespace.GetNamespaces();
            Namespace testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.GetName());

            ImmutableArray<NamedType> NamedTypes = testLibNamespace.GetNamedTypes();
            NamedType publicInterface = null;
            foreach (NamedType NamedType in NamedTypes)
            {
                if (NamedType.GetName().Equals("PublicInterface"))
                    publicInterface = NamedType;
            }
            Assert.False(publicInterface == null);
            Assert.Equal("PublicInterface", publicInterface.GetName());

            ImmutableArray<Method> methods = publicInterface.GetMethods();
            Method method = null;
            foreach (Method m in methods)
            {
                if (m.GetName().Equals("TypeParamParamsMethod"))
                    method = m;
            }

            ImmutableArray<TypeParameter> parameters = method.GetTypeParameters();
            Assert.Single(parameters);
            Assert.Equal("T", parameters[0].GetName());
        }
    }
}
