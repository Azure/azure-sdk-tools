import { ActivityTypes, MemoryStorage, TurnContext } from "botbuilder";
import * as path from "path";
import config from "../config";

// See https://aka.ms/teams-ai-library to learn more about the Teams AI library.
import { Application, ActionPlanner, PromptManager } from "@microsoft/teams-ai";
import { RAGModel } from "../models/RAGModel";

// Create AI components
const model = new RAGModel({
    apiKey: config.azureOpenAIKey,
    tenantId: config.azureOpenAIDeploymentName,
    endpoint: config.azureOpenAIEndpoint,
});
const prompts = new PromptManager({
    promptsFolder: path.join(__dirname, "../prompts"),
});
const planner = new ActionPlanner({
    model,
    prompts,
    defaultPrompt: "chat",
});

// Define storage and application
const storage = new MemoryStorage();
const app = new Application({
    storage,
    ai: {
        planner,
        enable_feedback_loop: true,
    },
});

app.feedbackLoop(async (context, state, feedbackLoopData) => {
    //add custom feedback process logic here
    console.log("Your feedback is " + JSON.stringify(context.activity.value));
});

const isSubmitMessage = async (ctx: TurnContext) =>
    ctx.activity.type === ActivityTypes.Message && !!ctx.activity.value?.action;

app.activity(isSubmitMessage, async (context: TurnContext) => {
    const action = context.activity.value?.action;
    switch (action) {
        case "feedback-like":
            await context.sendActivity(
                "You liked my service. Thanks for your feedback!"
            );
            break;
        case "feedback-dislike":
            await context.sendActivity(
                "You disliked my service. Thanks for your feedback!"
            );
            break;
        default:
            // await context.sendActivity("请点击按钮以获取更多帮助。");
            break;
    }
    const text = context.activity.text;
    // await context.sendActivity(`你刚才说：${text}`);
});
export default app;
