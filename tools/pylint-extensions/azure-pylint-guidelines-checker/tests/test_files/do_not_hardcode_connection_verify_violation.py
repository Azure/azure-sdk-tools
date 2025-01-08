class InstanceVariableError:
    def __init__(self):
        self.connection_verify = True
        self.self = self


class VariableError:
    connection_verify = True


class FunctionKeywordArgumentsErrors:
    def create(x, connection_verify):
        pass

    client = create(connection_verify=False, x=0)


class FunctionArgumentsInstanceErrors:
    def __init__(self):
        client = self.create_client_from_credential(connection_verify=False)


class ReturnErrorFunctionArgument:
    def send(connection_verify):
        pass

    def sample_function(self):
        return self.send(connection_verify=True)


class ReturnErrorDict:
    def return_dict(self):

        return dict(
            connection_verify=False,
        )

class AnnotatedAssignment:
    connection_verify: bool = True


class AnnotatedSelfAssignment:
    def __init__(self):
        self.connection_verify: bool = True


