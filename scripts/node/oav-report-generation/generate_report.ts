#!/usr/bin/env node
import {Argv} from "yargs";
const fs = require('fs/promises');
const path = require('path');

class PayloadObject {
    PayloadLocation: string;

    constructor(payloadLocation: string){
        this.PayloadLocation = payloadLocation;
    }

    async getPayload(): Promise<Array<any>> {
        var data = await fs.readFile(this.PayloadLocation)

        console.log(data.toString());

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

        let obj = new PayloadObject(args.payload);
        await obj.getPayload();
    }).argv;