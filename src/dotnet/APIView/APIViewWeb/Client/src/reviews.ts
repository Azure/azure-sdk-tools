$(() => {
  // Search
  const languageSelect = $("#reviews-table-language-filter");
  const searchBox = $('#reviews-table-search-box');
  const searchContext = $('.review-name') as any;
  const serviceGroupRows = $('.service-group-row');
  const packageGroupRows = $('.package-group-row');
  const reviewHeaderRows = $('.review-rows-header');
  const packageDataRows = $('.package-data-row');

  // Enable tooltip
  (<any>$('[data-toggle="tooltip"]')).tooltip();

  function toggleServiceGroup(serviceRow, state) {
    var serviceGroupTag = serviceRow[0].id;
    var serviceRowIcon = serviceRow.find('i').first();
    var packageGroupRow = $(`.${serviceGroupTag}`);
    if (state == "closed")
    {
      packageGroupRow.removeClass('hidden-row');
      serviceRowIcon.removeClass('fa-angle-right').addClass('fa-angle-down');
      serviceRow.addClass('shadow-sm');
    }
    else
    {
      packageGroupRow.addClass('hidden-row');
      serviceRowIcon.removeClass('fa-angle-down').addClass('fa-angle-right');
      serviceRow.removeClass('shadow-sm');
    }
  }

  function togglePackageGroup(packageRow, state) {
    var packageGroupTag = packageRow[0].id;
    var packageRowIcon = packageRow.find('i').first();
    var packageDataRows = $(`.${packageGroupTag}`);
    if (state == "closed")
    {
      packageDataRows.removeClass('hidden-row');
      packageRowIcon.removeClass('fa-angle-right').addClass('fa-angle-down');
      packageRow.addClass('shadow-sm');
    }
    else
    {
      packageDataRows.addClass('hidden-row');
      packageRowIcon.removeClass('fa-angle-down').addClass('fa-angle-right');
      packageRow.removeClass('shadow-sm');
    }
  }

  function filterReviews(){
    // highlight matching text using mark.js framework and hide rows that don't match
    const searchText = (searchBox.val() as string).toUpperCase();
    searchContext.closest('tr').removeClass('hidden-row').unmark();
    if(searchText)
    {
      searchContext.mark(searchText, {
        done: function () {
          searchContext.not(':has(mark)').closest('tr').addClass('hidden-row');
            serviceGroupRows.addClass('hidden-row');
            packageGroupRows.addClass('hidden-row');
            reviewHeaderRows.addClass('hidden-row');
        }
      });
    }
    else
    {
      serviceGroupRows.removeClass('hidden-row').addClass('shadow-sm');
      serviceGroupRows.find('i').removeClass('fa-angle-right').addClass('fa-angle-down');
      packageGroupRows.removeClass('hidden-row').addClass('shadow-sm');
      packageGroupRows.find('i').removeClass('fa-angle-right').addClass('fa-angle-down');
      reviewHeaderRows.removeClass('hidden-row').addClass('shadow-sm');
    }
  }

    // Expand all Service Groups
  $('#expand-all-service-groups-btn').on('click', function () {
    if (!(searchBox.val() as string))
    {
      serviceGroupRows.each(function(index, value){
        toggleServiceGroup($(this), "closed");
      });
    }
  });

  // Expand all Groups
  $('#expand-all-groups-btn').on('click', function () {
    if (!(searchBox.val() as string))
    {
      serviceGroupRows.each(function(index, value){
        toggleServiceGroup($(this), "closed");
      });
      packageGroupRows.each(function(index, value){
        togglePackageGroup($(this), "closed");
      });
    }
  });

  // Collapse all Groups
  $('#collapse-all-groups-btn').on('click', function () {
    if (!(searchBox.val() as string))
    {
      serviceGroupRows.each(function(index, value){
        toggleServiceGroup($(this), "opened");
      });
      packageGroupRows.each(function(index, value){
        togglePackageGroup($(this), "opened");
      });
    }
  });

  // Clear all filters
  $('#clear-all-filters').on('click', function() {
    if (languageSelect.val() != "")
    {
      languageSelect.val("").trigger('change');
    }
    if (searchBox.val() != "")
    {
      searchBox.val("").trigger('input');
    }
  });

  // Toggle individual service Group
  $('#reviews-table tbody').on('click', '.service-group-row', function() {
    var serviceRowIcon = $(this).find('i').first();
    var serviceGroupID = $(this).first()[0].id;
    if (serviceRowIcon.hasClass('fa-angle-right'))
    {
      toggleServiceGroup($(this), "closed");
    }
    else 
    {
      $(`.package-group-row.${serviceGroupID}`).each(function(index, value){
        var packageRowIcon = $(this).find('i').first();
        if (packageRowIcon.hasClass(`fa-angle-down`))
        {
          togglePackageGroup($(this), "opened");
        }
      });
      toggleServiceGroup($(this), "opened");
    }
  });

  // Toggle individual package Group
  $('#reviews-table tbody').on('click', '.package-group-row', function() {
    var packageRowIcon = $(this).find('i').first();
    if (packageRowIcon.hasClass('fa-angle-right'))
    {
      togglePackageGroup($(this), "closed");
    }
    else 
    {
      togglePackageGroup($(this), "opened");
    }
  });

  // If already populated from navigating back, filter again
  if (searchBox.val()) {
    filterReviews();
  }

  searchBox.on('input', function() {
    setTimeout(filterReviews, 300);
  });

  // Filter by language
  languageSelect.on('change', function(e) {
    var filterText = $(this).val() as string;
    if (filterText == "")
    {
      packageDataRows.removeClass('hidden-row-via-filter');
    }
    else 
    {
      packageDataRows.each(function (index, value) {
        let langImageAlt = value.children[0].children[0].getAttribute("alt");
        if (langImageAlt != null && langImageAlt.match(RegExp(filterText)))
        {
          $(this).removeClass('hidden-row-via-filter');
        }
        else
        {
          $(this).addClass('hidden-row-via-filter');
        }
      });
    }
  });
});
