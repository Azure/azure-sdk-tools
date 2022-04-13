// Disable EsLint for this file since all contents are copied from autorest.go, keeping the original shape will make maintenance easier
/* eslint-disable */

import { Operation, OperationGroup, Parameter, SchemaResponse } from '@autorest/codemodel';
import { isLROOperation, isPageableOperation, isSchemaResponse } from '@autorest/go/dist/common/helpers';
import { formatParameterTypeName, getMethodParameters, getResponseEnvelopeName } from '@autorest/go/dist/generator/helpers';
import { values } from '@azure-tools/linq';

// homo structureed with getAPIParametersSig() in autorest.go
export function getAPIParametersSig(op: Operation): Array<[string, string, Parameter]> {
    const methodParams = getMethodParameters(op);
    const params = new Array<[string, string, Parameter]>();
    if (!isPageableOperation(op) || isLROOperation(op)) {
        params.push(['ctx', 'context.Context', undefined]);
    }
    for (const methodParam of values(methodParams)) {
        params.push([methodParam.language.go.name, formatParameterTypeName(methodParam), methodParam]);
    }
    return params;
}

export function getClientParametersSig(group: OperationGroup): Array<[string, string, Parameter]> {
    const params = [];

    for (const parameter of values((group.language.go?.clientParams || []) as Parameter[])) {
        params.push([parameter.language.go.name, formatParameterTypeName(parameter), parameter]);
    }
    return params;
}

// homo structured with generateReturnsInfo() in autorest.go
export function generateReturnsInfo(op: Operation, apiType: 'api' | 'op' | 'handler'): string[] {
    let returnType = getResponseEnvelopeName(op);
    if (isLROOperation(op)) {
        switch (apiType) {
            case 'api':
                // this should go away once we can type alias a generic type
                if (isPageableOperation(op)) {
                    returnType = `*armruntime.Poller[*runtime.Pager[${getResponseEnvelopeName(op)}]]`;
                } else {
                    returnType = `*armruntime.Poller[${getResponseEnvelopeName(op)}]`;
                }
                break;
            case 'handler':
                // we only have a handler for operations that return a schema
                if (isPageableOperation(op)) {
                    // we need to consult the final response type name
                    returnType = getResponseEnvelopeName(op);
                } else {
                    throw new Error(`handler being generated for non-pageable LRO ${op.language.go.name} which is unexpected`);
                }
                break;
            case 'op':
                // change to get final response type
                // TODO need to check pageable LRO
                returnType = getResponseEnvelopeName(op);
        }
    } else if (isPageableOperation(op)) {
        switch (apiType) {
            case 'api':
            case 'op':
                // pager operations don't return an error
                return [`*runtime.Pager[${returnType}]`];
        }
    }
    return [returnType, 'error'];
}

// returns the schema response for this operation.
// if multi-response operations return first 20x.
export function getSchemaResponse(op: Operation): SchemaResponse | undefined {
    if (!op.responses) {
        return undefined;
    }
    // get the list and count of distinct schema responses
    const schemaResponses = new Array<SchemaResponse>();
    for (const response of values(op.responses)) {
        // perform the comparison by name as some responses have different objects for the same underlying response type
        if (
            isSchemaResponse(response) &&
            !values(schemaResponses)
                .where((sr) => sr.schema.language.go!.name === response.schema.language.go!.name)
                .any()
        ) {
            schemaResponses.push(response);
        }
    }
    if (schemaResponses.length === 0) {
        return undefined;
    } else if (schemaResponses.length === 1) {
        return schemaResponses[0];
    }

    // multiple schema responses, for LROs find the best fit.

    // for LROs, there are a couple of corner-cases we need to handle WRT response types.
    // 1. 200 Foo / 20x Bar - we take Foo and display a warning
    // 2. 201 Foo / 202 Bar - this is a hard error
    // 3. 200 void / 20x Bar - we take Bar
    // since we always assume responses[0] has the return type we need to fix up
    // the list of responses so that it points to the schema we select.

    // multiple schemas, find the one for 200 status code
    // note that case #3 was handled earlier
    let with200: SchemaResponse | undefined;
    for (const response of values(schemaResponses)) {
        if ((<Array<string>>response.protocol.http!.statusCodes).indexOf('200') > -1) {
            with200 = response;
            break;
        }
    }
    if (with200 === undefined) {
        throw new Error(`LRO ${op.language.go!.clientName}.${op.language.go!.name} contains multiple response types which is not supported`);
    }
    return with200;
}
