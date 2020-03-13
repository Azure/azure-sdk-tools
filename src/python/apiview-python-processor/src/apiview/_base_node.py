import logging
import inspect
import astroid
import re
from inspect import Parameter

logging.getLogger().setLevel(logging.INFO)

docstring_regex = "(?<!:):[a-z\s]+\S+:(?!:)"

docstring_types = ["param", "type", "paramtype", "keyword", "rtype"]

class ArgType:
    """Represents Arguement type
    """

    def __init__(self, name, argtype = None, default = None):
        super().__init__()
        self.argname = name
        self.argtype = argtype
        self.default = default
        self.is_async = False

    def __str__(self):
        value = self.argname
        if self.argtype:
            value += ": {}".format(self.argtype)

        if self.default:
            value += " = {}".format(self.default)
        return value

    def __repr__(self):
        return self.argname

    def is_internal_type(self):
        pass # todo


class Docstring:
    """This represents a parsed doc string which has list of positional and keyword arguements and return type
    """

    def __init__(self):
        super().__init__()        
        self.pos_args = []
        self.kw_args = []
        self.ret_type = None

    
    def parse(self, doc_string):
        """Returns a parsed docstring object
        """
        if not doc_string:
            return

        lines = doc_string.splitlines()
        data_re = re.compile(docstring_regex)
        # filter lines containing required doctring keys
        lines = ([l.strip() for l in lines])
        for n in range(0, len(lines)):
            # Find matching keys like param, paramtype, keyword etc
            line = lines[n]
            s_data = data_re.match(line)
            if not s_data or not s_data.group():
                continue
            
            # e.g group should be something like :param int size:
            keys = s_data.group()[1:-1].split()
            if keys[0] not in docstring_types:
                continue

            # if keys length is 3 then param or keyword name and type is present
            # if length is 2 and it is for keyword or param then type is missing
            # if type is missing then parse next line to find type if it is given
            # if length is 1 then consider this doc type only for rtype

            if keys[0] == 'rtype' and len(keys) == 1:
                # This is return type
                self.ret_type = line.split(":")[-1].strip()
                if self.ret_type == "None":
                    self.ret_type = None

            if keys[0]  == 'param' or keys[0] == 'keyword':
                arg = ArgType(keys[-1])
                # Set type if available in same docstring line
                if len(keys) == 3:
                    arg.argtype = keys[1]
                elif len(keys) == 2:
                    # type is missing in current line
                    # parse next lines and check if it has type
                    n += 1
                    while n < len(lines) and not data_re.match(lines[n]):
                        n += 1
                        continue

                    if n < len(lines):
                        line = lines[n]
                        type_data = data_re.match(line)
                        found_type = False
                        if type_data and type_data.group():
                            type_keys = type_data.group()[1:-1].split()
                            # Make sure this is type or keywordtype and it is for the param we parsed above
                            if type_keys[0] in ["type", "keywordtype"] and type_keys[1] == arg.argname:
                                found_type = True
                                # some docstring mentions type name in next line and some mentions in same line
                                # type name comes after parsed group value
                                # if parsed group value is same as current line then type name is in next line
                                if type_data.group() == line:
                                    # type value is given in next line
                                    arg.argtype = lines[n+1].strip() if n+1 < len(lines) else None
                                else: 
                                    arg.argtype = line.split(":")[-1].strip()

                        # reset line index to previous if type is not found
                        if not found_type:
                            n -= 1

                # add parsed arg to positional arg or keyword arg
                arg_dest = self.pos_args if keys[0] == 'param' else self.kw_args
                arg_dest.append(arg)

        
                    
    def _format_type(self, arg: ArgType):
        if arg and arg.argtype and " " in arg.argtype:
            types = arg.argtype.split(" ")
            types = list(filter(lambda x: x and x != 'or', types))
            if len(types) > 1:
                arg.argtype = "Union[{}]".format(", ".join(types))

        return arg

                       
class NodeEntityBase:

    
    def __init__(self, namespace, parent_node, obj):
        super().__init__()
        self.namespace = namespace
        self.parent_node = parent_node
        self.obj = obj
        self.name = obj.__name__
        self.qualname = self.name
        if hasattr(obj, "__qualname__"):
            self.qualname = obj.__qualname__
        self.display_name = self.name        
        self.child_nodes = []
        self._generate_id()
        
    
    def get_display_name(self):
        return self.display_name    


    def get_child_nodes(self):
        return child_nodes


    def get_name(self):
        return name


    def _generate_id(self):
        self.id = self.namespace
        if self.parent_node:
            self.id = "{0}:{1}".format(self.parent_node.id, self.name)
        

    def dump(self, delim = 0):
        if not self.child_nodes:
            return None
        print("\n{}\n".format(self.display_name))
        for n in self.child_nodes:
            n.dump(delim+5)

    @classmethod
    def get_qualified_name(cls, obj):
        if obj is Parameter.empty:
            return None

        name = obj
        if hasattr(obj, "__name__"):
            name = getattr(obj, "__name__")
        elif hasattr(obj, "__qualname__"):
            name = getattr(obj, "__qualname__")

        module_name = ""
        if hasattr(obj, "__module__"):
            module_name = getattr(obj, "__module__")
        if module_name and module_name.startswith('azure'):
            return "{0}.{1}".format(module_name, name)

        return name

