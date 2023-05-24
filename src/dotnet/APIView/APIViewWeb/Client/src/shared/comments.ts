import {
  updatePageSettings, getCodeRow, getCodeRowSectionClasses,
  getRowSectionClasses, toggleCommentIcon
} from "../shared/helpers";

$(() => {
  const INVISIBLE = "invisible";
  const SEL_CODE_DIAG = ".code-diagnostics";
  const SEL_COMMENT_ICON = ".icon-comments";
  const SEL_COMMENT_CELL = ".comment-cell";
  const SHOW_COMMENTS_CHECK = "#show-comments-checkbox";
  const SHOW_SYS_COMMENTS_CHECK = "#show-system-comments-checkbox";

  let CurrentUserSuggestionElements: HTMLElement[] = [];	
  let CurrentUserSuggestionIndex = -1;	
  // simple github username match	
  const githubLoginTagMatch = /(\s|^)@([a-zA-Z\d-]+)/g;

  $(document).on("click", ".commentable", e => {
    var rowSectionClasses = getCodeRowSectionClasses(e.target.id);
    showCommentBox(e.target.id, rowSectionClasses);
    e.preventDefault();
  });

  $(document).on("click", ".line-comment-button", e => {
    let id = getElementId(e.target);
    let inlineId = getElementId(e.target, "data-inline-id");
    if (id) {
      var rowSectionClasses = getCodeRowSectionClasses(id);
      if (inlineId) {
        let groupNo = inlineId.replace(`${id}-tr-`, '');
        showCommentBox(id, rowSectionClasses, groupNo);
      }
      else {
        showCommentBox(id, rowSectionClasses);
      }
  }
  e.preventDefault();
  });

  $(document).on("click", ".comment-cancel-button", e => {
    let id = getElementId(e.target);
    if (id) {
      hideCommentBox(id);
      // if a comment was added and then cancelled, and there are no other
      // comments for the thread, we should remove the comments icon.
      if (getCommentsRow(id).find(SEL_COMMENT_CELL).length === 0) {
          getCodeRow(id).find(SEL_COMMENT_ICON).addClass(INVISIBLE);
      }
    }
    e.preventDefault();
  });

  $(document).on("click", SHOW_COMMENTS_CHECK, e => {
    updatePageSettings(function () {
      const checked = $(SHOW_COMMENTS_CHECK).prop("checked");
      toggleAllCommentsVisibility(checked);
    });
  });

  $(document).on("click", SHOW_SYS_COMMENTS_CHECK, e => {
    updatePageSettings(function () {
      const checked = $(SHOW_SYS_COMMENTS_CHECK).prop("checked");
      toggleAllDiagnosticsVisibility(checked);
    });
  });

  $(document).on("click", SEL_COMMENT_ICON, e => {
    let lineId = getElementId(e.target);
    if (lineId) {
      toggleSingleCommentAndDiagnostics(lineId);
    }
    e.preventDefault();
  });

  $(document).on("submit", "form[data-post-update='comments']", e => {
    const form = <HTMLFormElement><any>$(e.target);
    let lineId = getElementId(e.target);
    let inlineRowNo = $(e.target).find(".new-comment-content small");

    if (inlineRowNo.length == 0) {
      inlineRowNo = getReplyGroupNo($(e.target));
    }

    if (lineId) {
      let commentRow = getCommentsRow(lineId);
      let rowSectionClasses = getRowSectionClasses(commentRow[0].classList);
      let serializedForm = form.serializeArray();
      serializedForm.push({ name: "elementId", value: lineId });
      serializedForm.push({ name: "reviewId", value: getReviewId(e.target) });
      serializedForm.push({ name: "revisionId", value: getRevisionId(e.target) });
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
        updateCommentThread(commentRow, partialViewResult);
        addCommentThreadNavigation();
      });
    }
    e.preventDefault();
  });

  $(document).on("click", ".review-thread-reply-button", e => {
    let lineId = getElementId(e.target);
    let inlineRowNo = getReplyGroupNo($(e.target).parent().parent());
    if (lineId) {
      if (inlineRowNo.length > 0) {
        let groupNo = inlineRowNo.text().replace("ROW-", '');
        showCommentBox(lineId,'', groupNo);
      }
      else {
        showCommentBox(lineId);
      }
    }
    e.preventDefault();
  });

  $(document).on("click", ".toggle-comments", e => {
    let lineId = getElementId(e.target);
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
      apiViewUrl = window.location.href.split("#")[0] + "%23" + escape(escape(getElementId(commentElement[0])!));
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
    addCommentThreadNavigation();
    $(SEL_COMMENT_CELL).each(function () {
      const id = getElementId(this);
      const checked = $(SHOW_COMMENTS_CHECK).prop("checked");
      toggleCommentIcon(id!, !checked);
    });
    $(SEL_CODE_DIAG).each(function () {
      const id = getElementId(this);
      const checked = $(SHOW_SYS_COMMENTS_CHECK).prop("checked");
      toggleCommentIcon(id!, !checked);
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
    var displayedCommentRows = getDisplayedCommentRows(commentRows, false, true);
    $(displayedCommentRows[0])[0].scrollIntoView();
  });

  function highlightCurrentRow(rowElement: JQuery<HTMLElement> = $(), isInlineRow: boolean = false) {
    if (location.hash.length < 1 && !isInlineRow) return;
    var row = (rowElement.length > 0) ? rowElement : getCodeRow(location.hash.substring(1));
    row.addClass("active");
    row.on("animationend", () => {
        row.removeClass("active");
    });
  }

  function getReviewId(element: HTMLElement) {
    return getParentData(element, "data-review-id");
  }

  function getLanguage(element: HTMLElement) {
    return getParentData(element, "data-language");
  }

  function getRevisionId(element: HTMLElement) {
    return getParentData(element, "data-revision-id");
  }

  function getElementId(element: HTMLElement, idName: string = "data-line-id") {
    return getParentData(element, idName);
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

  function getReplyGroupNo(sibling: JQuery<HTMLElement>) {
    return $(sibling).prevAll("a").first().find("small");
  }

  function getCommentId(element: HTMLElement) {
    return getParentData(element, "data-comment-id");
  }

  function getParentData(element: HTMLElement, name: string) {
    return $(element).closest(`[${name}]`).attr(name);
  }

  function toggleComments(id: string) {
    $(getCommentsRow(id)).find(".comment-holder").toggle();
  }

  function editComment(commentId: string) {
    let commentElement = $(getCommentElement(commentId));
    let commentText = commentElement.find(".js-comment-raw").html();
    let template = createCommentEditForm(commentId, commentText);
    commentElement.replaceWith(template);
  }

  function getCommentElement(commentId: string) {
    return $(`.review-comment[data-comment-id='${commentId}']`);
  }

  function getCommentsRow(id: string) {
    return $(`.comment-row[data-line-id='${id}']`);
  }

  function getDiagnosticsRow(id: string) {
    return $(`.code-diagnostics[data-line-id='${id}']`);
  }

  function hideCommentBox(id: string) {
    let commentsRow = getCommentsRow(id);
    let replyDiv = commentsRow.find(".review-thread-reply");
    if (replyDiv.length > 0) {
      replyDiv.show();
      commentsRow.find(".comment-form").hide();
    }
    else {
      commentsRow.remove();
    }
  }

  function showCommentBox(id: string, classes: string = '', groupNo: string = '') {
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

    commentForm.find(".new-thread-comment-text").focus();
  }

  function createCommentForm(groupNo: string = '') {
    var commentForm = $("#js-comment-form-template").children().clone();
    if (groupNo) {
      commentForm.find("form .new-comment-content").prepend(`<span class="badge badge-pill badge-light mb-2"><small>ROW-${groupNo}</small></span>`);
    }
    return commentForm;
  }

  function createCommentEditForm(commentId: string, text: string) {
    let form = $("#js-comment-edit-form-template").children().clone();
    form.find(".js-comment-id").val(commentId);
    form.find(".new-thread-comment-text").html(text);
    return form;
  }

  function updateCommentThread(commentBox, partialViewResult) {
    partialViewResult = $.parseHTML(partialViewResult);
    $(commentBox).replaceWith(partialViewResult);
    return false;
  }

  function toggleAllCommentsVisibility(showComments: boolean) {
    $(SEL_COMMENT_CELL).each(function () {
      const id = getElementId(this);
      if (id) {
        const tbRow = getCommentsRow(id);
        const prevRow = tbRow.prev(".code-line");
        const nextRow = tbRow.next(".code-line");
        if ((prevRow != undefined && prevRow.hasClass("d-none")) && (nextRow != undefined && nextRow.hasClass("d-none")))
          return;

        (showComments) ? tbRow.removeClass("d-none") : tbRow.addClass("d-none");
        toggleCommentIcon(id, !showComments);
      }
    });
  }

  function toggleAllDiagnosticsVisibility(showComments: boolean) {
    $(SEL_CODE_DIAG).each(function () {
      const id = getElementId(this);
      if (id) {
        const tbRow = getDiagnosticsRow(id);
        const prevRow = tbRow.prev(".code-line");
        const nextRow = tbRow.next(".code-line");
        if ((prevRow != undefined && prevRow.hasClass("d-none")) && (nextRow != undefined && nextRow.hasClass("d-none")))
          return;

        (showComments) ? tbRow.removeClass("d-none") : tbRow.addClass("d-none");
        toggleCommentIcon(id, !showComments);
      }
    });
  }

  function toggleSingleCommentAndDiagnostics(id: string) {
    getCommentsRow(id).toggleClass("d-none");
    getDiagnosticsRow(id).toggleClass("d-none");
  }

  function getDisplayedCommentRows(commentRows: JQuery<HTMLElement>, clearCommentAnchors = false, returnFirst = false) {
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

  function addCommentThreadNavigation(){
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
});
