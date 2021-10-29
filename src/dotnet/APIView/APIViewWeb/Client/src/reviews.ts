$(() => {
    const languageSelect = $("#reviews-table-language-filter");
    const searchBox = $("#reviews-table-search-box");
    const groupbyPackage = $("#groupbyPackageRadio");
    const disableRowgroup = $("#groupbyNoneRadio");
    var collapsedGroups = {};

    // Enable data Tables
    var reviewsTable = (<any>$('#reviews-table')).DataTable({
      "responsive": true,
      "order": [
        [6, 'asc']
      ],
      "rowGroup": {
        enable: true,
        dataSrc: 6,
        startRender: function(rows, group) {
          var collapsed = !collapsedGroups[group];
          var arrowClass = "fa-chevron-up";
          if (collapsed) {
            arrowClass = "fa-chevron-down";
          }

          rows.nodes().each(function(r) {
            r.style.display = '';
            if (collapsed) {
              r.style.display = 'none';
            }
          });

          return $('<tr/>')
            .append('<td colspan="6" class="clickable bg-light bg-gradient font-weight-normal"><i class="fas ' + arrowClass + '"></i>&nbsp;&nbsp;' + group + ' (' + rows.count() + ')</td>')
            .attr('data-name', group)
            .toggleClass('collapsed', collapsed);
        }
      },
      "dom": '<"row"t><"row"<"col-3 px-0"i><"col px-0"p>>',
      "columnDefs" : [
        { orderable: false, targets: 5 }, 
        { visible: false, targets: 6 },
        { className: 'dt-body-center', targets: 5 }
      ],
      "pageLength" : 100,
      "search.smart" : true,
      "drawCallback": function (settings){
        // Enable bootstraps tooltip
        (<any>$('[data-toggle="tooltip"]')).tooltip();
      }
    });

    // Row Groups toggling
    $('#reviews-table tbody').on('click', 'tr.dtrg-start', function() {
      var name = $(this).data('name');
      var arrowIcon = $(this).find("td > i");
      arrowIcon.toggleClass('fa-chevron-down');
      arrowIcon.toggleClass('fa-chevron-up');
      collapsedGroups[name] = !collapsedGroups[name];
      reviewsTable.draw(false);
    });

    // Group Rows by Package
    groupbyPackage.on("click", function() {
      collapsedGroups = {};
      reviewsTable.rowGroup().enable().draw();
    });

    // Disable Row Grouping
    disableRowgroup.on("click", function() {
      reviewsTable.rowGroup().disable().draw();
      $('#reviews-table tr').css("display", "");
    });

    // Search
    searchBox.on("input", function (e) {
      reviewsTable.search($(this).val() as string).draw();
    });

    // Filter by Language
    languageSelect.on('change', function(e) {
      reviewsTable.columns(4).search($(this).val() as string, true, false, false).draw();
    });
  }
);


