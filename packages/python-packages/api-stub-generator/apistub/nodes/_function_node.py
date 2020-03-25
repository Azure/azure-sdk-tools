import logging
import inspect
import astroid
import operator
from inspect import Parameter
from ._docstring_parser import DocstringParser
from ._base_node import NodeEntityBase, get_qualified_name
from ._argtype import ArgType

logging.getLogger().setLevel(logging.INFO)


    
class FunctionNode(NodeEntityBase):
    """Function node to represent parsed function and method nodes
    """
    def __init__(self, namespace, parent_node, obj, is_module_level = False):
        super().__init__(namespace, parent_node, obj)
        self.annotations = []
        self.args = []
        self.return_type = None
        self.namespace_id = self.generate_id()
        self.is_class_method = False
        self.is_module_level  = is_module_level
        self._inspect()        


    def _inspect(self):
        code = inspect.getsource(self.obj).strip()
        # We cannot do "startswith" check here due to annotations or decorators present for functions
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
        # Add cls as first arg for class methods in API review tool
        if "@classmethod" in self.annotations:
            self.args.append(ArgType("cls"))
        
        # Find signature to find positional args and return type
        sig = inspect.signature(self.obj)
        logging.info("Processing function {0}{1}".format(self.name, str(sig)))
        params = sig.parameters
        self.kw_arg = None
        for argname in params:
            arg = ArgType(argname, get_qualified_name(params[argname].annotation))
            if params[argname].default != Parameter.empty:
                arg.default = str(params[argname].default)

            # Store handle to kwarg object to replace it later
            if params[argname].kind in [Parameter.VAR_KEYWORD, Parameter.KEYWORD_ONLY]:
                arg.argname = "**kwargs"
                self.kw_arg = arg

            self.args.append(arg)

        if sig.return_annotation:
            self.return_type = get_qualified_name(sig.return_annotation)

        # Parse docstring to get list of keyword args, type and default value for both positional and kw args and return type( if not already found in signature)
        docstring = ""
        # Refer docstring at class if this is constructor
        if self.name == "__init__":
            # docstring at __init__ method should be preferred first
            # if docstring is missing at __init__ method then use class level docstring for __init__
            if hasattr(self.obj, "__doc__"):
                docstring = self.obj.__doc__
            if not docstring and hasattr(self.parent_node.obj, "__doc__"):
                docstring = self.parent_node.obj.__doc__
        else:
            docstring = self.obj.__doc__

        if docstring:
            #  Parse doc string to find missing types, kwargs and return type
            parsed_docstring = DocstringParser(docstring)
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
                # Add seperator to differentiate pos_arg and keyword args
                self.args.append(ArgType("*"))
                parsed_docstring.kw_args.sort(key=operator.attrgetter('argname'))
                self.args.extend(parsed_docstring.kw_args)
                if self.kw_arg in self.args:
                    # Remove kwarg from list and add it at the end
                    self.args.remove(self.kw_arg)
                    self.args.append(self.kw_arg)
            

    def _generate_signature_token(self, apiview):
        apiview.add_punctuation("(")
        args_count = len(self.args)
        use_multi_line = args_count > 5
        # Show args in individual line if method has more than 4 args and use two tabs to properly aign them
        if use_multi_line:
            apiview.begin_group()
            apiview.begin_group()

        # Generate token for each arg
        for index in range(args_count):
            # Add new line if args are listed in new line
            if use_multi_line:
                apiview.add_new_line()
                apiview.add_whitespace()

            self.args[index].generate_tokens(apiview, True)
            # Add punctuation betwen types except for last one
            if index < args_count-1:
                apiview.add_punctuation(",")
                apiview.add_space()

        if use_multi_line:
            apiview.add_new_line()
            apiview.end_group()
            apiview.add_whitespace()
            apiview.add_punctuation(")")
            apiview.end_group()
        else:
            apiview.add_punctuation(")")


    def generate_tokens(self, apiview):
        """Generates token function"""

        # Add tokens for annotations
        for annot in self.annotations:
            apiview.add_whitespace()
            apiview.add_keyword(annot)
            apiview.add_new_line()

        apiview.add_whitespace()
        apiview.add_line_marker(self.namespace_id)
        if self.is_async:
            apiview.add_keyword("async")
            apiview.add_space()

        apiview.add_keyword("def")
        apiview.add_space()
        apiview.add_text(self.namespace_id, self.namespace_id if self.is_module_level else self.name)
        # Add parameters
        self._generate_signature_token(apiview)
        if self.return_type:
            apiview.add_punctuation("->")
            apiview.add_type(self.return_type)