import { ActivityTypes, MemoryStorage, TurnContext } from "botbuilder";
import * as path from "path";
import config from "../config.js";

// See https://aka.ms/teams-ai-library to learn more about the Teams AI library.
import { Application, ActionPlanner, PromptManager } from "@microsoft/teams-ai";
import { FakeModel } from "../models/FakeModel.js";
import { FeedbackReaction, sendFeedback } from "../backend/feedback.js";

// Create AI components
const model = new FakeModel({
    apiKey: config.azureOpenAIKey,
    tenantId: config.azureOpenAIDeploymentName,
    // TODO: make /completion endpoint configurable
    endpoint: config.azureOpenAIEndpoint + "/completion",
});
const dir = import.meta.dirname;
const prompts = new PromptManager({
    promptsFolder: path.join(dir, "../prompts"),
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
    // const conversation = context.activity.value?.conversation;
    switch (action) {
        case "feedback-like":
            await sendFeedback(["test good"], FeedbackReaction.good);
            await context.sendActivity(
                "You liked my service. Thanks for your feedback!"
            );
            break;
        case "feedback-dislike":
            await sendFeedback(["test bad"], FeedbackReaction.bad);
            await context.sendActivity(
                "You disliked my service. Thanks for your feedback!"
            );
            break;
        default:
            break;
    }
});
export default app;
