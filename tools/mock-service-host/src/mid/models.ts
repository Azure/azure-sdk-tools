import { LiveRequest, LiveResponse } from 'oav/lib/liveValidation/operationValidator'
import { StringMap } from '@azure-tools/openapi-tools-common'
import { createErrorBody } from '../common/errors'

export interface VirtualServerRequest extends LiveRequest {
    protocol: string
}

export class VirtualServerResponse implements LiveResponse {
    constructor(
        public statusCode: string,
        public body: StringMap<unknown>,
        public headers: { [propertyName: string]: string } = {}
    ) {}

    public set(
        statusCode: string | number,
        body: StringMap<unknown> | string,
        headers?: { [propertyName: string]: string }
    ): any {
        this.statusCode = statusCode.toString()
        if (typeof body === 'string') this.body = createErrorBody(statusCode, body)
        else this.body = body
        if (headers) this.headers = headers
    }

    public setHeader(name: string, value: any) {
        this.headers[name] = value
    }
}
