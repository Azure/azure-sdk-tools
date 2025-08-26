import { app } from "@azure/functions";
import { FeedbackService } from "../services/AnalyticsServices/FeedbackService";
import { RecordService } from "../services/AnalyticsServices/RecordService";
import { ChannelConfigService } from "../services/AnalyticsServices/ChannelConfigService";

const feedbackService = new FeedbackService();
const recordService = new RecordService();
const channelConfigService = new ChannelConfigService();

// Register the Azure Function
app.http("feedbacks", {
    methods: ["GET"],
    authLevel: "function",
    handler: feedbackService.handler.bind(feedbackService),
});
app.http("records", {
    methods: ["GET"],
    authLevel: "function",
    handler: recordService.handler.bind(recordService),
});
app.http("channels", {
    methods: ["GET"],
    authLevel: "function",
    handler: channelConfigService.handler.bind(channelConfigService),
});