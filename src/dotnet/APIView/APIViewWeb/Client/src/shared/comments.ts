import * as hp from "../shared/helpers";
import { PushComment } from "./signalr";

$(() => {
  const INVISIBLE = "invisible";
  const SEL_CODE_DIAG = ".code-diagnostics";
  const SEL_COMMENT_ICON = ".icon-comments";
  const SEL_COMMENT_CELL = ".comment-cell";
  const SHOW_COMMENTS_CHECK = "#show-comments-checkbox";
  const SHOW_SYS_COMMENTS_CHECK = "#show-system-comments-checkbox";

  let CurrentUserSuggestionElements: HTMLElement[] = [];	
  let CurrentUserSuggestionIndex = -1;
  let CurrentCommentToggle = false;

  // simple github username match	
  const githubLoginTagMatch = /(\s|^)@([a-zA-Z\d-]+)/g;

  $(document).on("click", ".commentable", e => {
    var rowSectionClasses = hp.getCodeRowSectionClasses(e.target.id);
    hp.showCommentBox(e.target.id, rowSectionClasses);
    e.preventDefault();
  });

  $(document).on("click", ".line-comment-button", e => {
    let id = hp.getElementId(e.target);
    let inlineId = hp.getElementId(e.target, "data-inline-id");
    if (id) {
      var rowSectionClasses = hp.getCodeRowSectionClasses(id);
      if (inlineId) {
        let groupNo = inlineId.replace(`${id}-tr-`, '');
        hp.showCommentBox(id, rowSectionClasses, groupNo);
      }
      else {
        hp.showCommentBox(id, rowSectionClasses);
      }
  }
  e.preventDefault();
  });

  $(document).on("click", ".comment-cancel-button", e => {
    let id = hp.getElementId(e.target);
    if (id) {
      hideCommentBox(id);
      // if a comment was added and then cancelled, and there are no other
      // comments for the thread, we should remove the comments icon.
      if (hp.getCommentsRow(id).find(SEL_COMMENT_CELL).length === 0) {
        hp.getCodeRow(id).find(SEL_COMMENT_ICON).addClass(INVISIBLE);
      }
    }
    e.preventDefault();
  });

  $(document).on("click", SHOW_COMMENTS_CHECK, e => {
    hp.updatePageSettings(function () {
      const checked = $(SHOW_COMMENTS_CHECK).prop("checked");
      toggleAllCommentsVisibility(checked);
    });
  });

  $(document).on("click", SHOW_SYS_COMMENTS_CHECK, e => {
    hp.updatePageSettings(function () {
      const checked = $(SHOW_SYS_COMMENTS_CHECK).prop("checked");
      toggleAllDiagnosticsVisibility(checked);
    });
  });

  $(document).on("mouseenter", SEL_COMMENT_ICON, e => {
    let lineId = hp.getElementId(e.target);
    if (!lineId) {
      return;
    }

    if (getSingleCommentAndDiagnosticsDisplayStatus(lineId)) {
      CurrentCommentToggle = true;
    } else {
      toggleSingleCommentAndDiagnostics(lineId);
      toggleSingleResolvedComment(lineId);
    }
    e.preventDefault();
  });

  $(document).on("click", SEL_COMMENT_ICON, e => {
    let lineId = hp.getElementId(e.target);
    if (lineId) {
      CurrentCommentToggle = !CurrentCommentToggle;
    }
    e.preventDefault();
  });

  $(document).on("mouseleave", SEL_COMMENT_ICON, e => {
    let lineId = hp.getElementId(e.target);
    if (!lineId) {
      return;
    }

    if (CurrentCommentToggle) {
      CurrentCommentToggle = false;
    } else {
      toggleSingleCommentAndDiagnostics(lineId);
      toggleSingleResolvedComment(lineId);
    }
    e.preventDefault();
  });

  $(document).on("submit", "form[data-post-update='comments']", e => {
    $(e.target).find('button').prop("disabled", true);
    const form = <HTMLFormElement><any>$(e.target);
    let lineId = hp.getElementId(e.target);
    let inlineRowNo = $(e.target).find(".new-comment-content small");

    if (inlineRowNo.length == 0) {
      inlineRowNo = hp.getReplyGroupNo($(e.target));
    }

    if (lineId) {
      let commentRow = hp.getCommentsRow(lineId);
      let reviewId = getReviewId(e.target);
      let revisionId = getRevisionId(e.target);
      let rowSectionClasses = hp.getRowSectionClasses(commentRow[0].classList);
      let serializedForm = form.serializeArray();
      serializedForm.push({ name: "elementId", value: lineId });
      serializedForm.push({ name: "reviewId", value: reviewId });
      serializedForm.push({ name: "revisionId", value: revisionId });
      serializedForm.push({ name: "sectionClass", value: rowSectionClasses });
      serializedForm.push({ name: "taggedUsers", value: getTaggedUsers(e.target) });
      
      if (inlineRowNo.length > 0) {
        let groupNo = inlineRowNo.text().replace("ROW-", '');
        serializedForm.push({ name: "groupNo", value: groupNo });
      }
      
      $.ajax({
        type: "POST",
        url: $(form).prop("action"),
        data: $.param(serializedForm)
      }).done(partialViewResult => {
        hp.updateCommentThread(commentRow, partialViewResult);
        hp.addCommentThreadNavigation();
        hp.removeCommentIconIfEmptyCommentBox(lineId);
        PushComment(reviewId, lineId, partialViewResult);
      });
    }
    e.preventDefault();
  });

  $(document).on("click", ".review-thread-reply-button", e => {
    let lineId = hp.getElementId(e.target);
    let inlineRowNo = hp.getReplyGroupNo($(e.target).parent().parent());
    if (lineId) {
      if (inlineRowNo.length > 0) {
        let groupNo = inlineRowNo.text().replace("ROW-", '');
        hp.showCommentBox(lineId,'', groupNo);
      }
      else {
        hp.showCommentBox(lineId);
      }
    }
    e.preventDefault();
  });

  $(document).on("click", ".toggle-comments", e => {
    let lineId = hp.getElementId(e.target);
    if (lineId) {
      toggleComments(lineId);
    }
    e.preventDefault();
  });

  $(document).on("click", ".js-edit-comment", e => {
    let commentId = getCommentId(e.target);
    if (commentId) {
      editComment(commentId);
    }
    e.preventDefault();
  });

  $(document).on("click", ".js-delete-comment", e => {
    let commentId = getCommentId(e.target);
    let lineId = hp.getElementId(e.target);
    if (lineId) {
      let commentRow = hp.getCommentsRow(lineId);
      if (commentId) {
        deleteComment(commentId, lineId, commentRow);
      }
      e.preventDefault();
    }
  });

  $(document).on("click", ".js-github", e => {
    let target = $(e.target);
    let repo = target.attr("data-repo");
    let language = getLanguage(e.target);

    // Fall back to the repo-language if we don't know the review language,
    // e.g.Go doesn't specify Language yet.
    if (!language) {
      language = target.attr("data-repo-language");
    }
    let commentElement = getCommentElement(getCommentId(e.target)!);
    let comment = commentElement.find(".js-comment-raw").html();
    let codeLine = commentElement.closest(".comment-row").prev(".code-line").find(".code");
    let apiViewUrl = "";

    // if creating issue from the convos tab, the link to the code element is in the DOM already.
    let commentUrlElem = codeLine.find(".comment-url");
    if (commentUrlElem.length) {
      apiViewUrl = location.protocol + '//' + location.host + escape(commentUrlElem.attr("href")!);
    }
    else {
      // otherwise we construct the link from the current URL and the element ID
      // Double escape the element - this is used as the URL back to API View and GitHub will render one layer of the encoding.
      apiViewUrl = window.location.href.split("#")[0] + "%23" + escape(escape(hp.getElementId(commentElement[0])!));
    }

    let issueBody = escape("```" + language + "\n" + codeLine.text().trim() + "\n```\n#\n" + comment);
    // TODO uncomment below once the feature to support public ApiView Reviews is enabled.
    //+ "\n#\n")
    //+ "[Created from ApiView comment](" + apiViewUrl + ")";

    window.open(
      "https://github.com/Azure/" + repo + "/issues/new?" +
      "&body=" + issueBody,
      '_blank');
    e.preventDefault();
  });

  $(document).on("keydown", ".new-thread-comment-text", e => {
    // form submit on ctrl + enter
    if (e.ctrlKey && (e.keyCode === 10 || e.keyCode === 13)) {
      const form = $(e.target).closest("form");
      if (form) {
          form.submit();
      }
      e.preventDefault();
      return;	
    }

    // current value of form not including key currently pressed	
    let currentVal: string = e.target.value;	
    // system keys should allow the list to be updated, but should not be added as a key	
    // currentVal doesn't include the key to be added, though, so we need to add it manually	
    if(!(e.key === "Enter" || e.key === "Backspace" || e.key === "Tab" || e.key === "Alt" || e.key === "Control" || e.key === "Shift" || e.key === "Tab" || e.key === "ArrowDown" || e.key === "ArrowUp")) {	
      currentVal += e.key;	
      CurrentUserSuggestionElements = [];	
      CurrentUserSuggestionIndex = -1;	
    } else if(e.key === "Backspace") {	
      currentVal = currentVal.substring(0, currentVal.length - 1); // remove last char	
    }	
    // current caret position in textarea	
    const caretPosition = (<HTMLInputElement>e.target).selectionStart;	
    // returned value from regex exec	
    // index 0 is the entire match, index 1 and 2 are capture groups	
    let matchArray;	
    // gh username without @	
    let matchName;	
    // list of suggested users container	
    const suggestionBox = $(e.target).parent().find(".tag-user-suggestion");	
    // mirror box helps calculate x and y to place suggestion box at	
    const mirrorBox = $(e.target).parent().find(".new-thread-comment-text-mirror");	
    if(!suggestionBox.hasClass("d-none")) {	
      // if suggestion box is visible and user pressed tab or enter	
      if(e.key === "Tab" || e.key === "Enter") {	
        // trigger click event (adds to box)	
        CurrentUserSuggestionElements[CurrentUserSuggestionIndex].click();	
        e.preventDefault();	
        e.target.focus();	
        return;	
      } else if(e.key === "ArrowDown") {	
        if(CurrentUserSuggestionIndex + 1 < CurrentUserSuggestionElements.length) {	
          $(CurrentUserSuggestionElements[CurrentUserSuggestionIndex]).removeClass("tag-user-suggestion-username-targetted");	
          CurrentUserSuggestionIndex++;	
          $(CurrentUserSuggestionElements[CurrentUserSuggestionIndex]).addClass("tag-user-suggestion-username-targetted");	
        }	
        e.preventDefault();	
        return;	
      } else if(e.key === "ArrowUp") {	
        if(CurrentUserSuggestionIndex > 0) {	
          $(CurrentUserSuggestionElements[CurrentUserSuggestionIndex]).removeClass("tag-user-suggestion-username-targetted");	
          CurrentUserSuggestionIndex--;	
          $(CurrentUserSuggestionElements[CurrentUserSuggestionIndex]).addClass("tag-user-suggestion-username-targetted");	
        }	
        e.preventDefault();	
        return;	
      }	
    }	
    if(caretPosition !== null) {	
      let changed = false;	
      // get y position to place suggestion box	
      mirrorBox.css('width', (<HTMLTextAreaElement>e.target).offsetWidth + "px");	
      const upToCurrent = currentVal.substring(0, caretPosition + 1);	
      mirrorBox.get(0).innerText = upToCurrent;	
      const boxHeight = <number>mirrorBox.outerHeight();	
      let top = boxHeight;	
      // account for vertical offset from padding and overflow handling	
      if(top < (<HTMLTextAreaElement>e.target).offsetHeight) {	
        top -= 12;	
      } else {	
        top = (<HTMLTextAreaElement>e.target).offsetHeight;	
        top += 6;	
      }	
      mirrorBox.css({'width': 'auto'});	
      mirrorBox.get(0).innerText = upToCurrent.split('\n')[upToCurrent.split('\n').length - 1];	
      const left = (<number>mirrorBox.innerWidth()) % ((<HTMLTextAreaElement>e.target).clientWidth - 24);	
      // reset regex last checked index	
      githubLoginTagMatch.lastIndex = 0;	
      while ((matchArray = githubLoginTagMatch.exec(currentVal)) !== null) {	
        // matchArray[0] = whole matched item	
        // matchArray[1] = empty or whitespace (matched to start of string or single whitespace before tag)	
        // matchArray[2] = tag excluding @ symbol	
        const end = githubLoginTagMatch.lastIndex; // last index = end of string	
        const stringLength = matchArray[2].length + 1; // add length of @ char	
        const start = end - stringLength;	
        // if caret is inside tagging user area	
        if(caretPosition >= start && caretPosition < end) {	
          changed = true;	
          matchName = matchArray[2];	
          suggestionBox.removeClass("d-none");	
          suggestionBox.css({top: `${top}px`, left: `${left}px`});	
          $(".tag-user-suggestion-username").addClass("d-none");	
          $(".tag-user-suggestion-username").removeClass("tag-user-suggestion-username-targetted");	
          let shownCount = 0;	
          const children = suggestionBox.children();	
          for(let child of children) {	
            if($(child).text().toLowerCase().includes(matchName.toLowerCase())) {	
              // display child	
              $(child).removeClass("d-none");	
              // add child to current list	
              CurrentUserSuggestionElements.push(child);	
              if(CurrentUserSuggestionElements.length === 1) {	
                CurrentUserSuggestionIndex = 0;	
                $(child).addClass("tag-user-suggestion-username-targetted");	
              }	
              // remove any other click handlers button might have	
              $(child).off('click');	
              $(child).on('click', (ev) => {	
                let currentValue = (<HTMLInputElement>e.target).value;	
                const newValue = currentValue.substring(0, start) + $(ev.target).text() + currentValue.substring(end, currentValue.length);	
                (<HTMLInputElement>e.target).value = newValue;	
                suggestionBox.addClass("d-none");	
                e.target.focus();	
              });	
              // load user image if image hasn't been loaded already	
              const imgChild = $(child).find('img')[0];	
              if($(imgChild).attr('src') === undefined) {	
                $(imgChild).attr('src', $(imgChild).data('src'));	
              }	
              shownCount++;	
              // show a maximum of 5 suggestions	
              if(shownCount === 5) {	
                break;	
              }	
            }	
          }	
          if(shownCount === 0) {	
            suggestionBox.addClass("d-none");	
          }	
          break;	
        }	
      }	
      if(changed === false && !suggestionBox.hasClass("d-none")) {	
        suggestionBox.addClass("d-none");	
        CurrentUserSuggestionElements = [];	
        CurrentUserSuggestionIndex = -1;	
      }
    }
  });

  $(window).on("hashchange", e => {
    highlightCurrentRow();
  });

  $(document).ready(function() {
    highlightCurrentRow();
    hp.addCommentThreadNavigation();
    $(SEL_COMMENT_CELL).each(function () {
      const id = hp.getElementId(this);
      const checked = $(SHOW_COMMENTS_CHECK).prop("checked");
      hp.toggleCommentIcon(id!, !checked);
    });
    $(SEL_CODE_DIAG).each(function () {
      const id = hp.getElementId(this);
      const checked = $(SHOW_SYS_COMMENTS_CHECK).prop("checked");
      hp.toggleCommentIcon(id!, !checked);
    });
  });

  $(document).on("click", ".comment-group-anchor-link", e => {
    e.preventDefault();
    var inlineId = $(e.currentTarget).prop("hash").substring(1);
    var inlineRow = $(`tr[data-inline-id='${inlineId}']`);
    inlineRow[0].scrollIntoView();
    highlightCurrentRow(inlineRow, true);
  });

  $("#jump-to-first-comment").on("click", function () {
    var commentRows = $('.comment-row');
    var displayedCommentRows = hp.getDisplayedCommentRows(commentRows, false, true);
    $(displayedCommentRows[0])[0].scrollIntoView();
  });

  function highlightCurrentRow(rowElement: JQuery<HTMLElement> = $(), isInlineRow: boolean = false) {
    if (location.hash.length < 1 && !isInlineRow) return;
    var row = (rowElement.length > 0) ? rowElement : hp.getCodeRow(location.hash.substring(1));
    row.addClass("active");
    row.on("animationend", () => {
        row.removeClass("active");
    });
  }

  function getReviewId(element: HTMLElement) {
    return hp.getParentData(element, "data-review-id");
  }

  function getLanguage(element: HTMLElement) {
    return hp.getParentData(element, "data-language");
  }

  function getRevisionId(element: HTMLElement) {
    return hp.getParentData(element, "data-revision-id");
  }

  function getTaggedUsers(element: HTMLFormElement): string[] {	
    githubLoginTagMatch.lastIndex = 0;	
    let matchArray;	
    const taggedUsers: string[] = [];	
    const currentValue = new FormData(element).get("commentText") as string;	
    while((matchArray = githubLoginTagMatch.exec(currentValue)) !== null) {	
      taggedUsers.push(matchArray[2]);	
    }	
    return taggedUsers;	
  }

  function getCommentId(element: HTMLElement) {
    return hp.getParentData(element, "data-comment-id");
  }

  function toggleComments(id: string) {
    $(hp.getCommentsRow(id)).find(".comment-holder").toggle();
  }

  function editComment(commentId: string) {
    let commentElement = $(getCommentElement(commentId));
    let commentText = commentElement.find(".js-comment-raw").html();
    let template = createCommentEditForm(commentId, commentText);
    commentElement.replaceWith(template);
  }

  function deleteComment(commentId: string, lineId: string, commentRow: JQuery<HTMLElement>) {
    const reviewId = hp.getReviewAndRevisionIdFromUrl(document.location.href)["reviewId"];
    const elementId = hp.getElementId(getCommentElement(commentId)[0]);
    const url = location.origin + `/comments/delete?reviewid=${reviewId}&commentid=${commentId}&elementid=${elementId}`;
    $.ajax({
      type: "POST",
      url: url,
    }).done(partialViewResult => {
      hp.updateCommentThread(commentRow, partialViewResult);
      hp.addCommentThreadNavigation();
      hp.removeCommentIconIfEmptyCommentBox(lineId);
      PushComment(reviewId, lineId, partialViewResult);
    });
  }

  function getCommentElement(commentId: string) {
    return $(`.review-comment[data-comment-id='${commentId}']`);
  }

  function hideCommentBox(id: string) {
    let commentsRow = hp.getCommentsRow(id);
    let replyDiv = commentsRow.find(".review-thread-reply");
    if (replyDiv.length > 0) {
      replyDiv.show();
      commentsRow.find(".comment-form").hide();
    }
    else {
      commentsRow.remove();
    }
  }

  function createCommentEditForm(commentId: string, text: string) {
    let form = $("#js-comment-edit-form-template").children().clone();
    form.find(".js-comment-id").val(commentId);
    form.find(".new-thread-comment-text").html(text);
    return form;
  }

  function toggleAllCommentsVisibility(showComments: boolean) {
    $(SEL_COMMENT_CELL).each(function () {
      const id = hp.getElementId(this);
      if (id) {
        const tbRow = hp.getCommentsRow(id);
        const prevRow = tbRow.prev(".code-line");
        const nextRow = tbRow.next(".code-line");
        if ((prevRow != undefined && prevRow.hasClass("d-none")) && (nextRow != undefined && nextRow.hasClass("d-none")))
          return;

        (showComments) ? tbRow.removeClass("d-none") : tbRow.addClass("d-none");
        hp.toggleCommentIcon(id, !showComments);
      }
    });
  }

  function toggleAllDiagnosticsVisibility(showComments: boolean) {
    $(SEL_CODE_DIAG).each(function () {
      const id = hp.getElementId(this);
      if (id) {
        const tbRow = hp.getDiagnosticsRow(id);
        const prevRow = tbRow.prev(".code-line");
        const nextRow = tbRow.next(".code-line");
        if ((prevRow != undefined && prevRow.hasClass("d-none")) && (nextRow != undefined && nextRow.hasClass("d-none")))
          return;

        (showComments) ? tbRow.removeClass("d-none") : tbRow.addClass("d-none");
        hp.toggleCommentIcon(id, !showComments);
      }
    });
  }

  function toggleSingleCommentAndDiagnostics(id: string) {
    hp.getCommentsRow(id).toggleClass("d-none");
    hp.getDiagnosticsRow(id).toggleClass("d-none");
  }

  function getSingleCommentAndDiagnosticsDisplayStatus(id: string) {
    return !(hp.getCommentsRow(id).hasClass("d-none") || hp.getDiagnosticsRow(id).hasClass("d-none"));
  }

  function toggleSingleResolvedComment(id: string) {
    let commentHolder = $(hp.getCommentsRow(id)).find(".comment-holder").first();
    if (commentHolder.hasClass("comments-resolved")) {
      toggleComments(id);
    }
  }
});
