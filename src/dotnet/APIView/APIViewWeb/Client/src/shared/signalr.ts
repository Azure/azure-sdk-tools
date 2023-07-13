import { HubConnection } from "@microsoft/signalr";
import * as hp from "./helpers";

const signalR = require('@microsoft/signalr');

let connection: HubConnection;
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

    if (currReviewId != reviewId) {
      return;
    }

    var rowSectionClasses = hp.getCodeRowSectionClasses(elementId);
    hp.showCommentBox(elementId, rowSectionClasses, undefined, false);

    let commentsRow = hp.getCommentsRow(elementId);
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

    var rowSectionClasses = hp.getCodeRowSectionClasses(elementId);
    hp.showCommentBox(elementId, rowSectionClasses, undefined, false);

    let commentsRow = hp.getCommentsRow(elementId);
    hp.updateCommentThread(commentsRow, partialViewResult);
    hp.addCommentThreadNavigation();
    hp.removeCommentIconIfEmptyCommentBox(elementId);
  });

  connection.on("ReceiveApproval", (reviewId, revisionId, approver, approvalToggle) => {
    let href = location.href;
    let result = hp.getReviewAndRevisionIdFromUrl(href);
    let currReviewId = result["reviewId"];
    let currRevisionId = result["revisionId"];

    if (currReviewId != reviewId) {
      return;
    }

    if (currRevisionId && currRevisionId != revisionId) { // TODO: need to identify if the currRevisionId === null && revisionId !== latest revision
      return;
    }

    // ----------------- add user to approved by -----------------
    // TODO: fix spacings 
    let approvalPendingText = "Current Revision Approval Pending";
    let approvedByText = "Approved by:";
    let approvesCurrentRevisionText = "Approves the current revision of the API";
    let firstReleaseText = "Approved for First Release";

    let $approvalSpans: JQuery<HTMLElement> = $("#approveCollapse span.small.text-muted");

    var indexResult = parseApprovalSpanIndex($approvalSpans, approvedByText, approvalPendingText, approvesCurrentRevisionText);

    let approversIndex = indexResult["approvers"];
    let upperTextSpan = indexResult["upperText"];        

    if (approversIndex === -1) {
      return;
    }

    let lowerTextSpan: HTMLElement = $approvalSpans[approversIndex];
    let approverHref = `/Assemblies/Profile/${approver}`;

    // TODO: figure out spacing
    // TODO: add checks to prevent clicking multiple times before it reloads
    // TODO: check if the current user is the sender (connectionId)
    // TODO: revision dropdown refresh
    // TODO: first approval icon refresh


    if (approvalToggle) {
      // add this user to approval list

      // other approvers are in the list
      if (lowerTextSpan.textContent?.includes(approvedByText)) {
        lowerTextSpan.append(" , ");  // works
      } else {
        lowerTextSpan.textContent = approvedByText; // works 
        addApprovedBorder();  // works 

        // attempt: 
        //let $reviewInfoBar = $("div#review-info-bar.input-group.input-group-sm");
        //let $reviewInfoBarSpans = $("div#review-info-bar.input-group.input-group-sm span.input-group-text");
        //if ($reviewInfoBarSpans.length === 0) {
        //  let $firstReleaseSpan = $("<span>").addClass("input-group-text");
        //  $firstReleaseSpan.attr("data-bs-toggle", "tooltip");
        //  $firstReleaseSpan.attr("title");
        //  $firstReleaseSpan.attr("data-bs-original-title", firstReleaseText);

        //  let $firstReleaseIcon = $("<i>").addClass("fa-regular fa-circle-check text-success");

        //  $firstReleaseSpan.append($firstReleaseIcon);
        //  $reviewInfoBar.after($reviewInfoBarSpans[1]);
        //}
      }

      $(lowerTextSpan).append('<a href="' + approverHref + '">' + approver + '</a>'); // works

      // TODO: check if the client is the current user
      removeUpperTextSpan(upperTextSpan, $approvalSpans); // works
      addButtonApproval();
    } else {
      // ==================== remove this user from approver list ====================
      let children = lowerTextSpan.children;
      let numApprovers = children.length;

      // multiple reviewers
      if (numApprovers > 1) {
        removeApproverFromApproversList(children, approver);
      } else {
        lowerTextSpan.textContent = approvalPendingText;
        removeApprovalBorder();
      }

      // TODO: if the client is for the current user, then also change approve button and top text
      addUpperTextSpan(approvesCurrentRevisionText);
      removeButtonApproval();
    }


    // js
    // add id to approval section to grab it
    // upddate username
    // select green border objects -> add css
    // check for approved for release
    // revision dropdown (refresh the whole dropdown)

    // notifiction "page was approved by this user"
    // create a function that updates everyhting at the same time 
  })

  // Start the connection.
  start();
});

