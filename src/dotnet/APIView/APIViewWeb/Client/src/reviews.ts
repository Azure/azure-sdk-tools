$(() => {
  // Search
  const reviewsFilterPartial = $( '#reviews-list-partial' );
  const pkgNameFilter = $( '#name-filter-bootstraps-select' );
  const languageFilter = $( '#language-filter-bootstraps-select' );
  const tagFilter = $( '#tags-filter-bootstraps-select' );
  const authorFilter = $( '#author-filter-bootstraps-select' );
  const openFilter = $( '#open-filter-check' );
  const closedFilter = $( '#closed-filter-check' );
  const manualFilter = $( '#manual-filter-check' );
  const automaticFilter = $( '#automatic-filter-check' );
  const pullRequestFilter = $( '#pullrequest-filter-check' );
  const approvedFilter = $( '#approved-filter-check' );
  const pendingFilter = $( '#pending-filter-check' );

  const searchBox = $('#reviews-table-search-box');
  const searchContext = $('.review-name') as any;

  // Enable tooltip
  (<any>$('[data-toggle="tooltip"]')).tooltip();

  function updateListedReviews(pageNo=0)
  {
    var uri = "?handler=reviewspartial";

    pkgNameFilter.children(":selected").each(function() {
      uri = uri + '&packageNames=' + encodeURIComponent(`${$(this).val()}`);
    });

    languageFilter.children(":selected").each(function() {
      uri = uri + '&languages=' + encodeURIComponent(`${$(this).val()}`);
    });

    tagFilter.children(":selected").each(function() {
      uri = uri + '&tags=' + encodeURIComponent(`${$(this).val()}`);
    });
    
    authorFilter.children(":selected").each(function() {
      uri = uri + '&authors=' + encodeURIComponent(`${$(this).val()}`);
    });

    uri = uri + '&isOpen=' + openFilter.prop('checked');
    uri = uri + '&isClosed=' + closedFilter.prop('checked');
    uri = uri + '&isManual=' + manualFilter.prop('checked');
    uri = uri + '&isAutomatic=' + automaticFilter.prop('checked');
    uri = uri + '&isPullRequest=' + pullRequestFilter.prop('checked');
    uri = uri + '&isApproved=' + approvedFilter.prop('checked');
    uri = uri + '&isPending=' + pendingFilter.prop('checked');
    uri = uri + '&offset=' + (pageNo * 50); //50 is the default size of a page
    uri = encodeURI(uri)

    $.ajax({
      url: uri
    }).done(function(partialViewResult) {
      reviewsFilterPartial.html(partialViewResult);
      addPaginationEventHandlers(); // This ensures that the event handlers are re-added after ajax refresh
    });
  }

  // Add onclick handler to pagination
  function addPaginationEventHandlers()
  {
    $( '.page-link' ).each(function() {
      $(this).on('click', function(event){
        event.preventDefault();
        var linkParts = $(this).prop('href').split('/');
        var pageNo = linkParts[linkParts.length - 1];
      if (pageNo !== null && pageNo !== undefined)
      {
        updateListedReviews(pageNo);
      }
      });
    });
  }

  function filterReviews(){
    // highlight matching text using mark.js framework and hide rows that don't match
    const searchText = (searchBox.val() as string).toUpperCase();
    searchContext.closest('tr').removeClass('hidden-row').unmark();
    searchContext.mark(searchText, {
      done: function () {
        searchContext.not(':has(mark)').closest('tr').addClass('hidden-row');
      }
    });
  }

  // Update when any dropdown is changed
  [pkgNameFilter, languageFilter, tagFilter, authorFilter].forEach(function(value, index) {
    value.on('hidden.bs.select', function() {
      updateListedReviews();
    });
  });

  // Update when any checkbox is checked
  [openFilter, closedFilter, automaticFilter, manualFilter, pullRequestFilter, approvedFilter, pendingFilter].forEach(function(value, index) {
    value.on('click', function() {
      updateListedReviews();
    });
  });

  addPaginationEventHandlers();

  // If already populated from navigating back, filter again
  if (searchBox.val()) {
    filterReviews();
  }

  searchBox.on('input', function() {
    setTimeout(filterReviews, 300);
  });
});
