#!/usr/bin/env node
import {Argv} from "yargs";
const fs = require('fs/promises');
const path = require('path');
import * as oav from "oav";
import * as Mustache from 'mustache';
import { debug } from "console";

interface PayloadEntry {
    message: string;
    level: string;
}

// used to pass data to the template rendering engine
class CoverageView {
    constructor(packageName: string, validationResults: Array<oav.TrafficValidationIssue>) {
        this.package = packageName;
        this.validationResults = validationResults;
        this.generatedDate = new Date();
    }

    getGeneralErrors(): Array<oav.TrafficValidationIssue>{
        return this.validationResults.filter((x, idx) => {
        });
    }

    getTotalErrors(): number {
        return this.validationResults.length;
    }

    getPayloadErrors(): Array<oav.TrafficValidationIssue>{
        return this.validationResults.filter((x, idx) => {
        });
    } 

    package: string;
    generatedDate: Date;
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

        let writeResult = await fs.writeFile("report.html", text, "utf-8");
    }).argv;