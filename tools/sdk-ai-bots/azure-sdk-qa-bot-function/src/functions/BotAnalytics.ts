import { app } from "@azure/functions";
import { FeedbackService } from "../services/AnalyticsServices/FeedbackService";
import { RecordService } from "../services/AnalyticsServices/RecordService";

const feedbackService = new FeedbackService();
const recordService = new RecordService();

// Register the Azure Function
app.http("feedbacks", {
    methods: ["GET"],
    authLevel: "function",
    handler: feedbackService.getData.bind(feedbackService),
});
app.http("records", {
    methods: ["GET"],
    authLevel: "function",
    handler: recordService.getData.bind(recordService),
});
