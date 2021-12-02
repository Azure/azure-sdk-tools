#!/usr/bin/env node
import {Argv} from "yargs";
const fs = require('fs/promises');
const path = require('path');

import * as oav from "oav";

interface PayloadEntry {
    message: string;
    level: string;
}

class PayloadObject {
    PayloadLocation: string;

    constructor(payloadLocation: string){
        this.PayloadLocation = payloadLocation;
    }

    getPayloadObjects(content: string): Array<PayloadEntry>{
        let result = JSON.parse(content);
        

        console.log(result);
        return [];
    }

    async getPayload(): Promise<Array<any>> {
        var data = await fs.readFile(this.PayloadLocation)

        var allFailures = data.toString();
        this.getPayloadObjects(allFailures);
        
        return [];
    }
}


// functionality start
require('yargs')
    .command('html', "Generate an HTML report", (yargs: Argv) => {
        yargs.option('payload', {
            alias: 'p',
            describe: "The targeted oav output."
        }).option('swagger', {
            alias: 's',
            describe: "The targeted oav swagger."
        })
    }, async (args: any) => {
        console.log(`Input Payload: ${args.payload}. Input Swagger: ${args.swagger}`)

        console.log(oav)
        // let obj = new PayloadObject(args.payload);
        // await obj.getPayload();
    }).argv;