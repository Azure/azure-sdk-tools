/**
* Call APIView controller endpoint (/userprofile/updatereviewpagesettings)
* to update various page settings
* Takes a call back function that is run after ajax call succeeds
* @param { function } a callback function
*/
export function updatePageSettings(callBack) {
  var hideLineNumbers = $("#hide-line-numbers").prop("checked");
  if (hideLineNumbers != undefined) { hideLineNumbers = !hideLineNumbers; }

  var hideLeftNavigation = $("#hide-left-navigation").prop("checked");
  if (hideLeftNavigation != undefined) { hideLeftNavigation = !hideLeftNavigation; }

  var showHiddenApis = $("#show-hidden-api-checkbox").prop("checked");
  var showComments = $("#show-comments-checkbox").prop("checked");
  var showSystemComments = $("#show-system-comments-checkbox").prop("checked");

  var hideReviewPageOptions = $("#review-right-offcanvas-toggle").prop("checked");
  if (hideReviewPageOptions != undefined) { hideReviewPageOptions = !hideReviewPageOptions; }

  var hideIndexPageOptions = $("#index-right-offcanvas-toggle").prop("checked");
  if (hideIndexPageOptions != undefined) { hideIndexPageOptions = !hideIndexPageOptions; }

  var uri = location.origin + `/userprofile/updatereviewpagesettings?` +
                              `hideLineNumbers=${hideLineNumbers}&` +
                              `hideLeftNavigation=${hideLeftNavigation}&` +
                              `showHiddenApis=${showHiddenApis}&` +
                              `hideReviewPageOptions=${hideReviewPageOptions}&` +
                              `hideIndexPageOptions=${hideIndexPageOptions}&` +
                              `showComments=${showComments}&` +
                              `showSystemComments=${showSystemComments}`;

  $.ajax({
    type: "PUT",
    url: uri
  }).done(callBack());
}

/**
* Retrieves a codeLineRow using the id
* @param { string } row id
*/
export function getCodeRow(id: string) {
  return $(`.code-line[data-line-id='${id}']`);
}

/**
* Retrieves the classList for a codeLineRow using the id
* @param { string } row id
*/
export function getCodeRowSectionClasses(id: string) {
  var codeRow = getCodeRow(id);
  var rowSectionClasses = "";
  if (codeRow) {
    rowSectionClasses = getRowSectionClasses(codeRow[0].classList);
  }
  return rowSectionClasses;
}

/**
* Retrieves the classes that identifies the codeLine as a section
* @param { DOMTokenList } classlist
*/
export function getRowSectionClasses(classList: DOMTokenList) {
  const rowSectionClasses: string[] = [];
  for (const value of classList.values()) {
    if (value == "section-loaded" || value.startsWith("code-line-section-content") || value.match(/lvl_[0-9]+_(parent|child)_[0-9]+/)) {
      rowSectionClasses.push(value);
    }
  }
  return rowSectionClasses.join(' ');
}

/**
* Updates the state of the comment icon (visible / invisible)
* @param { string } id
* @param { boolean } show
*/
export function toggleCommentIcon(id: string, show: boolean) {
  getCodeRow(id).find(".icon-comments").toggleClass("invisible", !show);
}

/**
* Retrieve a Specific Cookie from Users Browser
* @param { String } cookies (pass document.cookies)
* @param { String } cookieName
* @return { String } cookieValue
*/
export function getCookieValue (cookies: string, cookieName: string)
{
  const nameEQ = `${cookieName}=`;
  const charArr = cookies.split(';');
  for (let i = 0; i < charArr.length; i++)
  {
    let ch = charArr[i];
    while(ch.charAt(0) === ' ')
    {
      ch = ch.substring(1, ch.length);
    }
    if (ch.indexOf(nameEQ) === 0)
      return ch.substring(nameEQ.length, ch.length);    
  }
  return null;
}

/**
* Retrieve the list of classes on an element
* @param { JQuery<HTMLElement> | HTMLElement } element
* @return { string [] } classList - list of classes of the element
*/
export function getElementClassList (element : JQuery<HTMLElement> | HTMLElement) {
  let el : HTMLElement = (element instanceof HTMLElement) ? element : element[0];
  return Array.from(el.classList);
}

// ToastNotification
export enum NotificationLevel { info, warning, error }
export interface Notification {
  message : string;
  level : NotificationLevel
}

/**
* Contruct and add a toast notification to the page
* @param { ToastNotification } notification
* @param { number } duration - how long should the notification stay on the page
*/
export function addToastNotification(notification : Notification, id : string = "", duration : number = 10000) {
  const newtoast = $('#notification-toast').clone().removeAttr("id").attr("data-bs-delay", duration);
  if (id != "")
  {
    newtoast.attr("id", id);
  }
  
  switch (notification.level) {
    case 0:
      newtoast.find(".toast-header").prepend(`<i class="fa-solid fa-circle-info text-info me-1" ></i>`);
      newtoast.find(".toast-header strong").html("Information");
      break;
    case 1:
      newtoast.find(".toast-header").prepend(`<i class="fa-solid fa-triangle-exclamation text-warning me-1"></i>`);
      newtoast.find(".toast-header strong").html("Warning");
      break;
    case 2:
      newtoast.find(".toast-header").prepend(`<i class="fa-solid fa-circle-exclamation text-danger me-1"></i>`);
      newtoast.find(".toast-header strong").html("Error");
      break;
  }
  newtoast.find(".toast-body").html(notification.message);
  const toastBootstrap = bootstrap.Toast.getOrCreateInstance(newtoast[0]);
  $("#notification-container").append(newtoast);
  toastBootstrap.show();
}

