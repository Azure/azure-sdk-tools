$(() => {
  const INVISIBLE = "invisible";
  const SEL_CODE_DIAG = ".code-diagnostics";
  const SEL_COMMENT_ICON = ".icon-comments";
  const SEL_COMMENT_CELL = ".comment-cell";

  let MessageIconAddedToDom = false;

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

  $(document).on("click", "#show-comments-checkbox", e => {
    ensureMessageIconInDOM();
    toggleAllCommentsVisibility(e.target.checked);
  });

  $(document).on("click", "#show-system-comments-checkbox", e => {
    ensureMessageIconInDOM();
    toggleAllDiagnosticsVisibility(e.target.checked);
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
    if (e.ctrlKey && (e.keyCode === 10 || e.keyCode === 13)) {
      const form = $(e.target).closest("form");
      if (form) {
          form.submit();
      }
      e.preventDefault();
    }
  });

  $(window).on("hashchange", e => {
    highlightCurrentRow();
  });

  $(document).ready(function() {
    highlightCurrentRow();
    addCommentThreadNavigation();
  });

  $(document).on("click", ".comment-group-anchor-link", e => {
    e.preventDefault();
    var inlineId = $(e.currentTarget).prop("hash").substring(1);
    var inlineRow = $(`tr[data-inline-id='${inlineId}']`);
    inlineRow[0].scrollIntoView();
    highlightCurrentRow(inlineRow);
  });

  function highlightCurrentRow(rowElement: JQuery<HTMLElement> = $()) {
    if (location.hash.length < 1) return;
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

  function getReplyGroupNo(sibling: JQuery<HTMLElement>) {
    return $(sibling).prevAll("a").first().find("small");
  }

  function getCodeRowSectionClasses(id: string) {
    var codeRow = getCodeRow(id);
    var rowSectionClasses = "";
    if (codeRow) {
      rowSectionClasses = getRowSectionClasses(codeRow[0].classList);
    }
    return rowSectionClasses;
  }

  function getRowSectionClasses(classList: DOMTokenList) {
    const rowSectionClasses: string[] = [];
    for (const value of classList.values()) {
      if (value == "section-loaded" || value.startsWith("code-line-section-content") || value.match(/lvl_[0-9]+_(parent|child)_[0-9]+/)) {
        rowSectionClasses.push(value);
      }
    }
    return rowSectionClasses.join(' ');
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

  function getCodeRow(id: string) {
    return $(`.code-line[data-line-id='${id}']`);
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
      var id = getElementId(this);
      if (id) {
          getCommentsRow(id).toggle(showComments);
          toggleCommentIcon(id, !showComments);
      }
    });
  }

  function toggleAllDiagnosticsVisibility(showComments: boolean) {
    $(SEL_CODE_DIAG).each(function () {
      var id = getElementId(this);
      if (id) {
          getDiagnosticsRow(id).toggle(showComments);
          toggleCommentIcon(id, !showComments);
      }
    });
  }

  function toggleSingleCommentAndDiagnostics(id: string) {
    getCommentsRow(id).toggle();
    getDiagnosticsRow(id).toggle();
  }

  function ensureMessageIconInDOM() {
    if (!MessageIconAddedToDom) {
      $(".line-comment-button-cell").append(`<span class="icon icon-comments ` + INVISIBLE + `"><i class="far fa-comment-alt pt-1 pl-1"></i></span>`);
      MessageIconAddedToDom = true;
    }
  }

  function toggleCommentIcon(id, show: boolean) {
    getCodeRow(id).find(SEL_COMMENT_ICON).toggleClass(INVISIBLE, !show);
  }

  function addCommentThreadNavigation(){
    var commentRows = $('.comment-row');
    var displayedCommentRows: JQuery<HTMLElement>[] = [];

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
          commentNavigationButtons.append(`<a class="btn btn-outline-dark" href="#${nextCommentThreadAnchor}" title="Next Comment"><i class="fa fa-chevron-down" aria-hidden="true"></i></a>`)
        }
        else if (index == displayedCommentRows.length - 1) {
          commentNavigationButtons.append(`<a class="btn btn-outline-dark" href="#${previousCommentThreadAnchor}" title="Previous Comment"><i class="fa fa-chevron-up" aria-hidden="true"></i></a>`)
        }
        else {
          commentNavigationButtons.append(`<a class="btn btn-outline-dark" href="#${previousCommentThreadAnchor}" title="Previous Comment"><i class="fa fa-chevron-up" aria-hidden="true"></i></a>`)
          commentNavigationButtons.append(`<a class="btn btn-outline-dark ml-1" href="#${nextCommentThreadAnchor}" title="Next Comment"><i class="fa fa-chevron-down" aria-hidden="true"></i></a>`)
        }
      });
    }
  }
});
