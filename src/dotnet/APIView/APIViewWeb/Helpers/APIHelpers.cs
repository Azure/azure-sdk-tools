using System;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;


namespace APIViewWeb.Helpers
{ 
    public class LeanJsonResult : JsonResult
    {
        private readonly int _statusCode;
        public LeanJsonResult(object value, int statusCode) : base(value)
        {
            _statusCode = statusCode;
{
    public class PageParams
    {
        private const int MaxPageSize = 50;
        public int NoOfItemsRead { get; set; } = 0;
        private int _pageSize = 5;

        public int PageSize { 
            get => _pageSize; 
            set => _pageSize = (value > MaxPageSize) ? MaxPageSize : value;
        }
    }

    public class ReviewFilterAndSortParams
    {
        public string Name { get; set; }
        public string Author { get; set; }
        public IEnumerable<string> Languages { get; set; }
        public IEnumerable<string> Details { get; set; }
        public string SortField { get; set; } = "LastUpdated";
        public int SortOrder { get; set; } = 1;
    }

    public class PagedList<T> : List<T>
    {
        public PagedList(IEnumerable<T> items, int noOfItemsRead, int totalCount,  int pageSize)
        {
            NoOfItemsRead = noOfItemsRead;
            TotalCount = totalCount;
            PageSize = pageSize;
            AddRange(items);
        }
        public int NoOfItemsRead { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }

    }

    public class LeanJsonResult : JsonResult
    {
        public LeanJsonResult(object value) : base(value)
        {
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var response = context.HttpContext.Response;

            response.ContentType = !string.IsNullOrEmpty(ContentType) ? ContentType : "application/json";
            response.StatusCode = _statusCode;

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };

            await JsonSerializer.SerializeAsync(response.Body, Value, options);
        }
    }
    public class PaginationHeader
    { 
        public PaginationHeader(int noOfItemsRead, int pageSize, int totalCount)
        {
            this.NoOfItemsRead = noOfItemsRead;
            this.PageSize = pageSize;
            this.TotalCount = totalCount;
        }

        public int NoOfItemsRead { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }
}
