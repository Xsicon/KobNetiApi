namespace KobNeti.Api.Shared;

public class Response<T>
{
    public T? Data { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public static Response<T> SuccessResponse(T data, string message = "") => new()
    {
        Data = data,
        Success = true,
        Message = message
    };

    public static Response<T> Fail(string message) => new()
    {
        Success = false,
        Message = message
    };
}

public class PaginatedResponse<T>
{
    public List<T> Data { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasMore { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }

    public static PaginatedResponse<T> Create(List<T> data, int totalCount, int page, int pageSize)
    {
        var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PaginatedResponse<T>
        {
            Data = data,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            HasMore = page < totalPages,
            HasPreviousPage = page > 1,
            HasNextPage = page < totalPages
        };
    }
}
