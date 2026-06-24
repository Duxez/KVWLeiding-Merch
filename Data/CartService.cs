using System.Text;
using System.Text.Json;
using BitzArt.Blazor.Cookies;

namespace kvwleidingmerch.Data;

public sealed class CartItem
{
    public int ProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int? SizeId { get; set; }
    public string SizeName { get; set; } = string.Empty;
    public int? ColorId { get; set; }
    public string ColorName { get; set; } = string.Empty;
    public string ColorHex { get; set; } = string.Empty;
}

public sealed class CartService
{
    private const string CartCookieName = "cart";
    private static readonly TimeSpan CartCookieLifetime = TimeSpan.FromDays(30);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICookieService _cookieService;
    private readonly List<CartItem> _items = [];
    private bool _isInitialized;

    public CartService(ICookieService cookieService)
    {
        _cookieService = cookieService;
    }

    public IReadOnlyList<CartItem> Items => _items;
    public int TotalCount => _items.Count;

    public event Action? OnCartChanged;

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;

        try
        {
            Cookie? cookie = await _cookieService.GetAsync(CartCookieName);
            if (cookie is null || string.IsNullOrWhiteSpace(cookie.Value))
                return;

            var json = DecodeFromBase64(cookie.Value);
            if (string.IsNullOrWhiteSpace(json))
                return;

            var savedItems = JsonSerializer.Deserialize<List<CartItem>>(json, JsonOptions);
            if (savedItems is null || savedItems.Count == 0)
                return;

            _items.Clear();
            foreach (var item in savedItems)
            {
                _items.Add(NormalizeCartItem(item));
            }
            OnCartChanged?.Invoke();
        }
        catch
        {
            await _cookieService.RemoveAsync(CartCookieName);
        }
    }

    public void AddItem(CartItem item)
    {
        _items.Add(item);
        _ = PersistAsync();
        OnCartChanged?.Invoke();
    }

    public void RemoveAt(int index)
    {
        if (index >= 0 && index < _items.Count)
        {
            _items.RemoveAt(index);
            _ = PersistAsync();
            OnCartChanged?.Invoke();
        }
    }

    public void Clear()
    {
        _items.Clear();
        _ = PersistAsync();
        OnCartChanged?.Invoke();
    }

    private async Task PersistAsync()
    {
        try
        {
            if (_items.Count == 0)
            {
                await _cookieService.RemoveAsync(CartCookieName);
                return;
            }

            var json = JsonSerializer.Serialize(_items, JsonOptions);
            var encoded = EncodeToBase64(json);
            await _cookieService.SetAsync(
                CartCookieName,
                encoded,
                DateTimeOffset.UtcNow.Add(CartCookieLifetime));
        }
        catch
        {
            // Keep cart usable in-memory even if cookie persistence fails.
        }
    }

    private static CartItem NormalizeCartItem(CartItem item)
    {
        if (item.SizeId == 0)
            item.SizeId = null;
        if (item.ColorId == 0)
            item.ColorId = null;
        return item;
    }

    private static string EncodeToBase64(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static string? DecodeFromBase64(string value)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch
        {
            return null;
        }
    }
}
