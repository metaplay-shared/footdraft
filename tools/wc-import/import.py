#!/usr/bin/env python3
"""
FOOTDRAFT — World Cup 2026 squad importer.

Reads the Wikipedia "2026 FIFA World Cup squads" page (all 48 nations × 26-man final squads, with positions
and clubs) and generates the C# file
  SharedCode/WorldCup/WorldCupSquadsGenerated.cs
which appends every nation + squad to the World Cup config (the "draft a WC squad" knockout mode picks from
these; opponents are these nations' best XIs).

SQUADS: parsed from the page wikitext ({{nat fs g player|no=|pos=|name=|club=|...}} templates), fetched from
the MediaWiki API (cached under the scratchpad). Authoritative + complete (48 nations, 1248 players).

RATINGS (Ovr): resolved per player, reusing the SAME real-ratings source as the legend importer so WC and
legend XIs share one scale:
  Tier 1  FIFA career-peak — MAX(overall) across that player's FIFA 15-24 rows in male_players.csv, matched by
                             normalised name (long + short), nationality-preferred. Real EA ratings.
  Tier 2  Curated          — a small hand dict for stars FIFA under-rates / misses (extend as needed).
  Tier 3  Heuristic floor  — position-based default for unmatched (young/obscure call-ups).
Final Ovr clamped to 58..95.

DATA NOTE: male_players.csv (FIFA 15-24) is NOT redistributable and lives OUTSIDE the repo at RATINGS_DIR
(override with FOOTDRAFT_RATINGS_DIR). If absent, every player falls through to the tier-3 floor.

Run:  python3 tools/wc-import/import.py
Then: dotnet build Backend/Server/Server.csproj   (value-only data; serializer regen still needed for the new
      [MetaSerializable] WC *types*, but not for re-running this importer).
"""

import csv, os, re, sys, json, unicodedata, urllib.request
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_PATH = os.path.join(ROOT, "SharedCode", "WorldCup", "WorldCupSquadsGenerated.cs")

# Cached Wikipedia wikitext (fetched once; re-fetched if missing).
CACHE = os.environ.get("FOOTDRAFT_WC_WIKITEXT",
    "/private/tmp/claude-501/-Users-chris/9529f1c0-e2e2-4585-8aab-da320872a825/scratchpad/wc/squads.json")
WIKI_API = ("https://en.wikipedia.org/w/api.php?action=parse&page=2026_FIFA_World_Cup_squads"
            "&prop=wikitext&format=json&formatversion=2")

RATINGS_DIR = os.environ.get("FOOTDRAFT_RATINGS_DIR",
    "/private/tmp/claude-501/-Users-chris/cde3fdfa-f3c7-4fdb-b9cb-3e3113e1d23b/scratchpad/ratings-data")
FIFA_CSV = os.path.join(RATINGS_DIR, "male_players.csv")

OVR_MIN, OVR_MAX = 58, 95
POS_MAP = {"GK": "GK", "DF": "DEF", "MF": "MID", "FW": "FWD"}
POS_FLOOR = {"GK": 68, "DEF": 68, "MID": 69, "FWD": 70}

# 48 qualified nations → (FIFA tri-code, flag emoji). Keyed by the exact Wikipedia L3 section header.
NATION_META = {
    "Czech Republic": ("CZE", "🇨🇿"), "Mexico": ("MEX", "🇲🇽"), "South Africa": ("RSA", "🇿🇦"), "South Korea": ("KOR", "🇰🇷"),
    "Bosnia and Herzegovina": ("BIH", "🇧🇦"), "Canada": ("CAN", "🇨🇦"), "Qatar": ("QAT", "🇶🇦"), "Switzerland": ("SUI", "🇨🇭"),
    "Brazil": ("BRA", "🇧🇷"), "Haiti": ("HAI", "🇭🇹"), "Morocco": ("MAR", "🇲🇦"), "Scotland": ("SCO", "🏴"),
    "Australia": ("AUS", "🇦🇺"), "Paraguay": ("PAR", "🇵🇾"), "Turkey": ("TUR", "🇹🇷"), "United States": ("USA", "🇺🇸"),
    "Curaçao": ("CUW", "🇨🇼"), "Ecuador": ("ECU", "🇪🇨"), "Germany": ("GER", "🇩🇪"), "Ivory Coast": ("CIV", "🇨🇮"),
    "Japan": ("JPN", "🇯🇵"), "Netherlands": ("NED", "🇳🇱"), "Sweden": ("SWE", "🇸🇪"), "Tunisia": ("TUN", "🇹🇳"),
    "Belgium": ("BEL", "🇧🇪"), "Egypt": ("EGY", "🇪🇬"), "Iran": ("IRN", "🇮🇷"), "New Zealand": ("NZL", "🇳🇿"),
    "Cape Verde": ("CPV", "🇨🇻"), "Saudi Arabia": ("KSA", "🇸🇦"), "Spain": ("ESP", "🇪🇸"), "Uruguay": ("URU", "🇺🇾"),
    "France": ("FRA", "🇫🇷"), "Iraq": ("IRQ", "🇮🇶"), "Norway": ("NOR", "🇳🇴"), "Senegal": ("SEN", "🇸🇳"),
    "Algeria": ("ALG", "🇩🇿"), "Argentina": ("ARG", "🇦🇷"), "Austria": ("AUT", "🇦🇹"), "Jordan": ("JOR", "🇯🇴"),
    "Colombia": ("COL", "🇨🇴"), "DR Congo": ("COD", "🇨🇩"), "Portugal": ("POR", "🇵🇹"), "Uzbekistan": ("UZB", "🇺🇿"),
    "Croatia": ("CRO", "🇭🇷"), "England": ("ENG", "🏴"), "Ghana": ("GHA", "🇬🇭"), "Panama": ("PAN", "🇵🇦"),
}

