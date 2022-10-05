import Split from "split.js";
import { updatePageSettings } from "./helpers";

$(() => {  
  const SEL_DOC_CLASS = ".documentation";
  const SHOW_DOC_CHECK_COMPONENT = "#show-documentation-component";
  const SHOW_DOC_CHECKBOX = ".show-doc-checkbox";
  const SHOW_DOC_HREF = ".show-document";
  const SHOW_DIFFONLY_CHECKBOX = ".show-diffonly-checkbox";
  const SHOW_DIFFONLY_HREF = ".show-diffonly";
  const TOGGLE_DOCUMENTATION = ".line-toggle-documentation-button";

  hideCheckboxIfNoDocs();

  /* FUNCTIONS
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
  function hideCheckboxIfNoDocs() {
    if ($(SEL_DOC_CLASS).length == 0) {
      $(SHOW_DOC_CHECK_COMPONENT).hide();
    }
  }

  /* Split left and right review panes using split.js */
  function splitReviewPageContent() {
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

  /* Update Icons that indicate if Section is Expanded or Collapsed */
  function updateSectionHeadingIcons(setTo: string, caretIcon, headingRow) {
    if (setTo == "OPEN") {
      caretIcon.removeClass("fa-angle-right");
      caretIcon.addClass("fa-angle-down");
      caretIcon.css("color", "darkorange");
      headingRow.find(".row-fold-elipsis").addClass("d-none");
    }

    if (setTo == "CLOSE") {
      caretIcon.removeClass("fa-angle-down");
      caretIcon.addClass("fa-angle-right");
      caretIcon.css("color", "darkcyan");
      headingRow.find(".row-fold-elipsis").removeClass("d-none");
    }
  }

  /* Expand or Collapse CodeLine Top Level Sections */
  function toggleSectionContent(headingRow, sectionContent, caretDirection, caretIcon) {
    if (caretDirection.endsWith("right")) {
      // In case the section passed has already been replaced with more rows
      if (sectionContent.length == 1) {
        var sectionContentClass = sectionContent[0].className.replace(/\s/g, '.');
        sectionContent = $(`.${sectionContentClass}`);
      }

      $.each(sectionContent, function (index, value) {
        let rowClasses = $(value).attr("class");
        if (rowClasses) {
          if (rowClasses.match(/lvl_1_/)) { // Only show first level rows of the section
            $(value).removeClass("d-none");
            $(value).find("svg").attr("height", `${$(value).height()}`);
          }
        }
      });

      // Update section heading icons to open state
      updateSectionHeadingIcons("OPEN", caretIcon, headingRow);

    }
    else {
      $.each(sectionContent, function (index, value) {
        let rowClasses = $(value).attr("class");
        if (rowClasses) {
          if (rowClasses.match(/lvl_[0-9]+_parent_/)) {
            // Update all heading/parent rows to closed state before hiding it
            let caretIcon = $(value).find(".row-fold-caret").children("i");
            updateSectionHeadingIcons("CLOSE", caretIcon, $(value));
          }
        }
        $(value).addClass("d-none");
      });

      // Update section heading icons to closed state
      updateSectionHeadingIcons("CLOSE", caretIcon, headingRow);
    }
  }

  /* Expand or Collapse CodeLine SubSections */
  function toggleSubSectionContent(headingRow, subSectionLevel, subSectionHeadingPosition, subSectionContentClass, caretDirection, caretIcon, lineNumber) {
    var subSectionDescendants = $(`.${subSectionContentClass}`);

    if (caretDirection.endsWith("right")) {
      var startShowing = false;

      $.each(subSectionDescendants, function (index, value) {
        var rowClasses = $(value).attr("class");
        var rowLineNumber = $(value).find(".line-number>span").text();
        if (rowClasses) {
          if (rowClasses.match(new RegExp(`lvl_${subSectionLevel}_parent_${subSectionHeadingPosition}`)) && rowLineNumber == lineNumber)
            startShowing = true;

          if (startShowing && (rowClasses.match(new RegExp(`lvl_${subSectionLevel}_parent_${Number(subSectionHeadingPosition) + 1}`))
            || rowClasses.match(new RegExp(`lvl_${subSectionLevel}_child_${Number(subSectionHeadingPosition) + 1}`))
            || rowClasses.match(new RegExp(`lvl_${Number(subSectionLevel) - 1}_`))))
            return false;

          // Show only immediate descendants
          if (startShowing) {
            if (rowClasses.match(new RegExp(`lvl_${Number(subSectionLevel) + 1}_`))) {
              $(value).removeClass("d-none");
              let rowHeight = $(value).height() ?? 0;
              $(value).find("svg").attr("height", `${rowHeight}`);
            }
          }
        }
      });

      // Update section heading icons to open state
      updateSectionHeadingIcons("OPEN", caretIcon, headingRow);
    }
    else {
      var startHiding = false;

      $.each(subSectionDescendants, function (index, value) {
        var rowClasses = $(value).attr("class");
        var rowLineNumber = $(value).find(".line-number>span").text();
        if (rowClasses) {
          if (rowClasses.match(new RegExp(`lvl_${subSectionLevel}_parent_${subSectionHeadingPosition}`)) && rowLineNumber == lineNumber)
            startHiding = true;

          if (startHiding && (rowClasses.match(new RegExp(`lvl_${subSectionLevel}_parent_${Number(subSectionHeadingPosition) + 1}`))
            || rowClasses.match(new RegExp(`lvl_${subSectionLevel}_child_${Number(subSectionHeadingPosition) + 1}`))
            || rowClasses.match(new RegExp(`lvl_${Number(subSectionLevel) - 1}_`))))
            return false;

          if (startHiding) {
            let descendantClasses = rowClasses.split(' ').filter(c => c.match(/lvl_[0-9]+_child_.*/))[0];
            if (descendantClasses) {
              let descendantLevel = descendantClasses.split('_')[1];
              if (/^\d+$/.test(descendantLevel)) {
                if (Number(descendantLevel) > Number(subSectionLevel)) {
                  $(value).addClass("d-none");
                  if (rowClasses.match(/lvl_[0-9]+_parent_.*/)) {
                    // Update all heading/parent rows to closed state before hiding it
                    let caretIcon = $(value).find(".row-fold-caret").children("i");
                    updateSectionHeadingIcons("CLOSE", caretIcon, $(value));

                  }
                }
              }
            }
          }
        }
      });

      // Update section heading icons to closed state
      updateSectionHeadingIcons("CLOSE", caretIcon, headingRow);
    }
  }

  /* On Click Handler for Expand/Collapse of CodeLine Sections and SubSections  */
  function toggleCodeLines(headingRow) {
    var headingRowClasses = headingRow.attr('class');
    var caretIcon = headingRow.find(".row-fold-caret").children("i");
    var caretClasses = caretIcon.attr("class");
    var caretDirection = caretClasses ? caretClasses.split(' ').filter(c => c.startsWith('fa-angle-'))[0] : "";
    var subSectionHeadingClass = headingRowClasses ? headingRowClasses.split(' ').filter(c => c.startsWith('code-line-section-heading-'))[0] : "";
    var subSectionContentClass = headingRowClasses ? headingRowClasses.split(' ').filter(c => c.startsWith('code-line-section-content-'))[0] : "";

    if (subSectionHeadingClass) {
      var sectionId = subSectionHeadingClass.replace("code-line-section-heading-", "")
      if (/^\d+$/.test(sectionId)) {
        var sectionContent = $(`.code-line-section-content-${sectionId}`);
        if (sectionContent.hasClass("section-loaded")) {
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

          var loadingMarkUp = "<td class='spinner-border spinner-border-sm ml-4' role='status'><span class='sr-only'>Loading...</span></td>"
          sectionContent.children("td").after(loadingMarkUp);
          sectionContent.removeClass("d-none");

          var request = $.ajax({ url: uri });
          request.done(function (partialViewResult) {
            sectionContent.replaceWith(partialViewResult);
            toggleSectionContent(headingRow, sectionContent, caretDirection, caretIcon);
            addToggleEventHandlers();
          });
          request.fail(function () {
            // Alert here
          });
          return request;
        }
      }
    }

    if (subSectionContentClass) {
      var subSectionClass = headingRowClasses ? headingRowClasses.split(' ').filter(c => c.match(/.*lvl_[0-9]+_parent.*/))[0] : "";
      var lineNumber = headingRow.find(".line-number>span").text();
      if (subSectionClass) {
        var subSectionLevel = subSectionClass.split('_')[1];
        var subSectionHeadingPosition = subSectionClass.split('_')[3];
        if (/^\d+$/.test(subSectionLevel) && /^\d+$/.test(subSectionHeadingPosition)) {
          toggleSubSectionContent (headingRow, subSectionLevel, subSectionHeadingPosition, subSectionContentClass, caretDirection, caretIcon, lineNumber);
        }
      }
    }
  }

  /* Add event handler for Expand/Collapse of CodeLine Sections and SubSections */
  function addToggleEventHandlers() {
    $('.row-fold-elipsis, .row-fold-caret').on('click', function (event) {
      event.preventDefault();
      event.stopImmediatePropagation();
      var headingRow = $(event.currentTarget).parents('.code-line').first();
      toggleCodeLines(headingRow);

    });
  }

  /* ADD FUNCTIONS TO LEFT NAVIGATION
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
  /* Enable expand/collapse of navigation groups */
  $(".nav-list-toggle").on('click', function (e) {
    var navItemRow = $(this).parents(".nav-list-group").first();
    if (navItemRow.hasClass("nav-list-collapsed")) {
      var navItemLink = navItemRow.children('a').first();
      var navItemHash = navItemLink.prop('hash');
      if (navItemHash) {
        var targetAnchorId = navItemHash.replace('#', '');
        var targetAnchor = document.getElementById(targetAnchorId);
        if (targetAnchor) {
          var targetAnchorRow = $(targetAnchor).parents(".code-line").first();
          var rowFoldSpan = targetAnchorRow.find(".row-fold-caret");
          if (rowFoldSpan.length > 0) {
            var caretIcon = rowFoldSpan.children("i");
            var caretClasses = caretIcon.attr("class");
            var caretDirection = caretClasses ? caretClasses.split(' ').filter(c => c.startsWith('fa-angle-'))[0] : "";
            if (caretDirection.endsWith("right")) {
              $.when(toggleCodeLines(targetAnchorRow)).then(function () {
                navItemRow.removeClass("nav-list-collapsed");
              });
            }
            else {
              navItemRow.removeClass("nav-list-collapsed");
            }
          }
        }
      }
      else {
        navItemRow.removeClass("nav-list-collapsed");
      }
      
    }
    else {
      navItemRow.addClass("nav-list-collapsed");
    }
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
    /*if((e.target as HTMLInputElement).checked) {
      // show all documentation
      $(".code-line-documentation").removeClass('hidden-row');
      $(TOGGLE_DOCUMENTATION).children('i').removeClass("fa-square-plus");
      $(TOGGLE_DOCUMENTATION).children('i').addClass("fa-square-minus");
      $(TOGGLE_DOCUMENTATION).children('i').css("color", "darkorange");
      $(TOGGLE_DOCUMENTATION).children('svg').removeClass("invisible");
    } else {
      // hide all documentation
      $(".code-line-documentation").addClass("hidden-row");
      $(TOGGLE_DOCUMENTATION).children('i').removeClass("fa-square-minus");
      $(TOGGLE_DOCUMENTATION).children('i').addClass("fa-square-plus");
      $(TOGGLE_DOCUMENTATION).children('i').css("color", "darkcyan");
      $(TOGGLE_DOCUMENTATION).children('svg').addClass("invisible");
    }*/
  });

  $(SHOW_DOC_HREF).on("click", e => {
    $(SHOW_DOC_CHECKBOX)[0].click();
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

  /* TOGGLE DOCUMENTATION DROPDOWN
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
  $(TOGGLE_DOCUMENTATION).on("click", function(e){
    const documentedBy = $(this).data('documented-by');
    const codeLines = $(".code-window > tbody > .code-line");
    
    for(var i = 0; i < documentedBy.length; i++) {
      $(codeLines[documentedBy[i] - 1]).toggleClass("hidden-row");
    }

    $(this).children('i').toggleClass('fa-square-minus');
    $(this).children('i').toggleClass('fa-square-plus');
    if ($(this).children('i').hasClass('fa-square-plus')) {
      $(this).children('i').css("color", "darkcyan");
      $(this).children('svg').addClass("invisible");
    }
    else {
      $(this).children('i').css("color", "darkorange");
      $(this).children('svg').removeClass("invisible");
    }

    // scroll button to center of screen, so that the line is visible after toggling folding
    $(this).get(0).scrollIntoView({ block: "center"});
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
  addToggleEventHandlers();

  /* ENABLE TOOLTIP AND POPOVER
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
  (<any>$('[data-toggle="tooltip"]')).tooltip();
  (<any>$('[data-toggle="popover"]')).popover();
});
