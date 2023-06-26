import * as hp from "./helpers";

let connection;
// sender/server side of comment refresh 
export function ReceiveComment(reviewId, elementId, partialViewResult) {
  connection.invoke("ReceiveComment", reviewId, elementId, partialViewResult);
}

$(() => {
//-------------------------------------------------------------------------------------------------
// Create SignalR Connection and Register various events
//-------------------------------------------------------------------------------------------------
  const signalR = require('@microsoft/signalr');

  connection = new signalR.HubConnectionBuilder()
    .withUrl(`${location.origin}/hubs/notification`, { 
      skipNegotiation: true,
      transport: signalR.HttpTransportType.WebSockets })
    .configureLogging(signalR.LogLevel.Information)
    .withAutomaticReconnect()
    .build();

  async function start() {
    try {
      await connection.start();
    }
    catch (err) {
      console.log(err);
      setTimeout(start, 5000);
    }
  }

  connection.onclose(async () => {
    await start();
  });

  connection.on("RecieveNotification", (notification) => {
    hp.addToastNotification(notification);
  });

  // receiver/client side of comment refresh 
  connection.on("ReceiveComment", (reviewId, elementId, partialViewResult) => {
    let result = hp.getReviewAndRevisionIdFromUrl();
    let currReviewId = result["reviewId"];

    if (currReviewId != reviewId) {
      return;
    }

    var rowSectionClasses = hp.getCodeRowSectionClasses(elementId);
    hp.showCommentBox(elementId, rowSectionClasses, undefined, false);

    let commentsRow = hp.getCommentsRow(elementId);
    hp.updateCommentThread(commentsRow, partialViewResult);
    hp.addCommentThreadNavigation();
  });

  // Start the connection.
  start();
});
