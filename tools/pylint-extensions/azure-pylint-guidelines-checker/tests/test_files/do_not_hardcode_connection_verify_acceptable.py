class InstanceVariableError:
    def __init__(self):
        self.connection_verify = None
        self.self = self


class VariableError:
    connection_verify = None


class FunctionArgumentsErrors:
    def create(connection_verify):
        pass

    client = create(connection_verify=None)


class FunctionArgumentsInstanceErrors:
    def __init__(self):
        client = self.create_client_from_credential(connection_verify=None)


class ReturnErrorFunctionArgument:
    def send(connection_verify):
        pass

    def sampleFunction(self):
        return self.send(connection_verify=None)


class ReturnErrorDict:
    def returnDict(self):

        return dict(
            connection_verify=None,
        )


class AnnotatedAssignment:
    connection_verify: bool = None


class AnnotatedSelfAssignment:
    def __init__(self):
        self.connection_verify: bool = None


