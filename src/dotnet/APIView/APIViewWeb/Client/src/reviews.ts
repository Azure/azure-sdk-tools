$(() => {
  // Search
  const searchBox = $('#reviews-table-search-box');
  const searchContext = $('.review-name') as any;
  const serviceGroupRows = $('.service-group-row');
  const packageGroupRows = $('.package-group-row');
  const reviewHeaderRows = $('.review-rows-header');

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
    $('.service-group-row').each(function(index, value){
      toggleServiceGroup($(this), "closed");
    });
  });

  // Expand all Groups
  $('#expand-all-groups-btn').on('click', function () {
    $('.service-group-row').each(function(index, value){
      toggleServiceGroup($(this), "closed");
    });
    $('.package-group-row').each(function(index, value){
      togglePackageGroup($(this), "closed");
    });
  });

  // Collapse all Groups
  $('#collapse-all-groups-btn').on('click', function () {
    $('.service-group-row').each(function(index, value){
      toggleServiceGroup($(this), "opened");
    });
    $('.package-group-row').each(function(index, value){
      togglePackageGroup($(this), "opened");
    });
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
  else
  {
    $('.service-group-row').each(function(index, value) {
      $(this)
    });
  }

  searchBox.on('input', function() {
    setTimeout(filterReviews, 300);
  });
});