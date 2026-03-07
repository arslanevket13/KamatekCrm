using System.Collections.Generic;

namespace KamatekCrm.Shared.DTOs
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
        public List<string>? Errors { get; set; }
        public ApiErrorInfo? Error { get; set; }
        public ApiMeta? Meta { get; set; }
    }

    public class ApiErrorInfo
    {
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? CorrelationId { get; set; }
    }

    public class ApiMeta
    {
        public PaginationMeta? Pagination { get; set; }
    }

    public class PaginationMeta
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)System.Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }
}
