import { test, expect, Locator } from "@playwright/test";
import  { FormData, File } from "formdata-node";
import { FormDataEncoder } from "form-data-encoder"
import { Readable } from "node:stream"
import fetch from "node-fetch";

import * as path from "path";
import * as fs from "fs";

const fixturesDir = process.env.FIXTURE_DIR as string;
const baseURL = process.env.BASE_URL as string;
const apiKey = process.env.APIVIEW_API_KEY as string;

test.describe('CodeLine Section State Management', () => {
    // Test Reviews Ids
    const testReviewIds = {};

    test.beforeAll(async ({}, testInfo) => {
        // Create automatic Reviews using existing token files
        await addAutoReview("webpubsub-data-plane-WebPubSub.Baseline.json", fixturesDir, "Swagger", testReviewIds);
        await new Promise(resolve => setTimeout(resolve, 5000)); // Give the upload sometime to complete
    });

    test('codeLine section expands and collapses', async ({ page }) => {
        test.slow();
        const swaggerTestReview = testReviewIds["Swagger"][0];
        await page.goto(swaggerTestReview);

        // Select one row-fold-caret btn to use for test
        const sectionTriggerBtns = await page.locator(".row-fold-caret").all();
        const btnToTest = sectionTriggerBtns[2];

        // Select the parent row heading class (the Section Heading)
        const sectionHeadingRowClass = await btnToTest.evaluate((el) => {
            let currEl = el;
            while (currEl && !currEl.classList.contains("code-line")) {
                currEl = currEl.parentElement!;
            }
            return currEl.classList[1];
        });

        // Ensure that all section content does not exist
        const sectionContentClass = sectionHeadingRowClass.replace("heading", "content");
        let sectionContentRows = await page.locator(`.${sectionContentClass}`).all();
        expect(sectionContentRows.length).toBe(1);
        
        // click on row caret (the test subject)
        btnToTest.click();
        await page.waitForLoadState('networkidle', { timeout: 50000 }); // Give the network few seconds moment to load

        // Reselect section content rows and ensure they are present
        sectionContentRows = await page.locator(`.${sectionContentClass}`).all();
        expect(sectionContentRows.length).toBe(10);

        // Click row caret again
        btnToTest.click();

        // Reselect section content rows and ensure they are hidden
        sectionContentRows = await page.locator(`.${sectionContentClass}`).all();

        expect(sectionContentRows.length).toBe(10);
        for (const l of sectionContentRows) {
            await l.waitFor({state: "hidden"});
            const classes = await l.evaluate((el) => Array.from(el.classList));
            await expect(classes).toContain("d-none");
        }
    });

    test('codeLine subSection expands and collapses and codeLine section collapses subSection', async ({ page }) => {
        test.slow();
        const swaggerTestReview = testReviewIds["Swagger"][0];
        await page.goto(swaggerTestReview);

        // Select one row-fold-caret btn to use for test
        const sectionTriggerBtns = await page.locator(".row-fold-caret").all();
        const btnToTest = sectionTriggerBtns[2];

        // Select the parent row heading class (the Section Heading)
        const sectionHeadingRowClass = await btnToTest.evaluate((el) => {
            let currEl = el;
            while (currEl && !currEl.classList.contains("code-line")) {
                currEl = currEl.parentElement!;
            }
            return currEl.classList[1];
        });

        // click on row caret (the test subject)
        btnToTest.click();

        // Select subSection row thats a heading
        let subSectionParent;
        const sectionContentClass = sectionHeadingRowClass.replace("heading", "content");
        await page.waitForLoadState('networkidle', { timeout: 50000 }); // Give the UI few seconds moment to load

        let sectionContentRows = await page.locator(`.${sectionContentClass}`).all();
        for (const l of sectionContentRows)
        {
            let parentClass = (await l.evaluate((el) => Array.from(el.classList))).filter(c => c.match(/_parent_/));
            if (parentClass.length > 0)
            {
                subSectionParent = l;
            }
        }

        // Ensure all sub section content is hidden
        const subSectionContentRows : Locator [] = []
        for (const l of sectionContentRows) {
            const sectionContentClasses = await l.evaluate((el) => Array.from(el.classList));
            if (sectionContentClasses.filter(c => c.match(/lvl_2_child_/)).length > 0) {
                expect(Array.from(sectionContentClasses)).toContain("d-none");
                subSectionContentRows.push(l);
            }
                
        } 
        expect(subSectionContentRows.length).toBe(7);

        // Find row caret thats child of subSectionHeading and click it
        subSectionParent.locator(".row-fold-caret").click();
        await page.waitForLoadState('load'); // Give the network few seconds moment to load
        await page.waitForTimeout(10000);

        // Ensure subsection content is now shown
        sectionContentRows = await page.locator(`.${sectionContentClass}`).all();
        for (const l of sectionContentRows) {
            const sectionContentClasses = await l.evaluate((el) => Array.from(el.classList));
            if (sectionContentClasses.filter(c => c.match(/lvl_2_child_/))) {
                expect(Array.from(sectionContentClasses)).not.toContain("d-none");
            }  
        }
        
        // Click heading row caret to close section and subSection
        btnToTest.click();

        // Ensure section and subsection is hidden
        sectionContentRows = await page.locator(`.${sectionContentClass}`).all();
        expect(sectionContentRows.length).toBe(10);
        for (const l of sectionContentRows) {
            await l.waitFor({state: "hidden"});
            const classes = await l.evaluate((el) => Array.from(el.classList));
            await expect(classes).toContain("d-none");
        }
    });

    test('codeLine secton and subSection stores heading line numbers in session storage', async ({ page }) => {
        test.slow();
        const swaggerTestReview = testReviewIds["Swagger"][0];
        await page.goto(swaggerTestReview);

        // Select one row-fold-caret btn to use for test
        const sectionTriggerBtns = await page.locator(".row-fold-caret").all();
        const btnToTest = sectionTriggerBtns[2];

        // Select the parent row heading class (the Section Heading)
        const sectionHeadingRowClass = await btnToTest.evaluate((el) => {
            let currEl = el;
            while (currEl && !currEl.classList.contains("code-line")) {
                currEl = currEl.parentElement!;
            }
            return currEl.classList[1];
        });

        // assert value in session storage
        let shownSectionHeadingLineNumbers = await page.evaluate(() => {
            return window.sessionStorage.getItem("shownSectionHeadingLineNumbers");
        });
        expect(shownSectionHeadingLineNumbers).toBeNull();

        // click on row caret (the test subject)
        btnToTest.click();
        await page.waitForLoadState('networkidle', { timeout: 50000 }); // Give the network few seconds moment to load

        // assert value in session storage
        shownSectionHeadingLineNumbers = await page.evaluate(() => {
            return window.sessionStorage.getItem("shownSectionHeadingLineNumbers");
        });
        expect(shownSectionHeadingLineNumbers).toBe("7");

        // Select subSection row thats a heading
        let subSectionParent;
        const sectionContentClass = sectionHeadingRowClass.replace("heading", "content");
        await page.waitForLoadState('networkidle', { timeout: 50000 }); // Give the UI few seconds moment to load

        let sectionContentRows = await page.locator(`.${sectionContentClass}`).all();
        for (const l of sectionContentRows)
        {
            let parentClass = (await l.evaluate((el) => Array.from(el.classList))).filter(c => c.match(/_parent_/));
            if (parentClass.length > 0)
            {
                subSectionParent = l;
            }
        }

        // Find row caret thats child of subSectionHeading and click it

        subSectionParent.locator(".row-fold-caret").click();
        await page.waitForLoadState('networkidle', { timeout: 50000 }); // Give the network few seconds moment to load
        await page.waitForFunction((sectionContentClass) => document.querySelectorAll(`.${sectionContentClass}`).length > 1, sectionContentClass);// Give the UI few seconds moment to load
        await page.waitForTimeout(10000);

        // assert value in session storage
        let shownSubSectionHeadingLineNumbers = await page.evaluate(() => {
            return window.sessionStorage.getItem("shownSubSectionHeadingLineNumbers");
        });
        expect(shownSubSectionHeadingLineNumbers).toBe("10");

        // click on row caret again (the test subject)
        btnToTest.click();
        await page.waitForLoadState('networkidle', { timeout: 50000 }); // Give the network few seconds moment to load
        await page.waitForSelector(sectionContentClass, { state: 'hidden' });// Give the UI few seconds moment to load
        await page.waitForTimeout(10000);

        shownSectionHeadingLineNumbers = await page.evaluate(() => {
            return window.sessionStorage.getItem("shownSectionHeadingLineNumbers");
        });
        shownSubSectionHeadingLineNumbers = await page.evaluate(() => {
            return window.sessionStorage.getItem("shownSubSectionHeadingLineNumbers");
        });
        expect(shownSectionHeadingLineNumbers).toBe("");
        expect(shownSubSectionHeadingLineNumbers).toBe("");
    });
});

