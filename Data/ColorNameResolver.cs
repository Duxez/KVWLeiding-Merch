using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace kvwleidingmerch.Data;

public interface IColorNameResolver
{
    Task<string?> ResolveHexByNameAsync(string colorName, CancellationToken cancellationToken = default);
}

public sealed class ColorNameResolver(HttpClient httpClient, IConfiguration configuration) : IColorNameResolver
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly string _baseUrl = configuration["ColorNameApi:BaseUrl"] ?? "https://api.color.pizza";
    public static readonly IReadOnlyDictionary<string, string> DutchCatalogHex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["wit"] = "#FFFFFF",
        ["vanille"] = "#F3E5AB",
        ["beige"] = "#F5F5DC",
        ["perzik"] = "#FFCBA4",
        ["mokka"] = "#967969",
        ["middelgrijs"] = "#808080",
        ["lichtgrijs"] = "#D3D3D3",
        ["grijsgroen"] = "#8A9A8A",
        ["kellygroen"] = "#4CBB17",
        ["flesgroen"] = "#006A4E",
        ["turquoise"] = "#40E0D0",
        ["nevelblauw"] = "#AEC6CF",
        ["diepzeeblauw"] = "#006994",
        ["koningsblauw"] = "#4169E1",
        ["lavendel"] = "#E6E6FA",
        ["roze"] = "#FFC0CB",
        ["rood"] = "#FF0000",
        ["bordeaux"] = "#800020",
        ["houtskool"] = "#36454F",
        ["grijs"] = "#808080",
        ["antiekblauw"] = "#6B8FAB",
        ["navy"] = "#000080",
        ["zwart"] = "#000000",
        ["oranje"] = "#FFA500",
        ["citroengeel"] = "#FFF44F",
        ["mint"] = "#98FF98",
    };

    private static readonly Dictionary<string, string> DutchColorAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zwart"] = "black",
        ["wit"] = "white",
        ["rood"] = "red",
        ["groen"] = "green",
        ["blauw"] = "blue",
        ["geel"] = "yellow",
        ["oranje"] = "orange",
        ["paars"] = "purple",
        ["roze"] = "pink",
        ["bruin"] = "brown",
        ["grijs"] = "gray",
        ["antiekblauw"] = "antique blue",
        ["lichtgrijs"] = "light gray",
        ["donkergrijs"] = "dark gray",
        ["lichtblauw"] = "light blue",
        ["donkerblauw"] = "dark blue",
        ["lichtgroen"] = "light green",
        ["donkergroen"] = "dark green",
        ["goud"] = "gold",
        ["zilver"] = "silver",
        ["beige"] = "beige",
        ["turkoois"] = "turquoise",
        ["cyaan"] = "cyan",
        ["magenta"] = "magenta",
        ["lila"] = "lilac",
        ["violet"] = "violet",
        ["bordeaux"] = "burgundy",

        // Required Dutch catalog names.
        ["vanille"] = "vanilla",
        ["beige"] = "beige",
        ["perzik"] = "peach",
        ["mokka"] = "mocha",
        ["middelgrijs"] = "medium gray",
        ["lichtgrijs"] = "light gray",
        ["grijsgroen"] = "sage green",
        ["kellygroen"] = "kelly green",
        ["flesgroen"] = "bottle green",
        ["turquoise"] = "turquoise",
        ["nevelblauw"] = "misty blue",
        ["diepzeeblauw"] = "deep sea blue",
        ["koningsblauw"] = "royal blue",
        ["lavendel"] = "lavender",
        ["roze"] = "pink",
        ["houtskool"] = "charcoal",
        ["navy"] = "navy",
        ["oranje"] = "orange",
        ["citroengeel"] = "lemon yellow",
        ["mint"] = "mint",
    };

    public async Task<string?> ResolveHexByNameAsync(string colorName, CancellationToken cancellationToken = default)
    {
        var query = colorName.Trim();
        if (string.IsNullOrWhiteSpace(query))
            return null;

        if (DutchCatalogHex.TryGetValue(query, out var catalogHex))
            return catalogHex;

        var hex = await TryResolveFromApiAsync(query, cancellationToken);
        if (!string.IsNullOrWhiteSpace(hex))
            return hex;

        if (DutchColorAliases.TryGetValue(query, out var alias))
            return await TryResolveFromApiAsync(alias, cancellationToken);

        return null;
    }

    private async Task<string?> TryResolveFromApiAsync(string query, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl.TrimEnd('/')}/v1/names/?name={Uri.EscapeDataString(query)}&maxResults=1";
        var response = await _httpClient.GetFromJsonAsync<ColorNameApiResponse>(url, cancellationToken);
        var hex = response?.Colors?
            .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Hex))?
            .Hex;

        if (string.IsNullOrWhiteSpace(hex))
            return null;

        return hex.StartsWith("#", StringComparison.Ordinal) ? hex.ToUpperInvariant() : $"#{hex.ToUpperInvariant()}";
    }

    private sealed class ColorNameApiResponse
    {
        public List<ColorNameApiItem>? Colors { get; set; }
    }

    private sealed class ColorNameApiItem
    {
        public string? Hex { get; set; }
    }
}
