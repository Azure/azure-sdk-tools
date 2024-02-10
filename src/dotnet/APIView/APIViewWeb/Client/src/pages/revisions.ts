import * as hp from "../shared/helpers";

$(() => {
  const apiRevisionsSearchBox = $("#apiRevisions-search");
  const samplesRevisionsSearchBox = $("#samplesRevisions-search");
  const apiRevisionsSearchContext = $(".api-revisions.revisions-list-container .card-body");
  const samplesRevisionsSearchContext = $(".samples-revisions.revisions-list-container .card-body");
  const apiRevisionTypeFilter = ["#manual-apirevisions-check", "#automatic-apirevisions-check", "#pullrequest-apirevisions-check"];

  function toggleNameField(renameIcon: JQuery) {
    renameIcon.toggle();
    renameIcon.siblings(".revision-name-input").toggle();
  }

  function makeActiveAPIRevisionEventHandler(event) {
    const trigger = $(event.currentTarget);
    const activeCard = $(".revisions-list-container .bi.bi-clock-history.active-rev").closest(".card");
    activeCard.find(".bi.bi-clock-history.active-rev").remove();
    activeCard.find(".btn-group").prepend(`<button type="button" class="btn btn-sm btn-outline-primary make-diff" data-bs-toggle="tooltip" title="Make Diff"><i class="bi bi-file-diff mr-1"></i></button>`);
    activeCard.find(".btn-group").prepend(`<button type="button" class="btn btn-sm btn-outline-primary make-active" data-bs-toggle="tooltip" title="Make Active"><i class="bi bi-clock-history mr-1"></i></button>`);

    activeCard.find(".btn-group .make-active").on("click", makeActiveAPIRevisionEventHandler);
    activeCard.find(".btn-group .make-diff").on("click", makeDiffAPIRevisionEventHandler);

    trigger.closest(".card").children(".revision-indicator-checks").append(`<i class="bi bi-clock-history active-rev mr-1"></i>`);
    trigger.siblings(".make-diff").remove();
    trigger.remove();
    $(".revisions-list-container").addClass("revisions-changed");
    $(".tooltip").remove();
  }

  function makeDiffAPIRevisionEventHandler(event) {
    const trigger = $(event.currentTarget);
    const diffCard = $(".revisions-list-container .bi.bi-file-dif.diff-rev").closest(".card");
    diffCard.find(".bi.bi-file-diff.diff-rev").remove();
    diffCard.find(".btn-group").prepend(`<button type="button" class="btn btn-sm btn-outline-primary make-diff" data-bs-toggle="tooltip" title="Make Diff"><i class="bi bi-file-diff mr-1"></i></button>`);
    diffCard.find(".btn-group").prepend(`<button type="button" class="btn btn-sm btn-outline-primary make-active" data-bs-toggle="tooltip" title="Make Active"><i class="bi bi-clock-history mr-1"></i></button>`);

    diffCard.find(".btn-group .make-active").on("click", makeActiveAPIRevisionEventHandler);
    diffCard.find(".btn-group .make-diff").on("click", makeDiffAPIRevisionEventHandler);

    trigger.closest(".card").children(".revision-indicator-checks").append(`<i class="bi bi-file-diff diff-rev mr-1"></i>`);
    trigger.siblings(".make-active").remove();
    trigger.remove();
    $(".revisions-list-container").addClass("revisions-changed");
    $(".tooltip").remove();
  }

  function clearDiffAPIRevisionEventHandler(event) {
    const trigger = $(event.currentTarget);
    const diffCard = $(".revisions-list-container .bi.bi-file-diff.diff-rev").closest(".card");
    diffCard.find(".bi.bi-file-diff.diff-rev").remove();
    trigger.closest(".btn-group").prepend(`<button type="button" class="btn btn-sm btn-outline-primary make-diff" data-bs-toggle="tooltip" title="Make Diff"><i class="bi bi-file-diff mr-1"></i></button>`);
    trigger.closest(".btn-group").prepend(`<button type="button" class="btn btn-sm btn-outline-primary make-active" data-bs-toggle="tooltip" title="Make Active"><i class="bi bi-clock-history mr-1"></i></button>`);

    diffCard.find(".btn-group .make-active").on("click", makeActiveAPIRevisionEventHandler);
    diffCard.find(".btn-group .make-diff").on("click", makeDiffAPIRevisionEventHandler);
    trigger.remove();
    $(".revisions-list-container").addClass("revisions-changed");
    $(".tooltip").remove();
  }

  function exitAPIRevisionRename(apiRevisionCard) {
    apiRevisionCard.find(".card-title").removeClass("d-none");
    apiRevisionCard.find(".card-subtitle").removeClass("d-none");
    apiRevisionCard.find(".edit-revision-label").addClass("d-none");
  }

  function searchRevisions(searchBox, searchContext) {
    const searchText = (searchBox.val() as string).toUpperCase();
    (<any>searchContext.closest(".card").show()).unmark();

    if (searchText) {
      (<any>searchContext).mark(searchText, {
        done: function () {
          searchContext.not(":has(mark)").closest(".card").hide();
        }
      })
    }
  }


  /* MANAGE APIREVISIONS IN CONTEXT OF REVIEW PAGE
  --------------------------------------------------------------------------------------------------------------------------------------------------------*/

  // Toggle active class for left side offcanvas buttons
  ["#apiRevisions-context", "#add-apirevision-context"].forEach(function (value, index) {
    $(value).on("shown.bs.offcanvas", function () {
      $("#left-review-offcanvas-menu-content").find('[data-bs-original-title="API"]').removeClass("active");
      $("#left-review-offcanvas-menu-content").find('[data-bs-target="#apiRevisions-context"]').addClass("active");

      if (value == "#apiRevisions-context") {
        $(".revisions-list-container").removeClass("revisions-changed");
      }
    })

    $(value).on("hidden.bs.offcanvas", function (event) {
      $("#left-review-offcanvas-menu-content").find('[data-bs-original-title="API"]').addClass("active");
      $("#left-review-offcanvas-menu-content").find('[data-bs-target="#apiRevisions-context"]').removeClass("active");
      event.stopPropagation();
    })

    if (value == "#apiRevisions-context") {
      $(value).on("hide.bs.offcanvas", function (event) {
        const activeRevisionId = $(".revisions-list-container .bi.bi-clock-history.active-rev").closest(".card").attr("data-id");
        let diffRevisionId = "";
        const diffIcon = $(".revisions-list-container .bi.bi-file-diff.diff-rev");
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

        if ($(".revisions-list-container").hasClass("revisions-changed")) {
          if (window.location.href != url.href) {
            window.location.href = url.href;
          }
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
    searchRevisions(apiRevisionsSearchBox, apiRevisionsSearchContext);
  });

  samplesRevisionsSearchBox.on("input", function () {
    searchRevisions(samplesRevisionsSearchBox, samplesRevisionsSearchContext);
  });

  // Filter by APIRevision Type
  apiRevisionTypeFilter.forEach(function (value, index) {
    $(value).on("change", function (event) {
      apiRevisionsSearchBox.val('');
      const manualChecked = $(apiRevisionTypeFilter[0]).is(":checked");
      const autoChecked = $(apiRevisionTypeFilter[1]).is(":checked");
      const prChecked = $(apiRevisionTypeFilter[2]).is(":checked");

      if ((manualChecked && autoChecked && prChecked) || (!manualChecked && !autoChecked && !prChecked)) {
        $(".api-revisions.revisions-list-container .card").show();
      }
      else {
        $(".api-revisions.revisions-list-container .card-subtitle").each(function (index, element) {
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
  $(".revisions-list-container .card .btn.make-active").on("click", makeActiveAPIRevisionEventHandler);
  $(".revisions-list-container .card .btn.make-diff").on("click", makeDiffAPIRevisionEventHandler);
  $(".revisions-list-container .card .btn.clear-diff").on("click", clearDiffAPIRevisionEventHandler);

  // Delete API Revision
  $(".revisions-list-container .delete").on("click", function (e) {
    e.stopPropagation();
    const id = hp.getReviewAndRevisionIdFromUrl(window.location.href)["reviewId"];
    const apiRevisionCard = $(this).closest(".card");
    const apiRevisionsId = apiRevisionCard.attr("data-id");
    const url = `/Assemblies/Revisions/${id}/${apiRevisionsId}`;
    var antiForgeryToken = $("input[name=__RequestVerificationToken]").val();

    $.ajax({
      type: "DELETE",
      url: url,
      headers: {
        "RequestVerificationToken": antiForgeryToken as string
      },
      success: function (data) {
        apiRevisionCard.remove();
      }
    });
  });

  // Rename API Revision Label
  $(".revisions-list-container .rename").on("click", function (e) {
    e.stopPropagation();
    const apiRevisionCard = $(this).closest(".card");
    apiRevisionCard.find(".card-title").addClass("d-none");
    apiRevisionCard.find(".card-subtitle").addClass("d-none");
    apiRevisionCard.find(".edit-revision-label").removeClass("d-none");
  });

  $(".revisions-list-container .cancel-rename").on("click", function (e) {
    e.stopPropagation();
    const apiRevisionCard = $(this).closest(".card");
    exitAPIRevisionRename(apiRevisionCard);
  });

  $(".revisions-list-container .enter-rename").on("click", function (e) {
    e.stopPropagation();
    const id = hp.getReviewAndRevisionIdFromUrl(window.location.href)["reviewId"];
    const apiRevisionCard = $(this).closest(".card");
    const updatedLabel = apiRevisionCard.find(".edit-revision-label > input").val();
    const apiRevisionsId = apiRevisionCard.attr("data-id");
    const url = `/Assemblies/Revisions/${id}/${apiRevisionsId}?handler=Rename&newLabel=${updatedLabel}`;
    var antiForgeryToken = $("input[name=__RequestVerificationToken]").val();
  
    $.ajax({
      type: "POST",
      url: url,
      headers: {
        "RequestVerificationToken": antiForgeryToken as string
      },
      success: function (data) {
        apiRevisionCard.find(".card-title").text(data);
        exitAPIRevisionRename(apiRevisionCard);
      }
    });
  });

  $(".edit-revision-label").on("click", function (e) {
    e.stopPropagation();
  });

  // Open API Revision in new tab
  $("#revisions-main-container .revisions-list-container .card").on("click", function () {
    const apiRevisionsId = $(this).attr("data-id");
    const revisionContainer = $(this).closest(".revisions-list-container");

    if (revisionContainer.hasClass("api-revisions")) {
      const uri = (window.location.href).replace("/Revisions/", "/Review/") + `?revisionId=${apiRevisionsId}`;
      window.open(uri, "_blank");
    }

    if (revisionContainer.hasClass("samples-revisions")) {
      const uri = (window.location.href).replace("/Revisions/", "/Samples/") + `?revisionId=${apiRevisionsId}`;
      window.open(uri, "_blank");
    }
  });
});
