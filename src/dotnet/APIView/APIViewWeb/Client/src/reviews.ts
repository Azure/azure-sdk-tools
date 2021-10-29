//$(() => {
//  const searchBox = $("#searchBox");
//  const context = $(".review-name") as any;
//
//  // if already populated from navigating back, filter again
//  if (searchBox.val()) {
//    filter();
//  }
//  
//  searchBox.on("input", function () {
//    setTimeout(filter, 300);
//  });
//
//  function filter() {
//    // highlight matching text using mark.js framework and hide rows that don't match
//    const searchText = (searchBox.val() as string).toUpperCase();
//    context.closest("tr").show().unmark();
//    if (searchText) {
//      context.mark(searchText, {
//        done: function () {
//          context.not(":has(mark)").closest("tr").hide();
//        }
//      });
//    }
//  }
//});

// Add DataTables
$(() => {
    const languageSelect = $("#reviews-table-language-filter");
    const searchBox = $("#reviews-table-search-box");
    var collapsedGroups = {};

    var reviewsTable = (<any>$('#reviews-table')).DataTable({
      "order": [
        [6, 'asc']
      ],
      "rowGroup": {
        dataSrc: 6,
        startRender: function(rows, group) {
          var collapsed = !!collapsedGroups[group];
          var arrowClass = "fa-chevron-up";
          if (!collapsed) {
            arrowClass = "fa-chevron-down";
          }

          rows.nodes().each(function(r) {
            r.style.display = 'none';
            if (collapsed) {
              r.style.display = '';
            }
          });

          return $('<tr/>')
            .append('<td colspan="6" class="clickable"><i class="fas ' + arrowClass + '"></i>&nbsp;' + group + ' (' + rows.count() + ')</td>')
            .attr('data-name', group)
            .toggleClass('collapsed', collapsed);
        }
      },
      "dom": 't<"pl-2 pr-2"ipr>',
      "columnDefs" : [
        { orderable: false, targets: 5 },
        { visible: false, targets: 6 }
      ],
      "pageLength" : 100,
      "search.smart" : true,
      "drawCallback": function (settings){
        // Enable bootstraps tooltip
        (<any>$('[data-toggle="tooltip"]')).tooltip();
      }
    });

    $('#reviews-table tbody').on('click', 'tr.dtrg-start', function() {
      var name = $(this).data('name');
      var arrowIcon = $(this).find("td > i");
      arrowIcon.toggleClass('fa-chevron-down');
      arrowIcon.toggleClass('fa-chevron-up');
      collapsedGroups[name] = !collapsedGroups[name];
      reviewsTable.draw(false);
    });

    searchBox.on("input", function (e) {
      reviewsTable.search($(this).val() as string).draw();
    });

    languageSelect.on('change', function(e) {
      reviewsTable.columns(4).search($(this).val() as string, true, false, false).draw();
    });
  }
);


