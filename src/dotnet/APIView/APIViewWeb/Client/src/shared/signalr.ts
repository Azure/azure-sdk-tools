import * as hp from "./helpers";

const signalR = require('@microsoft/signalr');

let connection;
// sender/server side of comment refresh 
export function PushComment(reviewId, elementId, partialViewResult) {
  if (connection && connection.state === signalR.HubConnectionState.Connected) {
    connection.invoke("PushComment", reviewId, elementId, partialViewResult);
  }
}

$(() => {
//-------------------------------------------------------------------------------------------------
// Create SignalR Connection and Register various events
//-------------------------------------------------------------------------------------------------

  connection = new signalR.HubConnectionBuilder()
  const connection = new signalR.HubConnectionBuilder()
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
    let href = location.href;
    let result = hp.getReviewAndRevisionIdFromUrl(href);
    let currReviewId = result["reviewId"];

  // receiver/client side of comment refresh 
  connection.on("ReceiveComment", (reviewId, revisionId, elementId, partialViewResult) => {
    let result = hp.getReviewAndRevisionIdFromUrl();
    let currReviewId = result["reviewId"];
    let currRevisionId = result["revisionId"];

    if (currReviewId !== reviewId) {
      return; 
    }

    if (revisionId === undefined) {
      // TODO: latest version
    }
    var rowSectionClasses = hp.getCodeRowSectionClasses(elementId);
    hp.showCommentBox(elementId, rowSectionClasses); // side effect of creating a comment box/row
    let commentsRow = hp.getCommentsRow(elementId); // TODO: new comment thread doesn't have a comments row

    //let inlineId = hp.getElementId(elementId, "data-inline-id");
    //  if (inlineId) {
    //    let groupNo = inlineId.replace(`${elementId}-tr-`, '');
    //  }
    //  else {
    //    hp.showCommentBox(elementId, rowSectionClasses);
    //    // create comment form
    //    // update/add
    //    // get displayed comment rows 
    //  }
    //}

    hp.updateCommentThread(commentsRow, partialViewResult);
    // commented because we don't want to navigate user to updated comment
    hp.addCommentThreadNavigation();  
  });
    if (currReviewId != reviewId) {
      return;
    }

    var rowSectionClasses = hp.getCodeRowSectionClasses(elementId);
    hp.showCommentBox(elementId, rowSectionClasses, undefined, false);

    let commentsRow = hp.getCommentsRow(elementId);
    hp.updateCommentThread(commentsRow, partialViewResult);
    hp.addCommentThreadNavigation();
    hp.removeCommentIconIfEmptyCommentBox(elementId);
  });

  // Start the connection.
  start();
});
