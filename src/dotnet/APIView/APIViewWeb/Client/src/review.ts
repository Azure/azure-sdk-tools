$(() => {  
  const SEL_DOC_CLASS = ".documentation";
  const SHOW_DOC_CHECK_COMPONENT = "#show-documentation-component";
  const SHOW_DOC_CHECKBOX = ".show-doc-checkbox";
  const SHOW_DOC_HREF = ".show-document";
  const SHOW_DIFFONLY_CHECKBOX = ".show-diffonly-checkbox";
  const SHOW_DIFFONLY_HREF = ".show-diffonly";
  const HIDE_LINE_NUMBERS = "#hide-line-numbers";

  hideCheckboxIfNoDocs();

  function hideCheckboxIfNoDocs() {
      if ($(SEL_DOC_CLASS).length == 0) {
          $(SHOW_DOC_CHECK_COMPONENT).hide();
      }
  }

  $(SHOW_DOC_CHECKBOX).on("click", e => {
    $(SHOW_DOC_HREF)[0].click();
  });

  $(SHOW_DIFFONLY_CHECKBOX).on("click", e => {
    $(SHOW_DIFFONLY_HREF)[0].click();
  });

  $(HIDE_LINE_NUMBERS).on("click", e => {
    $(".line-number").toggleClass("d-none");
  });

  /* DIFF BUTTON (UPDATES REVIEW PAGE ON CLICK)
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
  $('.diff-button').each(function(index, value){
    $(this).on('click', function () {
      window.location.href = $(this).val() as string;
    });
  });


  /* DROPDOWN FILTER FOR REVIEW, REVISIONS AND DIFF (UPDATES REVIEW PAGE ON CHANGE)
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
  $('#revisions-bootstraps-select, #review-bootstraps-select, #diff-bootstraps-select').each(function(index, value) {
    $(this).on('change', function() {
      var url = $(this).find(":selected").val();
      if (url)
      {
        window.location.href = url as string;
      }
    });
  });

  /* COLLAPSIBLE CODE LINES (EXPAND AND COLLAPSE FEATURE)
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
  function toggleSectionContent(headingRow, sectionContent, caretDirection, caretIcon ) {
    sectionContent.toggleClass("d-none");

    if (caretDirection.endsWith("right")) {
      caretIcon.removeClass("fa-angle-right");
      caretIcon.addClass("fa-angle-down");
    }
    else {
      caretIcon.removeClass("fa-angle-down");
      caretIcon.addClass("fa-angle-right");
    }

    headingRow.find(".row-fold-elipsis").toggleClass("d-none");
  }
  $('.row-fold-elipsis, .row-fold-caret').on('click', function () {
    var headingRow = $(this).parents('.code-line');
    var headingRowClasses = headingRow.attr('class');
    var caretIcon = headingRow.find(".row-fold-caret").children("i");
    var caretClasses = caretIcon.attr("class");
    var caretDirection = caretClasses ? caretClasses.split(' ').filter(c => c.startsWith('fa-angle-'))[0] : "";
    var sectionId = headingRowClasses ? headingRowClasses.split(' ').filter(c => c.startsWith('code-line-section-heading-'))[0].replace("code-line-section-heading-", "") : "";
    var sectionContent = $(`.code-line-section-content-${sectionId}`);

    if (sectionContent.hasClass("content-loaded")) {
      toggleSectionContent(headingRow, sectionContent, caretDirection, caretIcon);
    }
    else {
      var uri = '?handler=codelinesection';
      var uriPath = location.pathname.split('/');
      var reviewId = uriPath[uriPath.length - 1];
      var revisionId = new URLSearchParams(location.search).get("revisionId");
      uri = uri + '&id=' + reviewId + '&sectionId=' + sectionId;
      if (revisionId) {
        uri = uri + '&revisionId=' + revisionId;
      }

      $.ajax({
        url: uri
      }).done(function (partialViewResult) {
        sectionContent.html(partialViewResult);
        toggleSectionContent(headingRow, sectionContent, caretDirection, caretIcon);
        sectionContent.addClass("content-loaded");
      });
    }
  });

  /* ENABLE TOOLTIP AND POPOVER
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
  (<any>$('[data-toggle="tooltip"]')).tooltip();
  (<any>$('[data-toggle="popover"]')).popover();
});
