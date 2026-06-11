#!/usr/bin/env python3
"""
38-0-20 squad importer.

Reads the Transfermarkt-style squad dataset under tools/squad-import/DATA_CSV/ and generates the C# file
  SharedCode/Draft/SeasonSquadsGenerated.cs
which appends every (club, season) squad to the draft corpus (the spin wheel picks from these).

Layout expected (as shipped in archive.zip):
  DATA_CSV/Season_<YYYY>/<Club_With_Underscores>_<transfermarktId>_<YYYY>.csv
Each CSV has a header row with at least: position, name, nationality, signedFrom, age.
There are no player ratings in this source, so OVR is synthesised: a club-tier base + a transfer-fee
signal (from `signedFrom`, e.g. "Ablöse €5.30m") + a small age adjustment + a deterministic per-name
jitter. Recent/expensive signings rate higher; it's an approximation (real ratings need a FIFA dataset).

Run:  python3 tools/squad-import/import.py
Then: dotnet build Backend/SharedCode/SharedCode.csproj
      dotnet run --project tools/SerializerGen -- WebClient/Serializer
"""

import csv, os, re, glob, hashlib

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
DATA_DIR = os.path.join(HERE, "DATA_CSV")
OUT_PATH = os.path.join(ROOT, "SharedCode", "Draft", "SeasonSquadsGenerated.cs")

POS_MAP = {
    "goalkeeper": "GK",
    "centre-back": "DEF", "left-back": "DEF", "right-back": "DEF", "defender": "DEF", "sweeper": "DEF",
    "defensive midfield": "MID", "central midfield": "MID", "attacking midfield": "MID",
    "left midfield": "MID", "right midfield": "MID", "midfielder": "MID",
    "centre-forward": "FWD", "second striker": "FWD", "left winger": "FWD", "right winger": "FWD",
    "striker": "FWD", "forward": "FWD",
}

# Historically big clubs get a higher rating base (era-specific quality mostly comes through transfer fees).
ELITE  = {"Manchester United", "Arsenal", "Chelsea", "Liverpool", "Manchester City", "Tottenham Hotspur"}
STRONG = {"Everton", "Newcastle United", "Leeds United", "Aston Villa", "Leicester City",
          "West Ham United", "Blackburn Rovers", "Nottingham Forest", "Wolverhampton Wanderers"}

def club_base(club):
    if club in ELITE:  return 80
    if club in STRONG: return 77
    return 74

def map_pos(raw):
    return POS_MAP.get((raw or "").strip().lower(), "MID")

def first_nation(raw):
    # raw like "['Australia', 'Croatia']" or "['England']"
    m = re.findall(r"'([^']+)'", raw or "")
    return m[0] if m else ""

def fee_bump(signed_from):
    s = signed_from or ""
    m = re.search(r"€\s*([\d.]+)\s*m", s)
    if m:
        try:    return min(11.0, float(m.group(1)) * 0.35)
        except ValueError: return 0.0
    return 0.0  # €...k / free transfer / club name → no bump

def age_adj(raw_age):
    try:    a = int(float(str(raw_age).strip()))
    except (ValueError, TypeError): return 0
    if 24 <= a <= 31: return 1
    if a < 20 or a > 34: return -1
    return 0

def jitter(name):
    h = int(hashlib.md5(name.encode("utf-8")).hexdigest(), 16)
    return (h % 7) - 3  # -3..+3

def rating(club, signed_from, raw_age, name):
    v = club_base(club) + fee_bump(signed_from) + age_adj(raw_age) + jitter(name)
    return max(64, min(93, int(round(v))))

def parse_club_year(stem):
    # "Manchester_United_985_1998" -> ("Manchester United", 1998); strips trailing _<id>_<year>.
    m = re.match(r"^(.*)_(\d+)_(\d{4})$", stem)
    if not m:
        return None, None
    club = m.group(1).replace("_", " ").strip()
    club = re.sub(r"\s*\([^)]*\)", "", club).strip()  # drop "(- 2004)"-style notes
    club = re.sub(r"\s+FC$", "", club).strip()        # "Chelsea FC" -> "Chelsea"
    return club, int(m.group(3))

def season_str(year):
    return f"{year}/{(year + 1) % 100:02d}"

def esc(s):
    return (s or "").replace("\\", "\\\\").replace('"', '\\"').strip()

def main():
    files = sorted(glob.glob(os.path.join(DATA_DIR, "Season_*", "*.csv")))
    if not files:
        raise SystemExit(f"No CSVs under {DATA_DIR}/Season_*/")

    squads = []  # (club, season, [(name, POS, ovr, nation), ...])
    total = 0
    for path in files:
        stem = os.path.splitext(os.path.basename(path))[0]
        club, year = parse_club_year(stem)
        if not club:
            continue
        season = season_str(year)
        with open(path, newline="", encoding="utf-8-sig") as f:
            reader = csv.DictReader(f)
            seen, players = set(), []
            for row in reader:
                name = (row.get("name") or "").strip()
                if not name or name in seen:
                    continue
                seen.add(name)
                pos = map_pos(row.get("position"))
                ovr = rating(club, row.get("signedFrom"), row.get("age"), name)
                nat = first_nation(row.get("nationality"))
                players.append((name, pos, ovr, nat))
        if players:
            squads.append((club, season, players))
            total += len(players)

    # Chunk into many small methods: a single method with ~24k statements would exceed the C# 64KB
    # per-method IL limit. ~25 squads per method keeps each well under it.
    CHUNK = 25
    chunks = [squads[i:i + CHUNK] for i in range(0, len(squads), CHUNK)]

    lines = [
        "// AUTO-GENERATED by tools/squad-import/import.py — do not edit by hand.",
        "// The full Premier League squad database (per club-season), appended to the draft corpus.",
        "// Ratings are synthesised (club tier + transfer fee + age + jitter); the source has no OVRs.",
        "",
        "using System.Collections.Generic;",
        "",
        "namespace Game.Logic",
        "{",
        "    public static partial class LegendContent",
        "    {",
        "        static partial void AppendGeneratedSquads(List<LegendPlayer> sink)",
        "        {",
    ]
    for ci in range(len(chunks)):
        lines.append(f"            Gen{ci}(sink);")
    lines.append("        }")
    lines.append("")
    for ci, chunk in enumerate(chunks):
        lines.append(f"        static void Gen{ci}(List<LegendPlayer> sink)")
        lines.append("        {")
        for (club, season, players) in chunk:
            lines.append(f'            AddSquad(sink, "{esc(club)}", "{esc(season)}", new PSpec[]')
            lines.append("            {")
            for (name, pos, ovr, nat) in players:
                lines.append(f'                P("{esc(name)}", Position.{pos}, {ovr}, "{esc(nat)}"),')
            lines.append("            });")
        lines.append("        }")
    lines += ["    }", "}"]

    with open(OUT_PATH, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")

    print(f"Wrote {OUT_PATH}")
    print(f"  {len(squads)} club-seasons, {total} player-seasons.")

if __name__ == "__main__":
    main()
