namespace KamatekCrm.API.Models
{
    /// <summary>
    /// Standart API yanıt zarfı — Tüm endpoint'ler bu formatı kullanmalı.
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
        public ApiErrorInfo? Error { get; set; }
        public ApiMeta? Meta { get; set; }

        /// <summary>Başarılı yanıt (veri ile)</summary>
        public static ApiResponse<T> Ok(T data, string? message = null)
            => new() { Success = true, Data = data, Message = message };

        /// <summary>Başarılı yanıt (sayfalı veri ile)</summary>
        public static ApiResponse<T> Ok(T data, PaginationMeta pagination, string? message = null)
            => new() { Success = true, Data = data, Message = message, Meta = new ApiMeta { Pagination = pagination } };

        /// <summary>Hata yanıtı</summary>
        public static ApiResponse<T> Fail(string message, string? errorType = null)
            => new() { Success = false, Message = message, Error = new ApiErrorInfo { Type = errorType ?? "Error", Message = message } };
    }

    /// <summary>
    /// Veri olmayan başarı/hata yanıtları için
    /// </summary>
    public class ApiResponse : ApiResponse<object>
    {
        public static ApiResponse Ok(string? message = null)
            => new() { Success = true, Message = message };

        public new static ApiResponse Fail(string message, string? errorType = null)
            => new() { Success = false, Message = message, Error = new ApiErrorInfo { Type = errorType ?? "Error", Message = message } };
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

    /// <summary>
    /// Sayfalama meta verisi — tüm sayfalı endpoint'ler bu bilgiyi döner
    /// </summary>
    public class PaginationMeta
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }
}
