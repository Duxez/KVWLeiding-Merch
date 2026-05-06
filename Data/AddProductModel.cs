namespace kvwleidingmerch.Data;

public sealed class AddProductModel
{
    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public decimal Price { get; set; }
}
