
import { rightOffCanvasNavToggle } from "../shared/off-canvas";

import * as rvM from "./review.module"
import * as hp from "../shared/helpers";

$(() => {  
  const SHOW_DOC_CHECKBOX = ".show-documentation-checkbox";
  const SHOW_DOC_HREF = ".show-documentation-switch";
  const SHOW_DIFFONLY_CHECKBOX = ".show-diffonly-checkbox";
  const SHOW_DIFFONLY_HREF = ".show-diffonly-switch";
  const TOGGLE_DOCUMENTATION = ".line-toggle-documentation-button";
  const SEL_HIDDEN_CLASS = ".hidden-api-toggleable";
  const SHOW_HIDDEN_CHECKBOX = "#show-hidden-api-checkbox";
  const SHOW_HIDDEN_HREF = ".show-hidden-api";

  rvM.hideCheckboxesIfNotApplicable();

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
        rvM.runAfterExpandingCodeline(targetAnchorId, function () {
          navItemRow.removeClass("nav-list-collapsed");
        });
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
    rvM.splitReviewPageContent();
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
    hp.updatePageSettings(function() {
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
    hp.updatePageSettings(function(){
      $(".line-number").toggleClass("d-none");
    });
  });

  $("#hide-left-navigation").on("click", e => {
    hp.updatePageSettings(function(){
      var leftContainer = $("#review-left");
      var rightContainer = $("#review-right");
      var gutter = $(".gutter-horizontal");

      if (leftContainer.hasClass("d-none")) {
        leftContainer.removeClass("d-none");
        rightContainer.removeClass("col-12");
        rightContainer.addClass("col-10");
        rvM.splitReviewPageContent();
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
  rvM.addCodeLineToggleEventHandlers();

  // Ask to update codeLine Section state after page refresh
  $(document).ready(function () {
    // Get sessionStorage values holding section state
    const shownSectionHeadingLineNumbers = sessionStorage.getItem("shownSectionHeadingLineNumbers");

    if (shownSectionHeadingLineNumbers != null)
    {
      rvM.loadPreviouslyShownSections();
    }
  });

  /* RIGHT OFFCANVAS OPERATIONS
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
   // Open / Close right Offcanvas Menu
  $("#review-right-offcanvas-toggle").on('click', function () {
    hp.updatePageSettings(function () {
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

  /* RIGHT OFFCANVAS OPERATIONS
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
});
