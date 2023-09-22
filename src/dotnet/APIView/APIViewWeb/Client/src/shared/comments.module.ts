
import * as hp from "./helpers";

/**
* Updates comment thread HTML to match the current user's context
* Remove remove/edit buttons of sender's comments
* Add remove/edit buttons to current user's comments (if any)
* @param commentThreadHTML
* @returns partial view result for the current user 
*/
export function updateCommentThreadUserContext(commentThreadHTML: string) {
  let commentThread = $(commentThreadHTML);

  // remove all delete and edit anchors
  commentThread.find("a.dropdown-item.js-delete-comment").next().next().remove();
  commentThread.find("a.dropdown-item.js-delete-comment").next().remove();
  commentThread.find("a.dropdown-item.js-delete-comment").remove();

  //verify name and add delete and edit anchors
  let $commentContents = commentThread.find("div.comment-contents > span");
  $commentContents.each((index, value) => {
    let commenter;
    if (value.children) {
      commenter = value.children[0];
    }
    if (!commenter) {
      return;
    }

    let commenterHref = commenter.attributes.getNamedItem('href')?.value;
    let profileHref;
    $('ul.navbar-nav.ms-auto > li.nav-item > a.nav-link').each((index, value) => {
      if (value.textContent && value.textContent.trim() === 'Profile') {
        profileHref = value.attributes.getNamedItem('href')?.value;
      }
    });


    if (profileHref === commenterHref) {
      let dropdown = commentThread.find('div.dropdown-menu.dropdown-menu-right')[index];
      $('<li><hr class="dropdown-divider"></li>').prependTo(dropdown);
      $('<a href="#" class="dropdown-item js-edit-comment">Edit</a>').prependTo(dropdown);
      $('<a href="#" class="dropdown-item js-delete-comment text-danger">Delete</a>').prependTo(dropdown);
    }
  });

  let partialViewString = commentThreadHTML.split("<td")[0] + commentThread.html() + "</tr>";
  return partialViewString;
}


/**
 * Replaces the row or comment thread with partial view result (an updated comment thread)
 * @param reviewId
 * @param elementId
 * @param commentThreadHTML
 */
export function updateCommentThreadInReviewPageDOM(reviewId: any, elementId: any, commentThreadHTML: any) {
  if (hp.checkReviewRevisionIdAgainstCurrent(reviewId, null, false)) {
    var rowSectionClasses = hp.getCodeRowSectionClasses(elementId);
    hp.showCommentBox(elementId, rowSectionClasses, undefined, false);

    let commentsRow = hp.getCommentsRow(elementId);
    const replyText = commentsRow.find(".new-thread-comment-text-mirror").text();
    hp.updateCommentThread(commentsRow, commentThreadHTML);
    hp.updateUserIcon();
    if (replyText) {
      commentsRow = hp.getCommentsRow(elementId);
      commentsRow.find(".review-thread-reply-button").click();
      commentsRow.find(".new-thread-comment-text-mirror").text(replyText)
      commentsRow.find(".new-thread-comment-text").html(replyText);
    }
    hp.addCommentThreadNavigation();
    hp.removeCommentIconIfEmptyCommentBox(elementId);
  }
}