// Auto Refresh Comment
export function updateCommentThread(commentBox, commentThreadHTML) {
  commentThreadHTML = $.parseHTML(commentThreadHTML);
  $(commentBox).replaceWith(commentThreadHTML);
  return false;
}

/**
 * remove comment icon if the comment box is empty (has no comments)
 * @param id lineid of the comment box 
 */
export function removeCommentIconIfEmptyCommentBox(id) {
  var commentRows = getCommentsRow(id);
  if (commentRows.length == 0 && !($("#show-comments-checkbox").prop("checked"))) {
    toggleCommentIcon(id, false);
  }
}

export function addCommentThreadNavigation() {
  var commentRows = $('.comment-row');
  var displayedCommentRows = getDisplayedCommentRows(commentRows, true, false);

  commentRows.each(function (index) {
    $(this).find('.comment-thread-anchor').removeAttr("id");
    $(this).find('.comment-navigation-buttons').empty();

    if ($(this).hasClass("d-none")) {
      return;
    }

    let commentHolder = $(this).find(".comment-holder").first();
    if (commentHolder.hasClass("comments-resolved") && commentHolder.css("display") != "block") {
      return;
    }
    displayedCommentRows.push($(this));
  });

  if (displayedCommentRows.length > 1) {
    displayedCommentRows.forEach(function (value, index) {
      var commentThreadAnchorId = "comment-thread-" + index;
      $(value).find('.comment-thread-anchor').first().prop('id', commentThreadAnchorId);

      var commentNavigationButtons = $(value).find('.comment-navigation-buttons').last();
      commentNavigationButtons.empty();

      var nextCommentThreadAnchor = "comment-thread-" + (index + 1);
      var previousCommentThreadAnchor = "comment-thread-" + (index - 1);

      if (index == 0) {
        commentNavigationButtons.append(`<a class="btn btn-outline-secondary" href="#${nextCommentThreadAnchor}" title="Next Comment"><i class="fa fa-chevron-down" aria-hidden="true"></i></a>`)
      }
      else if (index == displayedCommentRows.length - 1) {
        commentNavigationButtons.append(`<a class="btn btn-outline-secondary" href="#${previousCommentThreadAnchor}" title="Previous Comment"><i class="fa fa-chevron-up" aria-hidden="true"></i></a>`)
      }
      else {
        commentNavigationButtons.append(`<a class="btn btn-outline-secondary" href="#${previousCommentThreadAnchor}" title="Previous Comment"><i class="fa fa-chevron-up" aria-hidden="true"></i></a>`)
        commentNavigationButtons.append(`<a class="btn btn-outline-secondary ml-1" href="#${nextCommentThreadAnchor}" title="Next Comment"><i class="fa fa-chevron-down" aria-hidden="true"></i></a>`)
      }
    });
  }
}

export function getDisplayedCommentRows(commentRows: JQuery<HTMLElement>, clearCommentAnchors = false, returnFirst = false) {
  var displayedCommentRows: JQuery<HTMLElement>[] = [];
  commentRows.each(function (index) {
    if (clearCommentAnchors) {
      $(this).find('.comment-thread-anchor').removeAttr("id");
      $(this).find('.comment-navigation-buttons').empty();
    }

    if ($(this).hasClass("d-none")) {
      return;
    }

    let commentHolder = $(this).find(".comment-holder").first();
    if (commentHolder.hasClass("comments-resolved") && commentHolder.css("display") != "block") {
      return;
    }
    displayedCommentRows.push($(this));
    if (returnFirst) {
      return false;
    }
  });
  return displayedCommentRows
}

/**
 * gets the review and revision id of review from given uri, if they exist
 * @param uri uri of api view page 
 * @returns result dictionary of "reviewId" and "revisionId", if they exist; undefined otherwise
 */
export function getReviewAndRevisionIdFromUrl(uri) {
  const regex = /.+(Review|Conversation|Revisions|Samples)\/([a-zA-Z0-9]+)(\?revisionId=([a-zA-Z0-9]+))?/;

  const match = uri.match(regex);
  const result = {}

  if (match) {
    result["reviewId"] = match[2];
    result["revisionId"] = match[4];  // undefined if latest revision 
  } 

  return result;
}

export function getCommentsRow(id: string) {
  return $(`.comment-row[data-line-id='${id}']`);
}

