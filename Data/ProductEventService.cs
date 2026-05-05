namespace kvwleidingmerch.Data;

public sealed class ProductEventService
{
    public event Action? OnProductAdded;

    public void NotifyProductAdded() => OnProductAdded?.Invoke();
}
