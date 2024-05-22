using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace APIViewWeb.Helpers
{
    public class PageParams
    {
        private const int MaxPageSize = 50;
        public int NoOfItemsRead { get; set; } = 0;
        private int _pageSize = 5;

        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = (value > MaxPageSize) ? MaxPageSize : value;
        }
    }

    public class ReviewFilterAndSortParams
    {
        public string Name { get; set; }
        public IEnumerable<string> Languages { get; set; }
        public string SortField { get; set; } = "LastUpdatedOn";
        public int SortOrder { get; set; } = 1;
        public bool? IsApproved { get; set; }
    }

    public class APIRevisionsFilterAndSortParams : ReviewFilterAndSortParams
    {
        public bool IsDeleted { get; set; }
        public bool AssignedToMe { get; set; }
        public string Label { get; set; }
        public string Author { get; set; }
        public string ReviewId { get; set; }
        public IEnumerable<string> Details { get; set; }
    }

    public class APIRevisionSoftDeleteParam
    {
        public string reviewId { get; set; }
        public IEnumerable<string> apiRevisionIds { get; set;}
    }

    public class ReviewCreationParam                                                                    
    {
        public IFormFile File { get; set; }
        public string Language { get; set; }
        public string Label { get; set; }
        public string FilePath { get; set; }
    }

    public class PagedList<T> : List<T>
    {
        public PagedList(IEnumerable<T> items, int noOfItemsRead, int totalCount, int pageSize)
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
        private readonly int _statusCode;
        private static readonly JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = new List<JsonConverter> { new StringEnumConverter() },
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            Formatting = Formatting.None
        };

        public LeanJsonResult(object value, int statusCode) : base(value)
        {
            _statusCode = statusCode;
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

            var serializedValue = JsonConvert.SerializeObject(Value, _settings);
            await response.WriteAsync(serializedValue);
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
