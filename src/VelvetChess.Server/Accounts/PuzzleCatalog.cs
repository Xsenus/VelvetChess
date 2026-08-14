using System.Text.Json;

namespace VelvetChess.Server.Accounts;

public sealed record RatedPuzzle(string Id, int Rating, IReadOnlyList<string> Solution);

public interface IPuzzleCatalog
{
    RatedPuzzle? Find(string id);
}

public sealed class JsonPuzzleCatalog : IPuzzleCatalog
{
    private readonly IReadOnlyDictionary<string, RatedPuzzle> _puzzles;

    public JsonPuzzleCatalog(IWebHostEnvironment environment)
    {
        var path = Path.Combine(environment.ContentRootPath, "puzzles.json");
        if (!File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory, "puzzles.json");
        using var stream = File.OpenRead(path);
        _puzzles = (JsonSerializer.Deserialize<List<RatedPuzzle>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [])
            .ToDictionary(x => x.Id, StringComparer.Ordinal);
    }

    public RatedPuzzle? Find(string id) => _puzzles.GetValueOrDefault(id);
}
