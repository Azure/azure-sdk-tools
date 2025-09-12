import { app } from "@azure/functions";
import { FeedbackService } from "../services/AnalyticsServices/FeedbackService";
import { RecordService } from "../services/AnalyticsServices/RecordService";
import { ChannelConfigService } from "../services/AnalyticsServices/ChannelConfigService";

// Register the Azure Function
app.http("feedbacks", {
    methods: ["GET"],
    authLevel: "function",
    handler:(req, ctx) => new FeedbackService().handler(req, ctx),
});
app.http("records", {
    methods: ["GET"],
    authLevel: "function",
    handler: (req, ctx) => new RecordService().handler(req, ctx),
});
app.http("channels", {
    methods: ["GET"],
    authLevel: "function",
    handler: (req, ctx) => new ChannelConfigService().handler(req, ctx),
});