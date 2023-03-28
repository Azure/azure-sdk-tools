import Split from "split.js";
import { updatePageSettings, toggleCommentIcon } from "../shared/helpers";
import { rightOffCanvasNavToggle } from "../shared/off-canvas";

$(() => {  
  const SEL_DOC_CLASS = ".documentation";
  const SHOW_DOC_CHECK_COMPONENT = "#show-documentation-component";
  const SHOW_DOC_CHECKBOX = ".show-documentation-checkbox";
  const SHOW_DOC_HREF = ".show-documentation-switch";
  const SHOW_DIFFONLY_CHECKBOX = ".show-diffonly-checkbox";
  const SHOW_DIFFONLY_HREF = ".show-diffonly-switch";
  const TOGGLE_DOCUMENTATION = ".line-toggle-documentation-button";
  const SEL_HIDDEN_CLASS = ".hidden-api-toggleable";
  const SHOW_HIDDEN_CHECK_COMPONENT = "#show-hidden-api-component";
  const SHOW_HIDDEN_CHECKBOX = "#show-hidden-api-checkbox";
  const SHOW_HIDDEN_HREF = ".show-hidden-api";

  hideCheckboxesIfNotApplicable();

  /* FUNCTIONS
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
  function hideCheckboxesIfNotApplicable() {
    if ($(SEL_DOC_CLASS).length == 0) {
      $(SHOW_DOC_CHECK_COMPONENT).hide();
    }
    if ($(SEL_HIDDEN_CLASS).length == 0) {
      $(SHOW_HIDDEN_CHECK_COMPONENT).hide();
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
      headingRow.find(".row-fold-elipsis").addClass("d-none");
    }

    if (setTo == "CLOSE") {
      caretIcon.removeClass("fa-angle-down");
      caretIcon.addClass("fa-angle-right");
      headingRow.find(".row-fold-elipsis").removeClass("d-none");
    }
  }

  /* Expand or Collapse CodeLine Top Level Sections */
  function toggleSectionContent(headingRow, sectionContent, caretDirection, caretIcon) {
    if (caretDirection.endsWith("right")) {
      // In case the section passed has already been replaced with more rows
      if (sectionContent.length == 1) {
        const sectionContentClass = sectionContent[0].className.replace(/\s/g, '.');
        const sectionCommentClass = sectionContentClass.replace("code-line.", "comment-row.");
        sectionContent = $(`.${sectionContentClass}`);
        sectionContent.push(...$(`.${sectionCommentClass}`));
      }

      $.each(sectionContent, function (index, value) {
        let rowClasses = $(value).attr("class");
        if (rowClasses) {
          if (rowClasses.match(/lvl_1_/)) {
            if (rowClasses.match(/comment-row/) && !$("#show-comments-checkbox").prop("checked")) {
              toggleCommentIcon($(value).attr("data-line-id"), true);
              return; // Dont show comment row if show comments setting is unchecked
            }
            disableCommentsOnInRowTables($(value));
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
              if (rowClasses.match(/comment-row/) && !$("#show-comments-checkbox").prop("checked")) {
                toggleCommentIcon($(value).attr("data-line-id"), true);
                return; // Dont show comment row if show comments setting is unchecked
              }

              disableCommentsOnInRowTables($(value));
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
    if (headingRow.attr('class')) {
      const headingRowClasses = headingRow.attr('class').split(/\s+/);
      const caretIcon = headingRow.find(".row-fold-caret").children("i");
      const caretDirection = caretIcon.attr("class").split(/\s+/).filter(c => c.startsWith('fa-angle-'))[0];
      const subSectionHeadingClass = headingRowClasses.filter(c => c.startsWith('code-line-section-heading-'))[0];
      const subSectionContentClass = headingRowClasses.filter(c => c.startsWith('code-line-section-content-'))[0];

      if (subSectionHeadingClass) {
        const sectionKey = subSectionHeadingClass.replace("code-line-section-heading-", "")
        const sectionKeyA = headingRowClasses.filter(c => c.startsWith('rev-a-heading-'))[0]?.replace('rev-a-heading-', '');
        const sectionKeyB = headingRowClasses.filter(c => c.startsWith('rev-b-heading-'))[0]?.replace('rev-b-heading-', '');

        if (/^\d+$/.test(sectionKey)) {
          var sectionContent = $(`.code-line-section-content-${sectionKey}`);
          if (sectionContent.hasClass("section-loaded")) {
            toggleSectionContent(headingRow, sectionContent, caretDirection, caretIcon);
          }
          else {
            let uri = '?handler=codelinesection';
            const uriPath = location.pathname.split('/');
            const reviewId = uriPath[uriPath.length - 1];
            const revisionId = new URLSearchParams(location.search).get("revisionId");
            const diffRevisionId = new URLSearchParams(location.search).get("diffRevisionId");
            const diffOnly = new URLSearchParams(location.search).get("diffOnly");
            uri = uri + '&id=' + reviewId + '&sectionKey=' + sectionKey;
            if (revisionId)
              uri = uri + '&revisionId=' + revisionId;
            if (diffRevisionId)
              uri = uri + '&diffRevisionId=' + diffRevisionId;
            if (diffOnly)
              uri = uri + '&diffOnly=' + diffOnly;
            if (sectionKeyA)
              uri = uri + '&sectionKeyA=' + sectionKeyA;
            if (sectionKeyB)
              uri = uri + '&sectionKeyB=' + sectionKeyB;

            const loadingMarkUp = "<td class='spinner-border spinner-border-sm ms-4' role='status'><span class='sr-only'>Loading...</span></td>";
            const failedToLoadMarkUp = "<div class='alert alert-warning alert-dismissible fade show' role='alert'>Failed to load section. Refresh page and try again.</div>";
            if (sectionContent.children(".spinner-border").length == 0) {
              sectionContent.children("td").after(loadingMarkUp);
            }
            sectionContent.removeClass("d-none");

            const request = $.ajax({ url: uri });
            request.done(function (partialViewResult) {
              sectionContent.replaceWith(partialViewResult);
              toggleSectionContent(headingRow, sectionContent, caretDirection, caretIcon);
              addToggleEventHandlers();
            });
            request.fail(function () {
              if (sectionContent.children(".alert").length == 0) {
                sectionContent.children(".spinner-border").replaceWith(failedToLoadMarkUp);
              }
            });
            return request;
          }
        }
      }

      if (subSectionContentClass) {
        const subSectionClass = headingRowClasses.filter(c => c.match(/.*lvl_[0-9]+_parent.*/))[0];
        const lineNumber = headingRow.find(".line-number>span").text();
        if (subSectionClass) {
          const subSectionLevel = subSectionClass.split('_')[1];
          const subSectionHeadingPosition = subSectionClass.split('_')[3];
          if (/^\d+$/.test(subSectionLevel) && /^\d+$/.test(subSectionHeadingPosition)) {
            toggleSubSectionContent(headingRow, subSectionLevel, subSectionHeadingPosition, subSectionContentClass, caretDirection, caretIcon, lineNumber);
          }
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

  /* Disables Comments for tables within codeline rows. Used for code-removed lines in diff */
  function disableCommentsOnInRowTables(row: JQuery<HTMLElement>) {
    if (row.hasClass("code-removed")) {
      const innerTable = row.find(".code-inner>table");
      if (innerTable.length > 0) {
        innerTable.find("tr").removeAttr("data-inline-id");
        innerTable.find(".line-comment-button").remove();
      }
    }
  }

  // Enable SumoSelect
  $(document).ready(function () {
    (<any>$("#revision-select")).SumoSelect({ search: true, searchText: 'Search Revisions...' });
    (<any>$("#diff-select")).SumoSelect({ search: true, searchText: 'Search Revisons for Diff...' });
  });

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
          }
        }
      }
      navItemRow.removeClass("nav-list-collapsed");
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
      $(TOGGLE_DOCUMENTATION).children('svg').removeClass("invisible");
    } else {
      // hide all documentation
      $(".code-line-documentation").addClass("hidden-row");
      $(TOGGLE_DOCUMENTATION).children('i').removeClass("fa-square-minus");
      $(TOGGLE_DOCUMENTATION).children('i').addClass("fa-square-plus");
      $(TOGGLE_DOCUMENTATION).children('svg').addClass("invisible");
    }*/
  });

  $(SHOW_DOC_HREF).on("click", e => {
    $(SHOW_DOC_CHECKBOX)[0].click();
  });

  $(SHOW_HIDDEN_CHECKBOX).on("click", e => {
    updatePageSettings(function() {
      $(SEL_HIDDEN_CLASS).toggleClass("d-none");
    });
  });

  $(SHOW_HIDDEN_HREF).on("click", e => {
      $(SHOW_HIDDEN_CHECKBOX)[0].click();
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
      $(this).children('svg').addClass("invisible");
    }
    else {
      $(this).children('svg').removeClass("invisible");
    }

    // scroll button to center of screen, so that the line is visible after toggling folding
    $(this).get(0).scrollIntoView({ block: "center"});
  });

  /* DROPDOWN FILTER FOR REVIEW, REVISIONS AND DIFF (UPDATES REVIEW PAGE ON CHANGE)
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
  $('#revision-select, #diff-select').each(function(index, value) {
    $(this).on('change', function() {
      var url = $(this).find(":selected").val();
      if (url)
      {
        window.location.href = url as string;
      }
    });
  });
  
  /* BUTTON FOR REQUEST REVIEW (CHANGES BETWEEN REQUEST ALL AND REQUEST SELECTED IN THE REQUEST APPROVAL SECTION)
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
  $('.selectReviewerForRequest').on("click", function () {
    var reviewers = document.getElementsByName('reviewers');
    var button = document.getElementById('submitReviewRequest') as HTMLInputElement | null;
    var anyChecked = false;

    if (button == null) return; // Case to remove null warnings
    button.disabled = false;

    for (var i = 0; i < reviewers.length; i++) {
      var element = reviewers[i] as HTMLInputElement | null;
      if (element?.checked) {
        anyChecked = true;
        button.innerText = "Request Selected";
        button.onclick = function () { document.getElementById("submitRequestForReview")?.click(); };
        break;
      }
    }

    if (!anyChecked) {
      button.innerText = "Request All";
      button.onclick = function () {
        var reviewers = document.getElementsByName('reviewers');
        // Select all, submit
        reviewers.forEach(f => {
          var e = f as HTMLInputElement | null;
          if(e != null) e.checked = true;
          document.getElementById("submitRequestForReview")?.click();
        });
      };
    }
  });

  /* COLLAPSIBLE CODE LINES (EXPAND AND COLLAPSE FEATURE)
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
  addToggleEventHandlers();

  /* RIGHT OFFCANVAS OPERATIONS
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
   // Open / Close right Offcanvas Menu
  $("#review-right-offcanvas-toggle").on('click', function () {
    updatePageSettings(function () {
      rightOffCanvasNavToggle("review-main-container");
    });
  });

  // Toggle Subscribe Switch
  $("#reviewSubscribeSwitch").on('change', function () {
    $("#reviewSubscribeForm").submit();
  });
  // Toggle Close Switch
  $("#reviewCloseSwitch").on('change', function () {
    $("#reviewCloseForm").submit();
  });

  // Manage Expand / Collapse State of options
  [$("#approveCollapse"), $("#requestReviewersCollapse"), $("#reviewOptionsCollapse"), $("#pageSettingsCollapse"), $("#associatedPRCollapse"), $("#associatedReviewsCollapse")].forEach(function (value, index) {
    const id = value.attr("id");
    value.on('hidden.bs.collapse', function () {
      document.cookie = `${id}=hidden; max-age=${7 * 24 * 60 * 60}`;
    });
    value.on('shown.bs.collapse', function () {
      document.cookie = `${id}=shown; max-age=${7 * 24 * 60 * 60}`;
    });
  });
});