# Tier-2 curated peaks (FIFA-equivalent) for the marquee names — guarantees the stars a player recognises are
# elite-rated even if FIFA 15-24 misses or under-rates them (recent breakouts, late call-ups). Extend freely.
# Keys are matched accent-insensitively, so spelling variants (Vinícius/Vinicius) are fine.
CURATED_RAW = {
    # Argentina
    "Lionel Messi": 93, "Lautaro Martínez": 89, "Julián Álvarez": 86, "Emiliano Martínez": 86,
    "Alexis Mac Allister": 86, "Enzo Fernández": 85,
    # France
    "Kylian Mbappé": 91, "Ousmane Dembélé": 87, "Antoine Griezmann": 86, "Aurélien Tchouaméni": 85,
    "William Saliba": 86, "Mike Maignan": 87,
    # England
    "Jude Bellingham": 90, "Harry Kane": 90, "Bukayo Saka": 87, "Phil Foden": 88, "Declan Rice": 87,
    # Spain
    "Rodri": 91, "Lamine Yamal": 89, "Pedri": 88, "Gavi": 86, "Nico Williams": 84,
    # Portugal
    "Cristiano Ronaldo": 88, "Bruno Fernandes": 88, "Bernardo Silva": 88, "Rúben Dias": 88,
    "Rafael Leão": 86, "Vitinha": 85,
    # Brazil
    "Vinícius Júnior": 90, "Rodrygo": 86, "Raphinha": 86, "Bruno Guimarães": 87, "Alisson": 89,
    "Marquinhos": 86, "Neymar": 88,
    # Netherlands / Germany / Belgium
    "Virgil van Dijk": 89, "Frenkie de Jong": 87, "Jamal Musiala": 88, "Florian Wirtz": 88,
    "Joshua Kimmich": 88, "Kevin De Bruyne": 90, "Thibaut Courtois": 90, "Romelu Lukaku": 85,
    # Rest-of-world headliners
    "Erling Haaland": 91, "Martin Ødegaard": 88, "Mohamed Salah": 89, "Heung-min Son": 87,
    "Federico Valverde": 88, "Luka Modrić": 86, "Achraf Hakimi": 85, "Luis Díaz": 86,
    "Christian Pulisic": 85, "Takefusa Kubo": 83, "Kaoru Mitoma": 83,
}


def norm(s):
    s = unicodedata.normalize("NFKD", s)
    s = "".join(c for c in s if not unicodedata.combining(c))
    s = s.lower()
    s = re.sub(r"[^a-z0-9 ]", " ", s)
    return re.sub(r"\s+", " ", s).strip()


def first_last(n):
    parts = n.split()
    return f"{parts[0]} {parts[-1]}" if len(parts) > 1 else n


def initial_last(n):
    # "lionel messi" -> "l messi" (matches FIFA short_name form "L. Messi"); the strongest single matcher.
    parts = n.split()
    return f"{parts[0][0]} {parts[-1]}" if len(parts) > 1 and parts[0] else n


# ---------------------------------------------------------------------------------------------------------
# Wikitext parsing
# ---------------------------------------------------------------------------------------------------------
def load_wikitext():
    if not os.path.exists(CACHE):
        os.makedirs(os.path.dirname(CACHE), exist_ok=True)
        req = urllib.request.Request(WIKI_API, headers={"User-Agent": "footdraft-import/1.0 (chris.wilson@metaplay.io)"})
        with urllib.request.urlopen(req, timeout=60) as r:
            open(CACHE, "wb").write(r.read())
    return json.load(open(CACHE))["parse"]["wikitext"]


