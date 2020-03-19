import logging
import inspect
import astroid
import operator
from inspect import Parameter
from ._docstring_parser import Docstring
from ._base_node import ArgType, NodeEntityBase, get_qualified_name, get_navigation_id
from _token import Token
from _token_kind import TokenKind


logging.getLogger().setLevel(logging.INFO)


    
class FunctionNode(NodeEntityBase):
    """Function node to represent parsed function and method nodes
    """
    def __init__(self, namespace, parent_node, obj):
        super().__init__(namespace, parent_node, obj)
        self.annotations = []
        self.args = []
        self.return_type = None
        self.namespace_id = self.generate_id()
        self.is_class_method = False
        self._inspect()        


    def _inspect(self):
        code = inspect.getsource(self.obj).strip()
        self.is_async = code.__contains__("async def")
        self.def_key = "async def" if self.is_async else "def"
        # Find decorators and any annotations
        node = astroid.extract_node(inspect.getsource(self.obj))
        if node.decorators:
            self.annotations = ["@{}".format(x.name) for x in node.decorators.nodes if hasattr(x, "name")]
            self.is_class_method = "@classmethod" in self.annotations
        self._parse_function()

    
    def _parse_function(self):
        """
        Find positional and keyword arguements, type and default value and return type of method
        Parsing logic will follow below order to identify these information
        1. Identify args, types, default and ret type using inspect
        2. Parse type annotations if inspect doesn't have complete info
        3. Parse docstring to find keyword arguements
        """
        if "@classmethod" in self.annotations:
            self.args.append(ArgType("cls"))
            
        sig = inspect.signature(self.obj)
        params = sig.parameters
        self.kw_arg = None
        for argname in params:
            arg = ArgType(argname, get_qualified_name(params[argname].annotation))
            if params[argname].default != Parameter.empty:
                arg.default = get_qualified_name(params[argname].default)

            # Store handle to kwarg object to replace it later
            if params[argname].kind in [Parameter.VAR_KEYWORD, Parameter.KEYWORD_ONLY]:
                arg.argname = "**kwargs"
                self.kw_arg = arg

            self.args.append(arg)

        if sig.return_annotation:
            self.return_type = get_qualified_name(sig.return_annotation)

        docstring = ""
        # Refer docstring at class if this is constructor
        if self.name == "__init__" and hasattr(self.parent_node.obj, "__doc__"):
            docstring = self.parent_node.obj.__doc__
        else:
            docstring = self.obj.__doc__

        if docstring:
            #  Parse doc string to find missing types, kwargs and return type
            parsed_docstring = Docstring(docstring)
            parsed_docstring.parse()
            # Copy missing types and kwargs and return type
            if not self.return_type and parsed_docstring.ret_type:
                self.return_type = parsed_docstring.ret_type

            # Update arg type from docstring if available and if argtype is missing from signatrue parsing
            arg_type_dict = dict([(x.argname, x.argtype) for x in parsed_docstring.pos_args])
            for pos_arg in self.args:
                if not pos_arg.argtype:
                    pos_arg.argtype = arg_type_dict.get(pos_arg.argname, pos_arg.argtype)

            if parsed_docstring.kw_args:
                if self.kw_arg in self.args:
                    self.args.remove(self.kw_arg)
                # Add seperator to differentiate pos_arg and keyword args
                self.args.append(ArgType("*"))
                parsed_docstring.kw_args.sort(key=operator.attrgetter('argname'))
                self.args.extend(parsed_docstring.kw_args)
            

        # Generate function signature
        params = ", ".join([str(x) for x in self.args])
        if self.return_type:
            self.display_name = "{0} {1}({2}) -> {3}".format(self.def_key, self.name, params, self.return_type)
        else:
            self.display_name = "{0} {1}({2})".format(self.def_key, self.name, params)
        

    def dump(self, delim):
        space = ' '* delim
        for annot in self.annotations:
            print("{0}{1}".format(space, annot))
        print("{0}{1}".format(space, self.display_name))    
            

    def generate_tokens(self, apiview):
        """Generates token function"""
        apiview.add_whitespace()
        apiview.add_line_marker(self.namespace_id)
        if self.is_async:
            apiview.add_keyword("async")
            apiview.add_space()

        apiview.add_keyword("def")
        apiview.add_space()
        apiview.add_text(self.namespace_id, self.name)
        apiview.add_punctuation("(")
        # Add parameters
        args_count = len(self.args)
        for index in range(args_count):
                self.args[index].generate_tokens(apiview, False)
                # Add punctuation betwen types
                if index < args_count-1:
                    apiview.add_punctuation(",")
                    apiview.add_space()
        # Add "**kwargs" as last arg if keyword arg is present in function signature
        if self.kw_arg:
            apiview.add_punctuation(",")
            apiview.add_space()
            apiview.add_keyword(self.kw_arg.argname)
        apiview.add_punctuation(")")
        
        if self.return_type:
            apiview.add_punctuation("->")
            apiview.add_type(self.return_type, get_navigation_id(self.return_type))