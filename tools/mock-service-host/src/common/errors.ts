export function createErrorBody(code: any, msg: any) {
    return { error: { code: String(code), message: String(msg) } }
}

export enum HttpStatusCode {
    OK = 200,
    CREATED = 201,
    ACCEPTED = 202,
    NO_CONTENT = 204,
    BAD_REQUEST = 400,
    NOT_FOUND = 404,
    // ----- start of mock-service-host error codes -----
    OPERATION_NOT_FOUND = 452,
    VALIDATION_FAIL = 453,
    EXAMPLE_NOT_FOUND = 454,
    EXAMPLE_NOT_MATCH = 455,
    RESOURCE_NOT_FOUND = 456,
    NO_PARENT_RESOURCE = 457,
    HAS_CHILD_RESOURCE = 458,
    NO_RESPONSE_DEFINED = 459,
    LRO_CALLBACK_NOT_FOUND = 460,
    WRONG_EXAMPLE_RESPONSE = 461,
    // ----- end of mock-service-host error codes -----
    INTERNAL_SERVER = 500
}

export class OperationalError extends Error {
    public readonly httpCode: HttpStatusCode
    public readonly target: string

    constructor(httpCode: HttpStatusCode, message: string, target: string) {
        super(message)
        Object.setPrototypeOf(this, new.target.prototype)

        this.httpCode = httpCode
        this.target = target
    }

    public ToAzureResponse() {
        return {
            error: {
                code: this.httpCode.toString(),
                message: this.message,
                target: this.target
            }
        }
    }
}

export class OperationNotFound extends OperationalError {
    constructor(detail = '') {
        super(HttpStatusCode.OPERATION_NOT_FOUND, 'Operation Not Found', detail)
    }
}

export class RequestError extends OperationalError {
    constructor(httpCode: HttpStatusCode, message: string, detail = '') {
        super(httpCode, message, detail)
    }
}

export class ResourceError extends OperationalError {
    constructor(httpCode: HttpStatusCode, message: string, detail = '') {
        super(httpCode, message, detail)
    }
}

export class NoParentResource extends ResourceError {
    constructor(detail = '') {
        super(HttpStatusCode.HAS_CHILD_RESOURCE, 'Need to create parent resource first', detail)
    }
}

export class HasChildResource extends ResourceError {
    constructor(detail = '') {
        super(HttpStatusCode.HAS_CHILD_RESOURCE, 'Need to delete child resource first', detail)
    }
}

export class ResourceNotFound extends ResourceError {
    constructor(detail = '') {
        super(HttpStatusCode.RESOURCE_NOT_FOUND, 'Resource has not been created', detail)
    }
}

export class ValidationFail extends RequestError {
    constructor(detail = '') {
        super(HttpStatusCode.VALIDATION_FAIL, 'Request Validation Failed', detail)
    }
}

export class ExampleNotFound extends RequestError {
    constructor(detail = '') {
        super(HttpStatusCode.EXAMPLE_NOT_FOUND, 'Example Not Found', detail)
    }
}

export class ExampleNotMatch extends RequestError {
    constructor(detail = '') {
        super(HttpStatusCode.EXAMPLE_NOT_MATCH, 'Example Not Match', detail)
    }
}

export class NoResponse extends OperationalError {
    constructor(detail = '') {
        super(HttpStatusCode.NO_RESPONSE_DEFINED, 'No Response Defined', detail)
    }
}

export class IntentionalError extends OperationalError {
    constructor(httpCode = HttpStatusCode.INTERNAL_SERVER) {
        super(httpCode, 'Intentional Error', '')
    }
}

export class LroCallbackNotFound extends OperationalError {
    constructor(detail = '') {
        super(
            HttpStatusCode.LRO_CALLBACK_NOT_FOUND,
            "Can't find a GET operation nearby this lro operation to work as callback url",
            detail
        )
    }
}

export class WrongExampleResponse extends OperationalError {
    constructor(detail = '') {
        super(HttpStatusCode.WRONG_EXAMPLE_RESPONSE, 'Wrong response example for operation', detail)
    }
}
