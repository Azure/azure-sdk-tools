#!/usr/bin/env node
import {Argv} from "yargs";
const fs = require('fs/promises');
const path = require('path');

import * as oav from "oav";

import * as Mustache from 'mustache';

interface PayloadEntry {
    message: string;
    level: string;
}

// probably unnecessary now that we're pulling the results directly in from oav instead of processing a results payload.
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

// used to pass data to the template rendering engine
class CoverageView {
    constructor(packageName: string, validationResults: Array<oav.TrafficValidationIssue>) {
        this.package = packageName;
        this.validationResults = validationResults;
    }

    getGeneralErrors(): number{
        return this.validationResults.length;
    }

    package: string;
    date: Date;
    errorArray: Array<PayloadEntry>;
    validationResults: Array<oav.TrafficValidationIssue>;
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
        let errors: Array<oav.TrafficValidationIssue> = [];

        try {
            errors = await oav.validateTrafficAgainstSpec(args.swagger, args.payload, {});
        }
        catch {}
        
        let template = await fs.readFile("template/layout-base.mustache", "utf-8");

        let view = new CoverageView("azure-data-tables", errors);
        let text = Mustache.render(template, view);
        debugger;

        let writeResult = await fs.writeFile("report.html", text, "utf-8");
    }).argv;