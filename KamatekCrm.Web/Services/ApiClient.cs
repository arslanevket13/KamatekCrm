using System.Net.Http;
using System.Net.Http.Json;
using KamatekCrm.Shared.DTOs;

namespace KamatekCrm.Web.Services;

public interface IApiClient
{
    Task<T?> GetAsync<T>(string endpoint);
    Task<T?> PostAsync<T>(string endpoint, object data);
    Task<T?> PutAsync<T>(string endpoint, object data);
    Task<bool> DeleteAsync(string endpoint);
}

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _contextAccessor;

    public ApiClient(IHttpClientFactory httpClientFactory, IHttpContextAccessor contextAccessor)
    {
        _httpClient = httpClientFactory.CreateClient("ApiClient");
        _contextAccessor = contextAccessor;
    }

    private void AddAuthHeader()
    {
        var token = _contextAccessor.HttpContext?.User.FindFirst("Token")?.Value;
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<T?> GetAsync<T>(string endpoint)
    {
        AddAuthHeader();
        return await _httpClient.GetFromJsonAsync<T>(endpoint);
    }

    public async Task<T?> PostAsync<T>(string endpoint, object data)
    {
        AddAuthHeader();
        var response = await _httpClient.PostAsJsonAsync(endpoint, data);
        return await response.Content.ReadFromJsonAsync<T>();
    }

    public async Task<T?> PutAsync<T>(string endpoint, object data)
    {
        AddAuthHeader();
        var response = await _httpClient.PutAsJsonAsync(endpoint, data);
        return await response.Content.ReadFromJsonAsync<T>();
    }

    public async Task<bool> DeleteAsync(string endpoint)
    {
        AddAuthHeader();
        var response = await _httpClient.DeleteAsync(endpoint);
        return response.IsSuccessStatusCode;
    }
}

// DTOs for Web Dashboard
public class DashboardStats
{
    public int ActiveJobs { get; set; }
    public int CompletedToday { get; set; }
    public int PendingJobs { get; set; }
    public int FieldVisits { get; set; }
    public decimal TodayRevenue { get; set; }
    public decimal WeekRevenue { get; set; }
    public decimal MonthRevenue { get; set; }
    public int TotalCustomers { get; set; }
    public int NewCustomersThisMonth { get; set; }
    public int LowStockCount { get; set; }
}

public class CustomerListItem
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public bool IsVip { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class ProductListItem
{
    public int Id { get; set; }
    public string ProductName { get; set; } = "";
    public string SKU { get; set; } = "";
    public string? Barcode { get; set; }
    public decimal SalePrice { get; set; }
    public decimal PurchasePrice { get; set; }
    public int TotalStockQuantity { get; set; }
    public int MinStockLevel { get; set; }
    public string? CategoryName { get; set; }
}

public class JobListItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Status { get; set; } = "";
    public string Priority { get; set; } = "";
    public DateTime? ScheduledDate { get; set; }
    public string? CustomerName { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime CreatedDate { get; set; }
}