function addUpperTextSpan(approvesCurrentRevisionText: string) {
    let $upperTextSpan = $("<span>").text(approvesCurrentRevisionText).addClass("small text-muted");
    let $upperTextForm = $("ul#approveCollapse form.form-inline");
    $upperTextForm.prepend($upperTextSpan);
}

function addButtonApproval() {
    let $approveBtn = $("form.form-inline button.btn.btn-success");
    $approveBtn.removeClass("btn-success");
    $approveBtn.addClass("btn-outline-secondary");
    $approveBtn.text("Revert API Approval");
}

function removeButtonApproval() {
    let $approveBtn = $("form.form-inline button.btn.btn-outline-secondary");
    $approveBtn.removeClass("btn-outline-secondary");
    $approveBtn.addClass("btn-success");
    $approveBtn.text("Approve");
}

function removeApprovalBorder() {
    let $reviewLeft = $("#review-left");
    $reviewLeft.removeClass("review-approved");
    $reviewLeft.addClass("border");
    $reviewLeft.addClass("rounded-1");

    let $reviewRight = $("#review-right");
    $reviewRight.removeClass("review-approved");
    $reviewRight.addClass("border");
    $reviewRight.addClass("rounded-1");
}

function parseApprovalSpanIndex($approvalSpans: JQuery<HTMLElement>, approvedByText: string, approvalPendingText: string, approvesCurrentRevisionText: string) {
  let indexResult = {
      "approvers": -1,
      "upperText": -1,
  };

  for (var i = 0; i < $approvalSpans.length; i++) {
    let content = $approvalSpans[i].textContent;

    if (!content) {
      return indexResult;
    }

    if (content.includes(approvedByText) || content.includes(approvalPendingText)) {
          indexResult["approvers"] = i;
      }
    if (content.includes(approvesCurrentRevisionText)) {
      indexResult["upperText"] = i;
      }
  }

  return indexResult;
}

function removeUpperTextSpan(upperTextIndex: number, $approvalSpans: JQuery<HTMLElement>) {
    if (upperTextIndex !== -1) {
        let upperTextSpan: HTMLElement = $approvalSpans[upperTextIndex];
      upperTextSpan.remove();
    }
}

function removeApproverFromApproversList(children, approver) {
  for (var i = 0; i < children.length; i++) {
    if (children[i].innerHTML === approver) {
      //"Approved by:" "," <approver> "," <approver> "\n" => first item: remove after
      //"Approved by:" <approver> "," "," <approver> "\n" => middle item: remove either
      //"Approved by:" <approver> "," <approver> "," "\n" => last item: remove before
      // remove before unless the previous text is "Approved by:" because it's more likely to accidentally approve (latest approver)
      if (i === 0) {
        children[i].nextSibling?.remove();
      } else {
        children[i].previousSibling?.remove();
      }
      children[i].remove();
      break;
    }
  }
}

function addApprovedBorder() {
  let reviewLeft = $("#review-left");
  reviewLeft.addClass("review-approved");
  reviewLeft.removeClass("border");
  reviewLeft.removeClass("rounded-1");

  let reviewRight = $("#review-right");
  reviewRight.addClass("review-approved");
  reviewRight.removeClass("border");
  reviewRight.removeClass("rounded-1");
}
