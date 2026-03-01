namespace KamatekCrm.API.Common
{
    /// <summary>
    /// Result pattern — Exception fırlatmak yerine başarı/hata durumunu taşır.
    /// Service katmanı bu dönüş tipini kullanır, Controller'da Ok/BadRequest'e dönüştürülür.
    /// </summary>
    public class Result
    {
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }
        public string? ErrorCode { get; }

        protected Result(bool isSuccess, string? errorMessage = null, string? errorCode = null)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            ErrorCode = errorCode;
        }

        public static Result Success() => new(true);
        public static Result Failure(string message, string? errorCode = null) => new(false, message, errorCode);
        
        public static Result<T> Success<T>(T data) => Result<T>.Success(data);
        public static Result<T> Failure<T>(string message, string? errorCode = null) => Result<T>.Failure(message, errorCode);
    }

    /// <summary>
    /// Generic Result — başarılı sonuç veri taşır
    /// </summary>
    public class Result<T> : Result
    {
        public T? Data { get; }

        private Result(bool isSuccess, T? data, string? errorMessage, string? errorCode) 
            : base(isSuccess, errorMessage, errorCode)
        {
            Data = data;
        }

        public static Result<T> Success(T data) => new(true, data, null, null);
        public new static Result<T> Failure(string message, string? errorCode = null) => new(false, default, message, errorCode);
    }

    /// <summary>
    /// Sayfalı veri sonucu
    /// </summary>
    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; }
        public int TotalCount { get; }
        public int Page { get; }
        public int PageSize { get; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;

        public PagedResult(IEnumerable<T> items, int totalCount, int page, int pageSize)
        {
            Items = items.ToList().AsReadOnly();
            TotalCount = totalCount;
            Page = page;
            PageSize = pageSize;
        }
    }

    /// <summary>
    /// Yaygın hata kodları sabitleri
    /// </summary>
    public static class ErrorCodes
    {
        public const string NotFound = "NOT_FOUND";
        public const string Duplicate = "DUPLICATE";
        public const string Validation = "VALIDATION_ERROR";
        public const string Unauthorized = "UNAUTHORIZED";
        public const string Forbidden = "FORBIDDEN";
        public const string Conflict = "CONFLICT";
        public const string ExternalService = "EXTERNAL_SERVICE_ERROR";
        public const string Database = "DATABASE_ERROR";
    }
}