/**
* Add an Auto Review to APIView using a Token File
* @param { String } fileName (full filename including extension)
* @param { String } fileDirectory
*/
async function addAutoReview(fileName: string, fileDirectory: string, language: string, testReviewIds: {}) {
    const swaggerTokenContent = fs.readFileSync(path.resolve(path.join(fileDirectory, fileName)), "utf8");
    const label = `${fileName} Review Label`;
    const autoUplloadUrl = `${baseURL}/AutoReview/UploadAutoReview?label=${label}`;

    const formData = new FormData();
    const file = new File([swaggerTokenContent], fileName);
    formData.set("file", file);
    const encoder = new FormDataEncoder(formData);
            
    const requestOptions = {
        method: "POST",
        headers: {
            "ApiKey": apiKey,
            "Content-Type": encoder.headers["Content-Type"],
            "Content-Length": encoder.headers["Content-Length"]
        },
        body: Readable.from(encoder)
    }

    await fetch(autoUplloadUrl, requestOptions)
        .then(response => response.text())
        .then(result => {
            if (Object.values(testReviewIds).includes(language)) {
                testReviewIds[language].push(result);
            }
            else {
                testReviewIds[language] = [];
                testReviewIds[language].push(result);
            }
        })
        .catch(error => console.log("error uploading auto review", error));
}

