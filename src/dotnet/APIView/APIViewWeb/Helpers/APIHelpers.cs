using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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

    public class FilterAndSortParams
    {
        public string Name { get; set; }
        public IEnumerable<string> Languages { get; set; }
        public string SortField { get; set; } = "LastUpdatedOn";
        public int SortOrder { get; set; } = 1;
        public bool? IsApproved { get; set; }
        public bool IsDeleted { get; set; }
        public bool AssignedToMe { get; set; }
        public string Label { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string ReviewId { get; set; }
        public bool WithTreeStyleTokens { get; set; }
        public IEnumerable<string> Details { get; set; }
    }

    public class APIRevisionSoftDeleteParam
    {
        public string reviewId { get; set; }
        public IEnumerable<string> apiRevisionIds { get; set; }
    }

    public class SamplesRevisionSoftDeleteParam
    {
        public string reviewId { get; set; }
        public IEnumerable<string> samplesRevisionIds { get; set; }
    }

    public class ReviewCreationParam                                                                    
    {
        public IFormFile File { get; set; }
        public string Language { get; set; }
        public string Label { get; set; }
        public string FilePath { get; set; }
    }

    public class UsageSampleAPIParam 
    {
        public IFormFile File { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
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
        private readonly string _locationUrl;

        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        public LeanJsonResult(object value, int statusCode) : base(value)
        {
            _statusCode = statusCode;
        }

        public LeanJsonResult(object value, int statusCode, string locationUrl) : base(value)
        {
            _statusCode = statusCode;
            _locationUrl = locationUrl;
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
            response.Headers["Location"] = _locationUrl;

            var serializedValue = JsonSerializer.Serialize(Value, _serializerOptions);
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

