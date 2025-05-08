import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ImageInputProcessor } from "../src/input/ImageInputProcessor.js";
import { dirname, join } from "node:path";
import { fileURLToPath } from "url";
import { ComputerVisionClient } from "@azure/cognitiveservices-computervision";
import { ApiKeyCredentials } from "@azure/ms-rest-js";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

describe("Image OCR", () => {
    // it("should extract text from image", async () => {
    //     const imageProcessor = new ImageInputProcessor({
    //         languages: ["eng"],
    //         numWorkers: 1,
    //     });
    //     await imageProcessor.init();
    //     const imagePath = join(__dirname, "images/ocr-eng.png");
    //     const results = await imageProcessor.recognize([imagePath]);
    //     console.log("🚀 ~ it ~ results:", results);
    //     expect(results[0].text).toBe(
    //         "Debugger listening on ws://127.0.8.1:9239/9d83219c-0d2e-40e9-a990-e9bde4d586a8\n" +
    //             "For help, see: https://nodejs.org/en/docs/inspector\n" +
    //             "\n" +
    //             "Debugger attached.\n" +
    //             "\n" +
    //             "Debugger attached.\n" +
    //             "\n" +
    //             "Bot Started, app listening to { address: '::'. family: 'IPv6'. port: 3978 }\n"
    //     );
    // });

    it("is a test", async () => {
        async function ocrWithTypeScript() {
            // 1. 填入你的终结点（endpoint）和密钥（key）
            const endpoint =
                "https://wanl-test-ocr.cognitiveservices.azure.com/";
            // TODO: !!!!!!!!!!!!!!!!!!!!!!!!! dont upload this to github !!!!!!!!!!!!!!!!!!!!!!!!!!!
            const key = "xxxxxxxxxxxxxxxxxxxxxxxxx";

            // 2. 创建客户端
            const creds = new ApiKeyCredentials({
                inHeader: { "Ocp-Apim-Subscription-Key": key },
            });
            const client = new ComputerVisionClient(creds, endpoint);

            // 3. 指定待 OCR 的图片 URL
            const imageUrl =
                // "https://raw.githubusercontent.com/Azure-Samples/cognitive-services-sample-data-files/master/ComputerVision/Images/printed_text.jpg";
                join(__dirname, "images/ocr-eng.png");

            const readResponse = await client.read(imageUrl);
            const operationLocation = readResponse.operationLocation;
            if (!operationLocation) {
                throw new Error(
                    "未能获取 Operation-Location，请检查请求是否正确。"
                );
            }
            const operationId = operationLocation.split("/").slice(-1)[0];

            // 5. 轮询直到识别完成
            let result;
            while (true) {
                result = await client.getReadResult(operationId);
                if (
                    result.status !== "notStarted" &&
                    result.status !== "running"
                ) {
                    break;
                }
                await new Promise((resolve) => setTimeout(resolve, 1000));
            }
            console.log("🚀 ~ ocrWithTypeScript ~ result:", result);

            // 6. 处理并打印识别结果
            if (
                result.status === "succeeded" &&
                result.analyzeResult?.readResults
            ) {
                for (const page of result.analyzeResult.readResults) {
                    console.log(`--- 第 ${page.page} 页 ---`);
                    for (const line of page.lines) {
                        console.log(line.text);
                    }
                }
            } else {
                console.error("OCR 识别未成功，请检查输入和密钥。");
            }
        }

        await ocrWithTypeScript();
    });
});