def strip_link(v):
    """[[Target|Display]] -> Display ; [[Target]] -> Target ; plain -> plain. Drops refs/comments."""
    v = re.sub(r"<ref.*?</ref>", "", v, flags=re.S)
    v = re.sub(r"<ref[^>]*/>", "", v)
    v = re.sub(r"<!--.*?-->", "", v, flags=re.S)
    m = re.findall(r"\[\[([^\]]+)\]\]", v)
    if m:
        inner = m[0]
        v = inner.split("|")[-1] if "|" in inner else inner
    v = v.replace("[[", "").replace("]]", "")
    v = re.sub(r"\{\{.*?\}\}", "", v)
    return v.strip().strip("'").strip()


def split_top_params(body):
    """Split a template body on '|' at top level (ignoring | inside [[ ]] or {{ }})."""
    parts, depth_b, depth_t, cur = [], 0, 0, []
    i = 0
    while i < len(body):
        c = body[i]
        two = body[i:i + 2]
        if two == "[[": depth_b += 1; cur.append(two); i += 2; continue
        if two == "]]": depth_b -= 1; cur.append(two); i += 2; continue
        if two == "{{": depth_t += 1; cur.append(two); i += 2; continue
        if two == "}}": depth_t -= 1; cur.append(two); i += 2; continue
        if c == "|" and depth_b == 0 and depth_t == 0:
            parts.append("".join(cur)); cur = []
        else:
            cur.append(c)
        i += 1
    parts.append("".join(cur))
    return parts


def extract_player_templates(block):
    """Find each {{nat fs g player ...}} in a block, brace-matched (the age= sub-template has its own }})."""
    out = []
    marker = "{{nat fs g player"
    idx = 0
    while True:
        start = block.find(marker, idx)
        if start < 0:
            break
        depth, j = 0, start
        while j < len(block):
            if block[j:j + 2] == "{{": depth += 1; j += 2; continue
            if block[j:j + 2] == "}}":
                depth -= 1; j += 2
                if depth == 0:
                    break
                continue
            j += 1
        out.append(block[start + 2:j - 2])  # body between {{ and }}
        idx = j
    return out


def parse_squads(wt):
    """-> ordered list of (nation_name, group, [ (no, pos, name, club), ... ])."""
    # Map each nation's L3 header to its group via the preceding L2 "Group X" header.
    nation_group = {}
    cur_group = "?"
    for m in re.finditer(r"^(==+)\s*(.*?)\s*\1\s*$", wt, re.M):
        level, title = len(m.group(1)), m.group(2).strip()
        if level == 2 and title.startswith("Group "):
            cur_group = title.replace("Group ", "").strip()
        elif level == 3 and title in NATION_META:
            nation_group.setdefault(title, cur_group)

    squads = []
    # Each squad block sits under its nation's L3 header.
    headers = [(m.start(), m.group(1).strip())
               for m in re.finditer(r"^===\s*(.*?)\s*===\s*$", wt, re.M) if m.group(1).strip() in NATION_META]
    for k, (pos, nation) in enumerate(headers):
        end = headers[k + 1][0] if k + 1 < len(headers) else len(wt)
        section = wt[pos:end]
        s = section.find("{{nat fs g start}}")
        if s < 0:
            continue
        block = section[s:]
        players = []
        for body in extract_player_templates(block):
            params = {}
            for part in split_top_params(body):
                if "=" in part:
                    key, _, val = part.partition("=")
                    params[key.strip().lower()] = val
            if "name" not in params:
                continue
            no = re.sub(r"\D", "", params.get("no", "")) or "0"
            pos_raw = strip_link(params.get("pos", "")).upper()[:2]
            position = POS_MAP.get(pos_raw, "MID")
            name = strip_link(params["name"])
            club = strip_link(params.get("club", ""))
            if name:
                players.append((int(no), position, name, club))
        if players:
            squads.append((nation, nation_group.get(nation, "?"), players))
    return squads


# ---------------------------------------------------------------------------------------------------------
# Ratings
# ---------------------------------------------------------------------------------------------------------
def load_fifa():
    """norm(name) -> max overall, and (nationality, norm(name)) -> max overall. Indexes long+short+first_last."""
    glob = defaultdict(int)
    natidx = defaultdict(int)
    if not os.path.exists(FIFA_CSV):
        print(f"  ! FIFA ratings not found at {FIFA_CSV} — every player uses the position floor.", file=sys.stderr)
        return glob, natidx
    with open(FIFA_CSV, newline="", encoding="utf-8", errors="replace") as f:
        for row in csv.DictReader(f):
            try:
                ovr = int(row.get("overall") or 0)
            except ValueError:
                continue
            if ovr <= 0:
                continue
            nat = norm(row.get("nationality_name") or "")
            for raw in (row.get("long_name"), row.get("short_name")):
                if not raw:
                    continue
                nraw = norm(raw)
                for key in {nraw, first_last(nraw), initial_last(nraw)}:
                    if not key:
                        continue
                    if ovr > glob[key]:
                        glob[key] = ovr
                    if nat:
                        nk = (nat, key)
                        if ovr > natidx[nk]:
                            natidx[nk] = ovr
    return glob, natidx


