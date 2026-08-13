import csv
import io
import zstandard as zstd

source = r"C:\Users\ilel\source\repos\VelvetChess\work_puzzles.csv.zst"
target = r"C:\Users\ilel\source\repos\VelvetChess\work\top_puzzles.csv"
rows = []
with open(source, "rb") as raw:
    stream = zstd.ZstdDecompressor().stream_reader(raw)
    text = io.TextIOWrapper(stream, encoding="utf-8", errors="ignore", newline="")
    try:
        for row in csv.reader(text):
            if len(row) < 9 or row[0] == "PuzzleId":
                continue
            try:
                rating, popularity, plays = int(row[3]), int(row[5]), int(row[6])
            except ValueError:
                continue
            themes = set(row[7].split())
            if 850 <= rating <= 2300 and popularity >= 95 and plays >= 100 and "veryLong" not in themes:
                rows.append((popularity, plays, row))
    except (zstd.ZstdError, EOFError):
        pass

rows.sort(key=lambda item: (item[0], item[1]), reverse=True)
selected = []
theme_counts = {}
for _, _, row in rows:
    primary = next((t for t in row[7].split() if t in {"mate", "fork", "pin", "skewer", "discoveredAttack", "sacrifice", "deflection", "attraction", "backRankMate", "doubleCheck", "clearance", "promotion", "endgame", "opening", "middlegame"}), "tactics")
    if theme_counts.get(primary, 0) >= 8:
        continue
    selected.append(row)
    theme_counts[primary] = theme_counts.get(primary, 0) + 1
    if len(selected) == 50:
        break

with open(target, "w", encoding="utf-8", newline="") as out:
    writer = csv.writer(out)
    writer.writerow(["PuzzleId", "FEN", "Moves", "Rating", "RatingDeviation", "Popularity", "NbPlays", "Themes", "GameUrl", "OpeningTags", "DailyDate"])
    writer.writerows(selected)
print(f"candidates={len(rows)} selected={len(selected)} themes={theme_counts}")
