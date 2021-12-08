import { LiveRequest, LiveResponse } from 'oav/lib/liveValidation/operationValidator'
import { StringMap } from '@azure-tools/openapi-tools-common'

export interface VirtualServerRequest extends LiveRequest {
    protocol: string
    localPort: number
}

export class VirtualServerResponse implements LiveResponse {
    constructor(
        public statusCode: string,
        public body: StringMap<unknown>,
        public headers: { [propertyName: string]: string } = {}
    ) {}

    public set(
        statusCode: string | number,
        body: StringMap<unknown>,
        headers?: { [propertyName: string]: string }
    ): any {
        this.statusCode = statusCode.toString()
        this.body = body
        if (headers) this.headers = headers
    }

    public setHeader(name: string, value: any) {
        this.headers[name] = value
    }
}
