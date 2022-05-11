$(() => {
  // Search
  const defaultPageSize = 50;
  const reviewsFilterPartial = $( '#reviews-filter-partial' );
  const languageFilter = $( '#language-filter-bootstraps-select' );
  const tagFilter = $( '#tags-filter-bootstraps-select' );
  const stateFilter = $( '#state-filter-bootstraps-select' );
  const statusFilter = $( '#status-filter-bootstraps-select' );
  const typeFilter = $( '#type-filter-bootstraps-select' );

  const searchBox = $('#reviews-table-search-box');
  const searchContext = $('.review-name') as any;

  // Enable tooltip
  (<any>$('[data-toggle="tooltip"]')).tooltip();

  // Computes the uri string using the values of search, pagination and various filters
  // Invokes partial page update to list of reviews using ajax
  // Updates the uri displayed on the client
  function updateListedReviews({ pageNo = 1, pageSize = defaultPageSize, sortField = "Name", search = "" } = {})
  {
    var uri = '?handler=reviewspartial';

    if (search != null && search.trim() != '')
    {
      var searchTerms = search.trim().split(/\s+/);
      searchTerms.forEach(function(value, index){
        uri = uri + '&search=' + encodeURIComponent(value);
      });
    }

    languageFilter.children(":selected").each(function() {
      uri = uri + '&languages=' + encodeURIComponent(`${$(this).val()}`);
    });

    tagFilter.children(":selected").each(function() {
      uri = uri + '&tags=' + encodeURIComponent(`${$(this).val()}`);
    });
    
    stateFilter.children(":selected").each(function() {
      uri = uri + '&state=' + encodeURIComponent(`${$(this).val()}`);
    });

    statusFilter.children(":selected").each(function() {
      uri = uri + '&status=' + encodeURIComponent(`${$(this).val()}`);
    });

    typeFilter.children(":selected").each(function() {
      uri = uri + '&type=' + encodeURIComponent(`${$(this).val()}`);
    });

    uri = uri + '&pageNo=' + encodeURIComponent(pageNo);
    uri = uri + '&pageSize=' + encodeURIComponent(pageSize);
    uri = uri + '&sortField=' + encodeURIComponent(sortField);
    uri = encodeURI(uri);

    $.ajax({
      url: uri
    }).done(function(partialViewResult) {
      reviewsFilterPartial.html(partialViewResult);
      history.pushState({}, '', uri.replace('handler=reviewspartial&', ''));
      addPaginationEventHandlers(); // This ensures that the event handlers are re-added after ajax refresh
    });
  }

  // Add custom behaviour and event to pagination buttons
  function addPaginationEventHandlers()
  {
    $( '.page-link' ).each(function() {
      $(this).on('click', function(event){
        event.preventDefault();
        var linkParts = $(this).prop('href').split('/');
        var pageNo = linkParts[linkParts.length - 1];
        if (pageNo !== null && pageNo !== undefined)
        {
          updateListedReviews({ pageNo: pageNo });
        }
      });
    });
  }

  // Triggers partial page update to retriev properties for poulating filter dropdowns
  function updateFilterDropDown(filter, query)
  {
    // update tags dropdown select
    var uri = `?handler=reviews${query}`;
    var urlParams = new URLSearchParams(location.search);
    if (urlParams.has(query))
    {
      urlParams.getAll(query).forEach(function(value, index) {
        uri = uri + `&selected${query}=` + encodeURIComponent(value);
      });
    }
    $.ajax({
      url: uri
    }).done(function(partialViewResult) {
      filter.html(partialViewResult);
      (<any>filter).selectpicker('refresh');
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

  // Update content of dropdown on page load
  $(document).on('ready', function() {
    updateFilterDropDown(tagFilter, "tags");
    updateFilterDropDown(languageFilter, "languages");
    addPaginationEventHandlers();
  });


  // Update when any dropdown is changed
  [languageFilter, tagFilter, stateFilter, statusFilter, typeFilter].forEach(function(value, index) {
    value.on('hidden.bs.select', function() {
      updateListedReviews();
    });
  });

  // If already populated from navigating back, filter again
  if (searchBox.val()) {
    filterReviews();
  }

  searchBox.on('keypress', function(e) {
    if (e.key == "Enter" && searchBox.val() != null)
    {
      updateListedReviews({ search : searchBox.val() as string });
    }
  });
});
