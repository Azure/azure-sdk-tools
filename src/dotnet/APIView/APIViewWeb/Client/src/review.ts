import Split from "split.js";

$(() => {  
  const SEL_DOC_CLASS = ".documentation";
  const SHOW_DOC_CHECK_COMPONENT = "#show-documentation-component";
  const SHOW_DOC_CHECKBOX = ".show-doc-checkbox";
  const SHOW_DOC_HREF = ".show-document";
  const SHOW_DIFFONLY_CHECKBOX = ".show-diffonly-checkbox";
  const SHOW_DIFFONLY_HREF = ".show-diffonly";
  const HIDE_LINE_NUMBERS = "#hide-line-numbers";
  const HIDE_LEFT_NAVIGATION = "#hide-left-navigation";

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

  // Updated Page Setting by Updating UserPreference 
  function updatePageSettings(callBack) {
    var hideLineNumbers = $(HIDE_LINE_NUMBERS).prop("checked");
    var hideLeftNavigation = $(HIDE_LEFT_NAVIGATION).prop("checked");
    var uri = `?handler=updatepagesettings&hideLineNumbers=${hideLineNumbers}&hideLeftNavigation=${hideLeftNavigation}`;

    $.ajax({
      type: "GET",
      url: uri
    }).done(callBack());
  }

  /* ADD EVENT LISTENER FOR TOGGLING LEFT NAVIGATION
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
  addEventListener("load", () => {
    $(".nav-list-toggle").click(function () {
      $(this).parents(".nav-list-group").first().toggleClass("nav-list-collapsed");
    });
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

  $(HIDE_LINE_NUMBERS).on("click", e => {
    updatePageSettings(function(){
      $(".line-number").toggleClass("d-none");
    });
  });

  $(HIDE_LEFT_NAVIGATION).on("click", e => {
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
  $('.row-fold-elipsis, .row-fold-caret').on('click', function () {
    var triggeringClass = $(this)[0].className;
    var headingRow = $(this).parents('.code-line');
    var headingRowClasses = headingRow.attr('class');
    var caretIcon = headingRow.find(".row-fold-caret").children("i");
    var caretClasses = caretIcon.attr("class");
    var caretDirection = caretClasses ? caretClasses.split(' ').filter(c => c.startsWith('fa-angle-'))[0] : "";
    var foldableClassPrefix = headingRowClasses ? headingRowClasses.split(' ').filter(c => c.endsWith('-heading'))[0].replace("-heading", "") : "";

    if (triggeringClass == "row-fold-caret" && caretDirection == "fa-angle-down") {
      var classesOfRowsToHide = [`${foldableClassPrefix}-content`];

      do
      {
        let rowToHide = classesOfRowsToHide.pop();
        if (rowToHide) {
          $(`.${rowToHide}`).each(function (index, value) {
            let contentRowClasses = $(this).attr("class");
            if (contentRowClasses) {
              let subHeadingRows = contentRowClasses.split(' ').filter(c => c.endsWith('-heading'));
              subHeadingRows.forEach(item => {
                classesOfRowsToHide.push(item.replace("-heading", "-content"));
              });
            }
            $(this).addClass("d-none");
          });
          let subHeadingClass = rowToHide.replace("-content", "-heading");
          let subHeadingRow = $(`.${subHeadingClass}`);
          let subHeadingCaretIcon = subHeadingRow.find(".row-fold-caret").children("i");
          let subHeadingCaretClasses = subHeadingCaretIcon.attr("class");
          let caretDirection = subHeadingCaretClasses ? subHeadingCaretClasses.split(' ').filter(c => c.startsWith('fa-angle-'))[0] : "";
          if (caretDirection) {
            subHeadingCaretIcon.removeClass("fa-angle-down");
            subHeadingCaretIcon.addClass("fa-angle-right");
          }
          // toggle ellipsis and border-top
          subHeadingRow.find(".row-fold-elipsis").removeClass("d-none");
          subHeadingRow.children(".code").removeClass("border-top");
        }
      }
      while (classesOfRowsToHide.length > 0);
    }
    else {
      $(`.${foldableClassPrefix}-content`).toggleClass("d-none");
      // toggle ellipsis and border-top
      headingRow.find(".row-fold-elipsis").toggleClass("d-none");
      headingRow.children(".code").toggleClass("border-top");

      // Toggle caret direction
      if (caretDirection.endsWith("right")) {
        caretIcon.removeClass("fa-angle-right");
        caretIcon.addClass("fa-angle-down");
      }
      else {
        caretIcon.removeClass("fa-angle-down");
        caretIcon.addClass("fa-angle-right");
      }
    }
  });

  /* ENABLE TOOLTIP AND POPOVER
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/
  (<any>$('[data-toggle="tooltip"]')).tooltip();
  (<any>$('[data-toggle="popover"]')).popover();
});
