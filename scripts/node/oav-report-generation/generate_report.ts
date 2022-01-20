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
    constructor(packageName: string, validationResults: Array<oav.TrafficValidationIssue>, language: string) {
        this.package = packageName;
        this.validationResults = validationResults;
        this.generatedDate = new Date();
        this.language = language;

        this.generalErrorResults = new Map();

    }

    formatGeneratedDate(): string {
        let day = this.generatedDate.getDate();
        let month = this.generatedDate.getMonth() + 1;
        let year = this.generatedDate.getFullYear();
        let hours = this.generatedDate.getHours();
        let minutes = this.generatedDate.getMinutes();

        return year + "-" 
            + (month < 10 ? "0" + month : month) + "-" 
            + (day < 10 ? "0" + day : day) + " at " 
            + hours + ":" + (minutes < 10 ? "0" + minutes : minutes) + (hours < 13 ? "AM" : "PM");
    }

    getTotalErrors(): number {
        return this.validationResults.length;
    }

    getGeneralErrors(): Array<oav.TrafficValidationIssue>{
        return this.validationResults.filter((x, idx) => {
            return x.errors.length > 0;
        });
    }

    getTotalGeneralErrors(): number {
        return this.getGeneralErrors().length;
    }

    getRunTimeErrors(): Array<oav.TrafficValidationIssue>{
        return this.validationResults.filter((x, idx) => {
            return x.runtimeExceptions.length > 0;
        });
    }

    getTotalRunTimeErrors(): number {
        return this.getRunTimeErrors().length;
    }

    package: string;
    generatedDate: Date;
    validationResults: Array<oav.TrafficValidationIssue>;
    language: string;
    generalErrorResults: Map<string, Array<oav.TrafficValidationIssue>>;
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
        }).option('package', {
            default: "azure-data-tables",
            describe: "The targeted package name"
        })
        .option('language', {
            alias: 'l',
            default: "python",
            describe: "The targeted oav swagger."
        })
    }, async (args: any) => {
        console.log(`Input Payload: ${args.payload}. Input Swagger: ${args.swagger}`)
        let errors: Array<oav.TrafficValidationIssue> = [];

        errors = await oav.validateTrafficAgainstSpec(args.swagger, args.payload, {});
        let template = await fs.readFile("template/layout-base.mustache", "utf-8");

        let view = new CoverageView(args.package, errors, args.language);

        let general_errors = view.getGeneralErrors();
        let runtime_errors = view.getRunTimeErrors();
        
        console.log(general_errors);
        console.log(runtime_errors);

        let text = Mustache.render(template, view);
        debugger;

        let writeResult = await fs.writeFile("report.html", text, "utf-8");
    }).argv;