// side effect: creates a comment row if it doesn't already exist
export function showCommentBox(id: string, classes: string = '', groupNo: string = '', moveFocus: boolean = true) {
  let commentForm;
  let commentsRow = getCommentsRow(id);
  let commentRowClasses = "comment-row";
  if (classes) {
    commentRowClasses = `${commentRowClasses} ${classes}`;
  }

  if (commentsRow.length === 0) {
    commentForm = createCommentForm(groupNo);
    commentsRow =
      $(`<tr class="${commentRowClasses}" data-line-id="${id}">`)
        .append($("<td colspan=\"3\">")
          .append(commentForm));

    commentsRow.insertAfter(getDiagnosticsRow(id).get(0) || getCodeRow(id).get(0));
  }
  else {
    // there is one or more comment rows - insert form
    let replyArea = $(commentsRow).find(".review-thread-reply");
    let targetReplyArea = replyArea.first();
    let firstReplyId = targetReplyArea.attr("data-reply-id");
    let insertAtBegining = false;

    if (groupNo) {
      replyArea.siblings(".comment-form").remove();
      if (Number(groupNo) < Number(firstReplyId)) {
        insertAtBegining = true;
      }
      else {
        replyArea.each(function (index, value) {
          let replyId = $(value).attr("data-reply-id");

          if (replyId == groupNo) {
            targetReplyArea = $(value);
            return false;
          }

          if (Number(replyId) > Number(groupNo)) {
            return false;
          }

          targetReplyArea = $(value);
        });
      }
    }
    else {
      let rowGroupNo = getReplyGroupNo($(targetReplyArea));
      if (rowGroupNo.length > 0) {
        insertAtBegining = true;
      }
    }

    commentForm = $(targetReplyArea).next();
    if (!commentForm.hasClass("comment-form")) {
      if (insertAtBegining) {
        let commentThreadContent = $(targetReplyArea).closest(".comment-thread-contents");
        $(createCommentForm(groupNo)).prependTo(commentThreadContent);
      }
      else {
        commentForm = $(createCommentForm(groupNo)).insertAfter(targetReplyArea);
      }
    }
    replyArea.hide();
    commentForm.show();
    commentsRow.show(); // ensure that entire comment row isn't being hidden
  }

  $(getDiagnosticsRow(id)).show(); // ensure that any code diagnostic for this row is shown in case it was previously hidden

  // If comment checkbox is unchecked, show the icon for new comment
  if (!($("#show-comments-checkbox").prop("checked"))) {
    toggleCommentIcon(id, true);
  }

  if (moveFocus) {
    commentForm.find(".new-thread-comment-text").focus();
  }
}

export function createCommentForm(groupNo: string = '') {
  var commentForm = $("#js-comment-form-template").children().clone();
  if (groupNo) {
    commentForm.find("form .new-comment-content").prepend(`<span class="badge badge-pill badge-light mb-2"><small>ROW-${groupNo}</small></span>`);
  }
  return commentForm;
}

export function getDiagnosticsRow(id: string) {
  return $(`.code-diagnostics[data-line-id='${id}']`);
}

export function getReplyGroupNo(sibling: JQuery<HTMLElement>) {
  return $(sibling).prevAll("a").first().find("small");
}

export function getElementId(element: HTMLElement, idName: string = "data-line-id") {
  return getParentData(element, idName);
}

export function getParentData(element: HTMLElement, name: string) {
  return $(element).closest(`[${name}]`).attr(name);
}

/**
 * @returns true if the current reviewId is equivalent to the @reviewId and @revisionId
 *          whether we check @revisionId depends on the value of @checkRevision
 *          false otherwise
 * @param checkRevision true indicates that both @reviewId and @revisionId must match,
 *                      false indicates that only @reviewId can match
 */
export function checkReviewRevisionIdAgainstCurrent(reviewId, revisionId, checkRevision) {
  let href = location.href;
  let result = getReviewAndRevisionIdFromUrl(href);
  let currReviewId = result["reviewId"];
  let currRevisionId = result["revisionId"];

  if (currReviewId != reviewId) {
    return false;
  }

  if (checkRevision && currRevisionId && currRevisionId != revisionId) {
    return false;
  }

  return true;
}

/**
 * Auto refresh sends the entire partial view result of the comment thread, including the sender's profile picture.
 * Overrides the sender's profile picture with the current user's picture
 */
export function updateUserIcon() {
  let size = 28;
  let $navLinks = $("nav.navbar a.nav-link");

  for (let nav of $navLinks) {
    if (nav.innerText.includes("Profile")) {
      let href = nav.getAttribute("href");
      if (href) {
        let hrefString = href;
        let hrefSplit = hrefString.split("/");
        let username = hrefSplit[hrefSplit.length - 1];
        let url: string = "https://github.com/" + username + ".png?size=" + size;
        $("div.review-thread-reply div.reply-cell img.comment-icon").attr("src", url);
        return;
      }
    }
  }
}

/**
* Contruct and add a toast notification and update Generate Review Button
* @param { ToastNotification } notification
*/
export function updateAIReviewGenerationStatus(notification: any) {
  $("#generateAIReviewButton").text(notification.message);
  if (notification.status === 0) {
    $("#generateAIReviewButton").append(`<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>`);
    $("#generateAIReviewButton").prop("disabled", true);
  }
  else {
    $("#generateAIReviewButton").prop("disabled", false);
  }
  addToastNotification(notification);
}
