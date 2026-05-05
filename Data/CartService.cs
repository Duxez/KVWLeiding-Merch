namespace kvwleidingmerch.Data;

public sealed class CartItem
{
    public int ProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public int SizeId { get; set; }
    public string SizeName { get; set; } = string.Empty;
    public int ColorId { get; set; }
    public string ColorName { get; set; } = string.Empty;
    public string ColorHex { get; set; } = string.Empty;
}

public sealed class CartService
{
    private readonly List<CartItem> _items = [];

    public IReadOnlyList<CartItem> Items => _items;
    public int TotalCount => _items.Count;

    public event Action? OnCartChanged;

    public void AddItem(CartItem item)
    {
        _items.Add(item);
        OnCartChanged?.Invoke();
    }

    public void RemoveAt(int index)
    {
        if (index >= 0 && index < _items.Count)
        {
            _items.RemoveAt(index);
            OnCartChanged?.Invoke();
        }
    }

    public void Clear()
    {
        _items.Clear();
        OnCartChanged?.Invoke();
    }
}
