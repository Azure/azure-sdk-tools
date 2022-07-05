// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License in the project root for license information.
import * as statsd from 'hot-shots';

export enum Metrics {
    Liveness = 'liveness',
    ApiCalls = 'apiCalls',
    InternalServerError = 'InternalServerError',
    BadRequest = 'BadRequest',
    NotFound = 'NotFound',
    Success = 'success',
}

export class MonitorClient {
    private stats = undefined;
    constructor(host: string, port: number, mock: boolean) {
        this.stats = new statsd.StatsD({
            host: host,
            port: port,
            mock: mock
        });
    }

    /**
     * call to emit metrics from the service.
     */
    public emitMetric(
        metric: Metrics,
        value: number,
        env: string,
        podName: string,
        nodeName: string,
        deploymentRegion: string,
        serviceName: string,
        dims?: { [key: string]: string | number }
    ): void {
        if (!dims) {
            dims = {};
        }
        dims.env = env;
        dims.pod = podName;
        dims.node = nodeName;
        dims.region = deploymentRegion;
        // Format must not be changed of the JSON object so it conforms with the Geneva statsd protocol.
        const stat = JSON.stringify({
            Namespace: serviceName,
            Metric: metric,
            Dims: dims
        });
        this.stats.gauge(stat, value);
    }
}
