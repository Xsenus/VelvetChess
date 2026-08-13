using System.Text.Json;
using VelvetChess.Core.Model;

var root = args.Length > 0 ? args[0] : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var input = Path.Combine(root, "work", "top_puzzles.csv");
var output = Path.Combine(root, "src", "VelvetChess.App", "Resources", "Raw", "puzzles.json");
var puzzles = new List<object>();
foreach (var line in File.ReadLines(input).Skip(1))
{
    var values = line.Split(',');
    if (values.Length < 9) continue;
    var id = values[0]; var board = new ChessBoard(values[1]); var moves = values[2].Split(' ');
    board.ApplyLegalMove(Move.ParseUci(moves[0]));
    var themes = values[7].Split(' ');
    var theme = ChooseTheme(themes);
    puzzles.Add(new
    {
        id,
        title = $"{ThemeName(theme)} · {puzzles.Count + 1}",
        theme = ThemeName(theme),
        rating = int.Parse(values[3]),
        fen = board.ToFen(),
        solution = moves.Skip(1).ToArray(),
        hint = ThemeHint(theme),
        explanation = $"Найдите единственную точную последовательность. Тема: {ThemeName(theme).ToLowerInvariant()}. Задача имеет популярность {values[5]}/100 по оценкам игроков.",
        sourceUrl = values[8]
    });
}
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, JsonSerializer.Serialize(puzzles, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Wrote {puzzles.Count} verified puzzles to {output}");

static string ChooseTheme(string[] themes)
{
    string[] priority = ["mateIn1", "mateIn2", "backRankMate", "doubleCheck", "fork", "pin", "skewer", "discoveredAttack", "sacrifice", "deflection", "attraction", "clearance", "promotion", "endgame", "middlegame"];
    return priority.FirstOrDefault(themes.Contains) ?? "tactics";
}
static string ThemeName(string theme) => theme switch
{
    "mateIn1" => "Мат в 1", "mateIn2" => "Мат в 2", "backRankMate" => "Мат по последней горизонтали",
    "doubleCheck" => "Двойной шах", "fork" => "Вилка", "pin" => "Связка", "skewer" => "Линейный удар",
    "discoveredAttack" => "Вскрытое нападение", "sacrifice" => "Жертва", "deflection" => "Отвлечение",
    "attraction" => "Завлечение", "clearance" => "Освобождение линии", "promotion" => "Превращение",
    "endgame" => "Эндшпиль", "middlegame" => "Миттельшпиль", _ => "Тактика"
};
static string ThemeHint(string theme) => theme switch
{
    "mateIn1" or "mateIn2" or "backRankMate" => "Сначала проверьте все шахи.",
    "fork" => "Ищите ход, который атакует сразу две цели.",
    "pin" => "Какая фигура не может уйти из-за более ценной фигуры позади?",
    "discoveredAttack" or "doubleCheck" => "Освободите линию для дальнобойной фигуры.",
    "deflection" or "attraction" => "Заставьте защитника покинуть ключевое поле.",
    "clearance" => "Освободите нужную линию или поле темповым ходом.",
    _ => "Считайте форсированные ходы: шахи, взятия и угрозы."
};
