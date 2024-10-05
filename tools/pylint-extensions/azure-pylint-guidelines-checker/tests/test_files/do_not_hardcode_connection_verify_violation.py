class InstanceVariableError:
    def __init__(self):
        self.connection_verify = True
        self.self = self


class VariableError:
    connection_verify = True


class FunctionArgumentsErrors:
    def create(connection_verify):
        pass

    client = create(connection_verify=False)


class FunctionArgumentsInstanceErrors:
    def __init__(self):
        client = self.create_client_from_credential(connection_verify=False)


class ReturnErrorFunctionArgument:
    def send(connection_verify):
        pass

    def sampleFunction(self):
        return self.send(connection_verify=True)


class ReturnErrorDict:
    def returnDict(self):

        return dict(
            connection_verify=False,
        )

class AnnotatedAssignment:
    connection_verify: bool = True



class AnnotatedSelfAssignment:
    def __init__(self):
        self.connection_verify: bool = True