CURATED = {norm(k): v for k, v in CURATED_RAW.items()}

# Wikipedia nation name -> FIFA nationality_name (only where they differ enough to matter for the nat-index).
FIFA_NAT = {"South Korea": "Korea Republic", "Ivory Coast": "Côte d'Ivoire", "DR Congo": "DR Congo",
            "United States": "United States", "Iran": "Iran", "Cape Verde": "Cabo Verde"}


def resolve_ovr(name, position, nation, glob, natidx):
    n = norm(name)
    if n in CURATED:
        return clamp(CURATED[n]), "curated"
    keys = {n, first_last(n), initial_last(n)}
    fifa_nat = norm(FIFA_NAT.get(nation, nation))
    best = 0
    for k in keys:
        v = natidx.get((fifa_nat, k), 0)
        if v > best:
            best = v
    src = "fifa-nat"
    if best == 0:
        for k in keys:
            v = glob.get(k, 0)
            if v > best:
                best = v
        src = "fifa"
    if best == 0:
        return POS_FLOOR.get(position, 69), "floor"
    return clamp(best), src


def clamp(v):
    return max(OVR_MIN, min(OVR_MAX, v))


# ---------------------------------------------------------------------------------------------------------
# Emit C#
# ---------------------------------------------------------------------------------------------------------
def cs_str(s):
    return s.replace("\\", "\\\\").replace('"', '\\"')


def emit(squads, glob, natidx):
    groups = defaultdict(list)  # group letter -> list of nations
    for nation, group, players in squads:
        groups[group].append((nation, players))

    stats = defaultdict(int)
    method_names = []
    bodies = []
    for group in sorted(groups):
        mname = f"GenGroup{group}"
        method_names.append(mname)
        lines = [f"        static void {mname}(List<NationInfo> sink)", "        {"]
        for nation, players in groups[group]:
            code, flag = NATION_META[nation]
            lines.append(f'            AddNation(sink, "{code}", "{cs_str(nation)}", "{flag}", "{group}", new WcSpec[]')
            lines.append("            {")
            for no, position, name, club in players:
                ovr, src = resolve_ovr(name, position, nation, glob, natidx)
                stats[src] += 1
                lines.append(
                    f'                W("{cs_str(name)}", Position.{position}, {ovr}, "{cs_str(club)}", {no}),')
            lines.append("            });")
        lines.append("        }")
        bodies.append("\n".join(lines))

    dispatch = "\n".join(f"            {m}(sink);" for m in method_names)
    header = (
        "// AUTO-GENERATED by tools/wc-import/import.py — do not edit by hand.\n"
        "// The 48 nations of the 2026 FIFA World Cup and their full 26-man squads (real players).\n"
        "// Squads: Wikipedia \"2026 FIFA World Cup squads\". Ovr: FIFA 15-24 career-peak (real EA ratings),\n"
        "// curated for a few stars, else a position floor. Clamped 58..95.\n\n"
        "using System.Collections.Generic;\n\n"
        "namespace Game.Logic\n"
        "{\n"
        "    public static partial class WorldCupContent\n"
        "    {\n"
        "        static partial void AppendGeneratedNations(List<NationInfo> sink)\n"
        "        {\n"
        f"{dispatch}\n"
        "        }\n\n"
        + "\n\n".join(bodies) + "\n"
        "    }\n"
        "}\n"
    )
    os.makedirs(os.path.dirname(OUT_PATH), exist_ok=True)
    open(OUT_PATH, "w", encoding="utf-8").write(header)
    return stats


def main():
    wt = load_wikitext()
    squads = parse_squads(wt)
    print(f"Parsed {len(squads)} nations, {sum(len(p) for _, _, p in squads)} players.")
    missing = [n for n in NATION_META if n not in {s[0] for s in squads}]
    if missing:
        print(f"  ! nations with no squad parsed: {missing}", file=sys.stderr)
    glob, natidx = load_fifa()
    print(f"FIFA index: {len(glob)} name keys.")
    stats = emit(squads, glob, natidx)
    total = sum(stats.values())
    print(f"Wrote {OUT_PATH}")
    print("Rating sources:", dict(stats), f"| matched {100*(total-stats['floor'])//max(1,total)}% to real ratings")


if __name__ == "__main__":
    main()
