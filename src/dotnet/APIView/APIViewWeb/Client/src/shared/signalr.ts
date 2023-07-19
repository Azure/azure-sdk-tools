import * as hp from "./helpers";
import * as rvM from "../pages/review.module";

const signalR = require('@microsoft/signalr');
let connection;

// sender/server side of comment refresh
export function pushComment(reviewId, elementId, partialViewResult) {
  if (connection && connection.state === signalR.HubConnectionState.Connected) {
    connection.invoke("PushComment", reviewId, elementId, partialViewResult);
  }
}

$(() => {
//-------------------------------------------------------------------------------------------------
// Create SignalR Connection and Register various events
//-------------------------------------------------------------------------------------------------

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

  /**
   * receiver/client side of comment refresh
   * SignalRHub does not have function to send to all users except for a particular group
   * solution: send to all users AND the group and raise flag
   */
  let alreadyRefreshedComment = false;
  connection.on("ReceiveCommentSelf", (reviewId, elementId, partialViewResult) => {
    rvM.replaceRowWIthPartialViewResult(reviewId, elementId, partialViewResult);
    alreadyRefreshedComment = true;
  });

  connection.on("ReceiveComment", (reviewId: string, elementId: string, partialViewResult: string) => {
    if (alreadyRefreshedComment == true) {
      alreadyRefreshedComment = false;
      return;
    }

    let partialViewString = rvM.getCurrentUserPartialViewResult(partialViewResult);
    rvM.replaceRowWIthPartialViewResult(reviewId, elementId, partialViewString);
  });

  let approvalPendingText = "Current Revision Approval Pending";
  let approvedByText = "Approved by:";
  let approvesCurrentRevisionText = "Approves the current revision of the API";

  connection.on("ReceiveApprovalSelf", (reviewId, revisionId, approvalToggle) => {
    if (!rvM.checkReviewRevisionIdAgainstCurrent(reviewId, revisionId, true)) {
      return;
    }

    let $approvalSpans: JQuery<HTMLElement> = $("#approveCollapse span.small.text-muted");

    var indexResult = rvM.parseApprovalSpanIndex($approvalSpans, approvedByText, approvalPendingText, approvesCurrentRevisionText);
    let upperTextSpan = indexResult["upperText"];

    if (approvalToggle) {
      rvM.removeUpperTextSpan(upperTextSpan, $approvalSpans);
      rvM.addButtonApproval();
    } else {
      rvM.addUpperTextSpan(approvesCurrentRevisionText);
      rvM.removeButtonApproval();
    }
  });

  /**
   * Known issues are related to approval for first release. They are resolved upon refreshing.
   * - Navigation bar checkmark for first release does not show up
   * - Approve for First Release button can get unselected instead of being replaced by text 
   * - Unselecting Approve For First Release button behaves in the same way as a regular Approve button
   */
  connection.on("ReceiveApproval", (reviewId, revisionId, approver, approvalToggle) => {
    if (!rvM.checkReviewRevisionIdAgainstCurrent(reviewId, revisionId, true)) {
      return;
    }

    let $approvalSpans: JQuery<HTMLElement> = $("#approveCollapse span.small.text-muted");

    var indexResult = rvM.parseApprovalSpanIndex($approvalSpans, approvedByText, approvalPendingText, approvesCurrentRevisionText);
    let approversIndex = indexResult["approvers"];

    if (approversIndex === -1) {
      return;
    }

    let lowerTextSpan: HTMLElement = $approvalSpans[approversIndex];
    let approverHref = `/Assemblies/Profile/${approver}`;

    if (approvalToggle) {
      rvM.addApprover(lowerTextSpan, approvedByText, approverHref, approver);
    } else {
      rvM.removeApprover(lowerTextSpan, approver, approvalPendingText);
    }
  })

  // Start the connection.
  start();
});
