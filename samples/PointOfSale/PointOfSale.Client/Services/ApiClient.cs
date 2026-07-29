using System.Net.Http.Headers;
using System.Net.Http.Json;
using PointOfSale.Contracts;

namespace PointOfSale.Client.Services;

public sealed class ApiClient : IDisposable
{
    private readonly HttpClient _http;

    public ApiClient(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public void SetToken(string? token)
    {
        _http.DefaultRequestHeaders.Authorization = token is null
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    public void Dispose() => _http.Dispose();

    // --- Auth ---

    public Task<LoginResponse> LoginAsync(string pin) =>
        PostAsync<LoginRequest, LoginResponse>("/api/auth/login", new LoginRequest(pin));

    public Task<ManagerOverrideResponse> ManagerOverrideAsync(string pin) =>
        PostAsync<ManagerOverrideRequest, ManagerOverrideResponse>("/api/auth/manager-override", new ManagerOverrideRequest(pin));

    // --- Categories ---

    public Task<List<CategoryDto>> GetCategoriesAsync() => GetAsync<List<CategoryDto>>("/api/categories");

    public Task<CategoryDto> CreateCategoryAsync(CategoryCreateDto request) =>
        PostAsync<CategoryCreateDto, CategoryDto>("/api/categories", request);

    public Task<CategoryDto> UpdateCategoryAsync(int id, CategoryUpdateDto request) =>
        PutAsync<CategoryUpdateDto, CategoryDto>($"/api/categories/{id}", request);

    public Task DeleteCategoryAsync(int id) => DeleteAsync($"/api/categories/{id}");

    // --- Products ---

    public Task<List<ProductDto>> GetProductsAsync(string? search = null, int? categoryId = null, bool activeOnly = false)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
            query.Add($"search={Uri.EscapeDataString(search)}");
        if (categoryId is not null)
            query.Add($"categoryId={categoryId}");
        if (activeOnly)
            query.Add("activeOnly=true");

        var qs = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        return GetAsync<List<ProductDto>>($"/api/products{qs}");
    }

    public Task<ProductDto> CreateProductAsync(ProductCreateDto request) =>
        PostAsync<ProductCreateDto, ProductDto>("/api/products", request);

    public Task<ProductDto> UpdateProductAsync(int id, ProductUpdateDto request) =>
        PutAsync<ProductUpdateDto, ProductDto>($"/api/products/{id}", request);

    public Task DeleteProductAsync(int id) => DeleteAsync($"/api/products/{id}");

    public Task<StockAdjustmentDto> AdjustStockAsync(int productId, StockAdjustmentCreateDto request) =>
        PostAsync<StockAdjustmentCreateDto, StockAdjustmentDto>($"/api/products/{productId}/stock-adjustments", request);

    // --- Sales ---

    public Task<SaleReceiptDto> CreateSaleAsync(SaleCreateDto request) =>
        PostAsync<SaleCreateDto, SaleReceiptDto>("/api/sales", request);

    public Task<SaleReceiptDto> VoidSaleAsync(int saleId) =>
        PostAsync<object?, SaleReceiptDto>($"/api/sales/{saleId}/void", null);

    public Task<List<SaleSummaryDto>> GetSalesAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = new List<string>();
        if (from is not null)
            query.Add($"from={from:o}");
        if (to is not null)
            query.Add($"to={to:o}");

        var qs = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        return GetAsync<List<SaleSummaryDto>>($"/api/sales{qs}");
    }

    // --- Reports ---

    public Task<DailySummaryDto> GetDailySummaryAsync(DateOnly date) =>
        GetAsync<DailySummaryDto>($"/api/reports/daily-summary?date={date:yyyy-MM-dd}");

    public Task<List<TopProductDto>> GetTopProductsAsync(DateTime? from = null, DateTime? to = null, int take = 10)
    {
        var query = new List<string> { $"take={take}" };
        if (from is not null)
            query.Add($"from={from:o}");
        if (to is not null)
            query.Add($"to={to:o}");

        return GetAsync<List<TopProductDto>>($"/api/reports/top-products?{string.Join("&", query)}");
    }

    // --- Users ---

    public Task<List<UserDto>> GetUsersAsync() => GetAsync<List<UserDto>>("/api/users");

    public Task<UserDto> CreateUserAsync(UserCreateDto request) =>
        PostAsync<UserCreateDto, UserDto>("/api/users", request);

    public Task<UserDto> UpdateUserAsync(int id, UserUpdateDto request) =>
        PutAsync<UserUpdateDto, UserDto>($"/api/users/{id}", request);

    // --- Low-level helpers ---

    private async Task<TResponse> GetAsync<TResponse>(string url)
    {
        var response = await _http.GetAsync(url);
        return await ReadOrThrowAsync<TResponse>(response);
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest body)
    {
        var response = await _http.PostAsJsonAsync(url, body);
        return await ReadOrThrowAsync<TResponse>(response);
    }

    private async Task<TResponse> PutAsync<TRequest, TResponse>(string url, TRequest body)
    {
        var response = await _http.PutAsJsonAsync(url, body);
        return await ReadOrThrowAsync<TResponse>(response);
    }

    private async Task DeleteAsync(string url)
    {
        var response = await _http.DeleteAsync(url);
        if (!response.IsSuccessStatusCode)
            throw new ApiException(response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<TResponse> ReadOrThrowAsync<TResponse>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new ApiException(response.StatusCode, await response.Content.ReadAsStringAsync());

        var result = await response.Content.ReadFromJsonAsync<TResponse>();
        return result ?? throw new InvalidOperationException("API returned an empty response body.");
    }
}
