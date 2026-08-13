using System.Text.Json;
using VelvetChess.Core.Puzzles;

namespace VelvetChess.App.Services;

public sealed class PuzzleRepository
{
    private IReadOnlyList<ChessPuzzle>? _cache;

    public async Task<IReadOnlyList<ChessPuzzle>> GetAllAsync()
    {
        if (_cache is not null) return _cache;
        await using var stream = await FileSystem.OpenAppPackageFileAsync("puzzles.json");
        _cache = await JsonSerializer.DeserializeAsync<List<ChessPuzzle>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        return _cache;
    }

    public async Task<ChessPuzzle?> GetAsync(string id) => (await GetAllAsync()).FirstOrDefault(p => p.Id == id);
}
