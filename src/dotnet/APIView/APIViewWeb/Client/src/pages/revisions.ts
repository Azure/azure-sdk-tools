import * as hp from "../shared/helpers";

$(() => {
  const apiRevisionsSearchBox = $("#apiRevisions-search");
  const apiRevisionsSearchContext = $(".apiRevisions-list-container .card-body");
  const apiRevisionTypeFilter = ["#manual-apirevisions-check", "#automatic-apirevisions-check", "#pullrequest-apirevisions-check"];

  $(document).on("click", ".revision-rename-icon", e => {
    toggleNameField($(e.target));
  });

  $(document).on("click", ".cancel-revision-rename", e => {
    var icon = $(e.target).parent().siblings(".revision-rename-icon");
    toggleNameField(icon);
  });

  $(document).on("click", ".submit-revision-rename", e => {
    $(e.target).parents(".revision-rename-form").submit();
  });

  function toggleNameField(renameIcon: JQuery) {
    renameIcon.toggle();
    renameIcon.siblings(".revision-name-input").toggle();
  }

  function makeActiveAPIRevisionEventHandler(event) {
    const trigger = $(event.target);
    const activeCard = $(".apiRevisions-list-container .bi.bi-clock-history").closest(".card");
    activeCard.find(".bi.bi-clock-history").remove();
    activeCard.find(".btn-group-vertical").append(`<button type="button" class="btn btn-sm btn-outline-primary make-active">Make Active</button>`);
    activeCard.find(".btn-group-vertical").append(`<button type="button" class="btn btn-sm btn-outline-primary make-diff">Make Diff</button>`);

    activeCard.find(".btn-group-vertical .make-active").on("click", makeActiveAPIRevisionEventHandler);
    activeCard.find(".btn-group-vertical .make-diff").on("click", makeDiffAPIRevisionEventHandler);

    trigger.closest(".card").children(".apirevision-indicator-checks").append(`<i class="bi bi-clock-history mr-1"></i>`);
    trigger.siblings(".make-diff").remove();
    trigger.remove();
  }

  function makeDiffAPIRevisionEventHandler(event) {
    const trigger = $(event.target);
    const diffCard = $(".apiRevisions-list-container .bi.bi-file-diff").closest(".card");
    diffCard.find(".bi.bi-file-diff").remove();
    diffCard.find(".btn-group-vertical").append(`<button type="button" class="btn btn-sm btn-outline-primary make-active">Make Active</button>`);
    diffCard.find(".btn-group-vertical").append(`<button type="button" class="btn btn-sm btn-outline-primary make-diff">Make Diff</button>`);

    diffCard.find(".btn-group-vertical .make-active").on("click", makeActiveAPIRevisionEventHandler);
    diffCard.find(".btn-group-vertical .make-diff").on("click", makeDiffAPIRevisionEventHandler);

    trigger.closest(".card").children(".apirevision-indicator-checks").append(`<i class="bi bi-file-diff mr-1"></i>`);
    trigger.siblings(".make-active").remove();
    trigger.remove();
  }
  function clearDiffAPIRevisionEventHandler(event) {
    const trigger = $(event.target);
    const diffCard = $(".apiRevisions-list-container .bi.bi-file-diff").closest(".card");
    diffCard.find(".bi.bi-file-diff").remove();
    trigger.closest(".btn-group-vertical").append(`<button type="button" class="btn btn-sm btn-outline-primary make-active">Make Active</button>`);
    trigger.closest(".btn-group-vertical").append(`<button type="button" class="btn btn-sm btn-outline-primary make-diff">Make Diff</button>`);

    diffCard.find(".btn-group-vertical .make-active").on("click", makeActiveAPIRevisionEventHandler);
    diffCard.find(".btn-group-vertical .make-diff").on("click", makeDiffAPIRevisionEventHandler);

    trigger.remove();
  }


  /* MANAGE APIREVISIONS IN CONTEXT OF REVIEW PAGE
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/

  // Toggle active class for left side offcanvas buttons
  ["#apiRevisions-context", "#add-apirevision-context"].forEach(function (value, index) {
    $(value).on("shown.bs.offcanvas", function () {
      $("#left-review-offcanvas-menu-content").find('[data-bs-original-title="API"]').removeClass("active");
      $("#left-review-offcanvas-menu-content").find('[data-bs-target="#apiRevisions-context"]').addClass("active");
    })

    $(value).on("hidden.bs.offcanvas", function (event) {
      $("#left-review-offcanvas-menu-content").find('[data-bs-original-title="API"]').addClass("active");
      $("#left-review-offcanvas-menu-content").find('[data-bs-target="#apiRevisions-context"]').removeClass("active");
      event.stopPropagation();
    })

    if (value == "#apiRevisions-context") {
      $(value).on("hide.bs.offcanvas", function (event) {
        const activeRevisionId = $(".apiRevisions-list-container .bi.bi-clock-history").closest(".card").attr("data-id");
        let diffRevisionId = "";
        const diffIcon = $(".apiRevisions-list-container .bi.bi-file-diff");
        if (diffIcon.length > 0) {
          diffRevisionId = diffIcon.closest(".card").attr("data-id")!;
        }

        const url = new URL(window.location.href);
        url.searchParams.set("revisionId", activeRevisionId!);
        if (diffRevisionId) {
          url.searchParams.set("diffRevisionId", diffRevisionId!);
        }
        else {
          url.searchParams.delete("diffRevisionId");
        }
        if (window.location.href != url.href) {
          window.location.href = url.href;
        }
      });
    }
  });

  $("#conversiations-context").on("hidden.bs.offcanvas", function () {
    $("#left-review-offcanvas-menu-content").find('[data-bs-original-title="API"]').addClass("active");
    $("#left-review-offcanvas-menu-content").find('[data-bs-target="#conversiations-context"]').removeClass("active");
  });

  $("#conversiations-context").on("shown.bs.offcanvas", function () {
    $("#left-review-offcanvas-menu-content").find('[data-bs-original-title="API"]').removeClass("active");
    $("#left-review-offcanvas-menu-content").find('[data-bs-target="#conversiations-context"]').addClass("active");
  });

  // Search / Filter APIRevisions
  apiRevisionsSearchBox.on("input", function () {
    apiRevisionTypeFilter.forEach(function (value, index) {
      $(value).removeAttr('checked');
    });
    const searchText = (apiRevisionsSearchBox.val() as string).toUpperCase();
    (<any>apiRevisionsSearchContext.closest(".card").show()).unmark();

    if (searchText) {
      (<any>apiRevisionsSearchContext).mark(searchText, {
        done: function () {
          apiRevisionsSearchContext.not(":has(mark)").closest(".card").hide();
        }
      })
    }
  });

  // Filter by APIRevision Type
  apiRevisionTypeFilter.forEach(function (value, index) {
    $(value).on("change", function (event) {
      apiRevisionsSearchBox.val('');
      const manualChecked = $(apiRevisionTypeFilter[0]).is(":checked");
      const autoChecked = $(apiRevisionTypeFilter[1]).is(":checked");
      const prChecked = $(apiRevisionTypeFilter[2]).is(":checked");

      if ((manualChecked && autoChecked && prChecked) || (!manualChecked && !autoChecked && !prChecked)) {
        $(".apiRevisions-list-container .card").show();
      }
      else {
        $(".apiRevisions-list-container .card-subtitle").each(function (index, element) {
          if ((manualChecked && element.innerText.includes("Type: Manual")) ||
            (autoChecked && element.innerText.includes("Type: Automatic")) ||
            (prChecked && element.innerText.includes("Type: PullRequest ")) ||
            (manualChecked && autoChecked && prChecked)) {
            $(element).closest(".card").show();
          }
          else {
            $(element).closest(".card").hide();
          }
        });
      }
    });
  });

  // Set Active or Diff APIRevision
  $(".apiRevisions-list-container .card .btn.make-active").on("click", makeActiveAPIRevisionEventHandler);
  $(".apiRevisions-list-container .card .btn.make-diff").on("click", makeDiffAPIRevisionEventHandler);
  $(".apiRevisions-list-container .card .btn.clear-diff").on("click", clearDiffAPIRevisionEventHandler);
});
