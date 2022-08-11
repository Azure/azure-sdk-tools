import Split from "split.js";
import { updatePageSettings } from "./helpers";

$(() => {  
  const SEL_DOC_CLASS = ".documentation";
  const SHOW_DOC_CHECK_COMPONENT = "#show-documentation-component";
  const SHOW_DOC_CHECKBOX = ".show-doc-checkbox";
  const SHOW_DOC_HREF = ".show-document";
  const SHOW_DIFFONLY_CHECKBOX = ".show-diffonly-checkbox";
  const SHOW_DIFFONLY_HREF = ".show-diffonly";

  hideCheckboxIfNoDocs();

  /* FUNCTIONS
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
  function hideCheckboxIfNoDocs() {
    if ($(SEL_DOC_CLASS).length == 0) {
      $(SHOW_DOC_CHECK_COMPONENT).hide();
    }
  }

  function splitReviewPageContent() {
    /* Split left and right review panes using split.js */
    const rl = $('#review-left');
    const rr = $('#review-right');

    if (rl.length && rr.length) {
      Split(['#review-left', '#review-right'], {
        direction: 'horizontal',
        sizes: [17, 83],
        elementStyle: (dimension, size, gutterSize) => {
          return {
            'flex-basis': `calc(${size}% - ${gutterSize}px`
          }
        },
        gutterStyle: (dimension, gutterSize) => {
          return {
            'flex-basis': `${gutterSize}px`
          }
        }
      });
    }
  }

  /* ADD EVENT LISTENER FOR TOGGLING LEFT NAVIGATION
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
  $(".nav-list-toggle").on('click', function (e) {
    $(this).parents(".nav-list-group").first().toggleClass("nav-list-collapsed");
    console.log(e);
  });

  /* SPLIT REVIEW PAGE CONTENT
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
  /* 992px matches bootstrap col-lg min-width */
  ($('.namespace-view') as any).stickySidebar({ minWidth: 992 });
  if (!$("#review-left").hasClass("d-none"))
  {
    // Only Add Split gutter if left navigation is not hidden
    splitReviewPageContent();
  }

  /* TOGGLE PAGE OPTIONS
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
  $(SHOW_DOC_CHECKBOX).on("click", e => {
    $(SHOW_DOC_HREF)[0].click();
  });

  $(SHOW_DIFFONLY_CHECKBOX).on("click", e => {
    $(SHOW_DIFFONLY_HREF)[0].click();
  });

  $("#hide-line-numbers").on("click", e => {
    updatePageSettings(function(){
      $(".line-number").toggleClass("d-none");
    });
  });

  $("#hide-left-navigation").on("click", e => {
    updatePageSettings(function(){
      var leftContainer = $("#review-left");
      var rightContainer = $("#review-right");
      var gutter = $(".gutter-horizontal");

      if (leftContainer.hasClass("d-none")) {
        leftContainer.removeClass("d-none");
        rightContainer.removeClass("col-12");
        rightContainer.addClass("col-10");
        splitReviewPageContent();
      }
      else {
        leftContainer.addClass("d-none");
        rightContainer.css("flex-basis", "100%");
        gutter.remove();
        rightContainer.removeClass("col-10");
        rightContainer.addClass("col-12");
      }
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
        sectionContent.replaceWith(partialViewResult);
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
