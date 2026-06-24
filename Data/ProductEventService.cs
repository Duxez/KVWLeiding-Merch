namespace kvwleidingmerch.Data;

public sealed class ProductEventService
{
    public event Action? OnProductAdded;
    public event Action? OnProductDeleted;

    public void NotifyProductAdded() => OnProductAdded?.Invoke();
    public void NotifyProductDeleted() => OnProductDeleted?.Invoke();
}
