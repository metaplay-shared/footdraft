#!/usr/bin/env python3
"""
38-0-20 squad importer.

Reads the Transfermarkt-style squad dataset under tools/squad-import/DATA_CSV/ and generates the C# file
  SharedCode/Draft/SeasonSquadsGenerated.cs
which appends every (club, season) squad to the draft corpus (the spin wheel picks from these).

Layout expected (as shipped in archive.zip):
  DATA_CSV/Season_<YYYY>/<Club_With_Underscores>_<transfermarktId>_<YYYY>.csv
Each CSV has a header row with at least: position, name, nationality, signedFrom, age.

RATINGS (OVR) — CAREER-BEST, one value per distinct player NAME, applied to every season-card of that
player. (The original game used career-best; per-season ratings make a 2002 legend read "faded" on his
late cards, so we deliberately use the peak.) The OVR is resolved per player by the first tier that hits:

  Tier 1  FIFA career-peak  — MAX(overall) across that player's FIFA 15-24 rows in male_players.csv,
                              IF confidently matched (see _fifa_lookup). Real EA ratings.
  Tier 2  Curated marquee   — a hand-built dict of well-known career-peak FIFA-equivalent ratings for the
                              pre-2014 greats FIFA 15-24 misses or shows faded (Henry reads 78 not ~91;
                              Shearer/Giggs/Schmeichel/Cantona absent). OVERRIDES tier 1 for these names,
                              so peak-era legends aren't shown as faded. Estimates — "true-to-life", not exact.
  Tier 3  TM value band     — players with no FIFA match and not curated: log-scale their Transfermarkt
                              highest_market_value_in_eur into a 64..88 OVR band. CC0, same name-space.
  Tier 4  Heuristic floor   — the legacy club-tier + transfer-fee + age + jitter formula, last resort
                              (mostly 1992-2003 fringe players nothing else reaches).

Final OVR is clamped to 58..95. A handful of all-time greats reach 94-95 via curation — kept rare.

DATA NOTE: the FIFA dataset (male_players.csv) is NOT redistributable and must NOT be committed to this
repo. It lives outside the tree at RATINGS_DIR below (override with the FOOTDRAFT_RATINGS_DIR env var).
The Transfermarkt files (players_tm.csv, CC0) live alongside it. If RATINGS_DIR is absent the importer
still runs — every player falls through to the tier-4 heuristic floor, exactly like the original.

Run:  python3 tools/squad-import/import.py
Then: dotnet build Backend/SharedCode/SharedCode.csproj
(This is a value-only change — OVR ints — so the schema is unchanged and NO serializer regen is needed.)
"""

import csv, os, re, glob, hashlib, math, sys, unicodedata
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
DATA_DIR = os.path.join(HERE, "DATA_CSV")
OUT_PATH = os.path.join(ROOT, "SharedCode", "Draft", "SeasonSquadsGenerated.cs")

# Real-ratings source CSVs. OUTSIDE the repo and NOT redistributable (FIFA). Override via env var.
RATINGS_DIR = os.environ.get(
    "FOOTDRAFT_RATINGS_DIR",
    "/private/tmp/claude-501/-Users-chris/cde3fdfa-f3c7-4fdb-b9cb-3e3113e1d23b/scratchpad/ratings-data",
)
FIFA_CSV = os.path.join(RATINGS_DIR, "male_players.csv")      # EA/FIFA 15-24, real overalls (NOT redistributable)
TM_CSV   = os.path.join(RATINGS_DIR, "players_tm.csv")        # Transfermarkt highest market value (CC0)

OVR_MIN, OVR_MAX = 58, 95

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

# ---------------------------------------------------------------------------------------------------------
# Curated marquee legends (Tier 2). Career-peak FIFA-equivalent OVRs for famous PL-era players, focused on
# the pre-2014 greats that FIFA 15-24 misses entirely or shows long past their prime. Keys are normalised
# (see _norm): NFKD accent-fold, lowercase, punctuation->space, collapse whitespace. These OVERRIDE tier 1.
# Estimates of widely-known peak FIFA ratings; "true-to-life", not exact. ~190 names.
# ---------------------------------------------------------------------------------------------------------
CURATED_RAW = {
    # ---- Arsenal / Invincibles & 90s-00s ----
    "Thierry Henry": 91, "Dennis Bergkamp": 89, "Patrick Vieira": 88, "Robert Pires": 87,
    "Freddie Ljungberg": 84, "Sol Campbell": 85, "Ashley Cole": 87, "Gilberto Silva": 83,
    "Kolo Touré": 82, "Robin van Persie": 89, "Cesc Fàbregas": 87, "Emmanuel Petit": 84,
    "Marc Overmars": 86, "Nicolas Anelka": 85, "Ian Wright": 86, "David Seaman": 85,
    "Tony Adams": 85, "Lee Dixon": 80, "Nigel Winterburn": 79, "Martin Keown": 82,
    "Ray Parlour": 79, "Edu": 82, "Lauren": 81, "Jens Lehmann": 84, "José Antonio Reyes": 82,

    # ---- Manchester United treble & beyond ----
    "Roy Keane": 88, "Ryan Giggs": 89, "Paul Scholes": 88, "David Beckham": 89, "Eric Cantona": 89,
    "Peter Schmeichel": 90, "Gary Neville": 82, "Phil Neville": 78, "Denis Irwin": 81,
    "Jaap Stam": 87, "Rio Ferdinand": 88, "Nemanja Vidić": 87, "Wayne Rooney": 90, "Ruud van Nistelrooy": 90,
    "Andy Cole": 84, "Dwight Yorke": 85, "Teddy Sheringham": 84, "Ole Gunnar Solskjær": 83,
    "Edwin van der Sar": 86, "Patrice Evra": 84, "Michael Carrick": 84, "Park Ji-sung": 81,
    "Juan Sebastián Verón": 85, "Dimitar Berbatov": 85, "Carlos Tévez": 86, "Antonio Valencia": 82,
    "Darren Fletcher": 80, "Gabriel Heinze": 81, "Mikael Silvestre": 79, "Owen Hargreaves": 82,

    # ---- Liverpool ----
    "Steven Gerrard": 89, "Jamie Carragher": 84, "Fernando Torres": 89, "Xabi Alonso": 87,
    "Luis García": 82, "Sami Hyypiä": 84, "Dietmar Hamann": 81, "Robbie Fowler": 86,
    "Michael Owen": 88, "John Barnes": 87, "Ian Rush": 86, "Javier Mascherano": 85,
    "Pepe Reina": 85, "Daniel Agger": 82, "Dirk Kuyt": 81, "Emile Heskey": 79, "Jordan Henderson": 83,
    "Philippe Coutinho": 86, "Luis Suárez": 92, "Raheem Sterling": 88,

    # ---- Chelsea ----
    "Frank Lampard": 88, "Didier Drogba": 89, "John Terry": 87, "Petr Čech": 89, "Claude Makélélé": 86,
    "Michael Essien": 86, "Arjen Robben": 90, "Joe Cole": 83, "Damien Duff": 83, "Ricardo Carvalho": 85,
    "Gianfranco Zola": 89, "Marcel Desailly": 86, "Eden Hazard": 91, "Ashley Cole Chelsea": 87,
    "Nicolas Anelka Chelsea": 85, "Florent Malouda": 83, "Branislav Ivanović": 84, "Cesc Fàbregas Chelsea": 87,
    "Gus Poyet": 82, "Dennis Wise": 81, "Gianluca Vialli": 85, "Ruud Gullit": 88,

    # ---- Newcastle / Blackburn / 90s scorers ----
    "Alan Shearer": 90, "Les Ferdinand": 85, "David Ginola": 85, "Tino Asprilla": 84,
    "Gary Speed": 82, "Rob Lee": 80, "Nolberto Solano": 81, "Sutton": 82, "Chris Sutton": 83,
    "Tim Flowers": 81, "Colin Hendry": 82, "Graeme Le Saux": 81, "Michael Owen Newcastle": 85,
    "Hatem Ben Arfa": 82, "Demba Ba": 81, "Papiss Cissé": 80, "Yohan Cabaye": 82,

    # ---- Tottenham ----
    "Gareth Bale": 90, "Luka Modrić": 89, "Dimitar Berbatov Spurs": 85, "Jürgen Klinsmann": 88,
    "Teddy Sheringham Spurs": 84, "Ledley King": 84, "David Ginola Spurs": 85, "Robbie Keane": 84,
    "Jermain Defoe": 82, "Rafael van der Vaart": 85, "Michael Dawson": 80, "Aaron Lennon": 79,
    "Harry Kane": 90, "Christian Eriksen": 86, "Hugo Lloris": 87, "Son Heung-min": 89, "Dele Alli": 84,

    # ---- Everton / Villa / Leeds / Forest / others ----
    "Tim Cahill": 81, "Mikel Arteta": 83, "Leighton Baines": 83, "Phil Jagielka": 80,
    "Tim Howard": 82, "Romelu Lukaku": 86, "Wayne Rooney Everton": 90, "Duncan Ferguson": 80,
    "Rio Ferdinand Leeds": 84, "Harry Kewell": 85, "Mark Viduka": 83, "Lucas Radebe": 82,
    "Olivier Dacourt": 81, "Alan Smith": 79, "Stan Collymore": 84, "Dwight Yorke Villa": 84,
    "Paul McGrath": 84, "Dion Dublin": 80, "Gareth Barry": 82, "James Milner": 83,
    "Stuart Pearce": 83, "Steve Stone": 78,

    # ---- Manchester City modern ----
    "Sergio Agüero": 90, "Vincent Kompany": 87, "David Silva": 89, "Yaya Touré": 87,
    "Joe Hart": 84, "Pablo Zabaleta": 82, "Carlos Tévez City": 86, "Edin Džeko": 83,
    "Kevin De Bruyne": 92, "İlkay Gündoğan": 85, "Fernandinho": 84, "Nicolás Otamendi": 82,
    "Kyle Walker": 84, "Riyad Mahrez": 86, "Bernardo Silva": 87, "Gabriel Jesus": 83,
    "Mohamed Salah": 90, "Sadio Mané": 89, "Virgil van Dijk": 90,

    # ---- Goalkeepers (FIFA often shows late) ----
    "David James": 82, "Brad Friedel": 83, "Shay Given": 84, "Mark Schwarzer": 81,
    "Nigel Martyn": 81, "Fabien Barthez": 84, "David de Gea": 89, "Thibaut Courtois": 89,

    # ---- Global stars who spent peak years in the PL ----
    "Cristiano Ronaldo": 94, "Juan Mata": 84, "Santi Cazorla": 85, "Mesut Özil": 88,
    "Alexis Sánchez": 87, "Angel Di María": 86, "Gylfi Sigurðsson": 81, "Wilfried Zaha": 83,
    "Pierre-Emerick Aubameyang": 87, "Sadio Mane": 89,

    # ---- Premier League greats not in FIFA 15-24 / faded ----
    "Matt Le Tissier": 85, "Le Tissier": 85, "Juninho": 85, "Juninho Paulista": 85,
    "Gianfranco Zola Chelsea": 89, "Fabrizio Ravanelli": 84, "Paolo Di Canio": 84,
    "Patrik Berger": 82, "Roy Keane United": 88, "Jaap Stam United": 87,
    "Sander Westerveld": 78, "Tugay": 81, "Youri Djorkaeff": 84, "Jay-Jay Okocha": 85,
    "Robert Pirès": 87, "Sylvain Distin": 79, "El Hadji Diouf": 80,
    "Nwankwo Kanu": 82, "Kanu": 82, "Emmanuel Adebayor": 84, "Cesc Fabregas": 87,
    "Frédéric Kanouté": 82, "Louis Saha": 82, "Diego Forlán": 84, "Gus Caesar": 70,
    "Andriy Shevchenko": 88, "Michael Ballack": 87, "Hernán Crespo": 86, "Adrian Mutu": 83,
    "Jimmy Floyd Hasselbaink": 85, "Eiður Guðjohnsen": 83, "Eidur Gudjohnsen": 83,
    "Gianluca Vialli Chelsea": 85, "Roberto Di Matteo": 81,
    "Tore André Flo": 81, "Tore Andre Flo": 81, "Marcel Desailly Chelsea": 86,
    "William Gallas": 84, "Sol Campbell Arsenal": 85, "Bacary Sagna": 82,
    "Mikel John Obi": 80, "Jon Obi Mikel": 80, "John Obi Mikel": 80, "Salomon Kalou": 80,
    "Ramires": 82, "Oscar": 83, "Willian": 84, "Diego Costa": 86, "Nemanja Matić": 83,
    "Thibaut Courtois Chelsea": 89, "César Azpilicueta": 84, "Gary McAllister": 83,

    # ---- exact-corpus-spelling fixes (these legends are spelled differently in the roster source than
    # ---- the canonical form above, so the canonical key never matched — pin them by the corpus spelling) ----
    "Ji-sung Park": 81, "Gylfi Sigurdsson": 81, "Faustino Asprilla": 84, "Gustavo Poyet": 82,
    "Tugay Kerimoglu": 81, "Jay-Jay Okocha": 85, "Papiss Demba Cisse": 80, "Eidur Gudjohnsen": 83,
    "Lucas Radebe": 82, "Tim Sherwood": 80, "Youri Djorkaeff Bolton": 84,
}


# ===== name normalisation (shared by every tier) =================================================
def _norm(s):
    """NFKD accent-fold -> ASCII, lowercase, strip punctuation, collapse whitespace.
    Mirrors join_check.py; keeps word boundaries so token matching works."""
    s = unicodedata.normalize("NFKD", s or "")
    s = "".join(c for c in s if not unicodedata.combining(c))
    s = s.lower()
    s = re.sub(r"[^a-z0-9 ]+", " ", s)
    return re.sub(r"\s+", " ", s).strip()


# Curated dict keyed by normalised name. Some keys carry a club hint suffix (e.g. "ashley cole chelsea")
# to disambiguate in the source notes — but the lookup is by the plain player name, so we collapse them:
# strip a trailing club token group and keep the MAX OVR for any name that appears more than once.
def _build_curated():
    out = {}
    club_tokens = {"chelsea", "arsenal", "united", "city", "spurs", "leeds", "newcastle", "everton", "villa"}
    for raw, ovr in CURATED_RAW.items():
        n = _norm(raw)
        toks = n.split()
        # Drop a single trailing club-hint token if present and the remainder is still a plausible name (>=2 tokens).
        if len(toks) >= 3 and toks[-1] in club_tokens:
            n = " ".join(toks[:-1])
        out[n] = max(out.get(n, 0), int(ovr))
    return out

CURATED = _build_curated()


# ===== FIFA tier (Tier 1) ========================================================================
# Built lazily from male_players.csv. We keep, per normalised long_name and short_name:
#   peak overall, and the set of (nationality, club) tokens seen — for the anti-mismatch gate.
class FifaIndex:
    def __init__(self):
        self.ok = False
        self.peak_long = {}            # norm(long_name)  -> peak overall
        self.peak_short = {}           # norm(short_name) -> peak overall
        self.nat_long = defaultdict(set)   # norm(long_name)  -> {norm(nationality)}
        self.nat_short = defaultdict(set)
        self.club_long = defaultdict(set)  # norm(long_name)  -> {norm(club) tokens}
        self.club_short = defaultdict(set)
        self.long_keys_by_lasttoken = defaultdict(set)  # surname token -> {norm(long_name)} for subset matching

    def load(self, path):
        if not os.path.exists(path):
            return
        with open(path, newline="", encoding="utf-8") as f:
            r = csv.DictReader(f)
            for row in r:
                try:
                    ovr = int(float(row["overall"]))
                except (ValueError, TypeError, KeyError):
                    continue
                ln = _norm(row.get("long_name", ""))
                sn = _norm(row.get("short_name", ""))
                nat = _norm(row.get("nationality_name", ""))
                club_toks = set(_norm(row.get("club_name", "")).split())
                if ln:
                    if ovr > self.peak_long.get(ln, 0):
                        self.peak_long[ln] = ovr
                    if nat:
                        self.nat_long[ln].add(nat)
                    self.club_long[ln] |= club_toks
                    for tok in ln.split():
                        self.long_keys_by_lasttoken[tok].add(ln)
                if sn:
                    if ovr > self.peak_short.get(sn, 0):
                        self.peak_short[sn] = ovr
                    if nat:
                        self.nat_short[sn].add(nat)
                    self.club_short[sn] |= club_toks
        self.ok = len(self.peak_long) > 0

    def lookup(self, corpus_name, corpus_nat, corpus_club):
        """Return (ovr, how) or (None, None). Matching order: exact long, exact short, gated token-subset.
        The token-subset tier is gated on nationality OR shared-club agreement to avoid the classic
        'Peter Schmeichel := Kasper (Peter) Schmeichel' family mismatch. When in doubt, returns None."""
        n = _norm(corpus_name)
        if not n:
            return None, None
        cn = _norm(corpus_nat)
        cclub_toks = set(_norm(corpus_club).split())

        # 1) exact long_name
        if n in self.peak_long:
            return self.peak_long[n], "fifa-long-exact"
        # 2) exact short_name
        if n in self.peak_short:
            return self.peak_short[n], "fifa-short-exact"

        # 3) gated token-subset: a UNIQUE FIFA long_name whose token set is a SUPERSET of all corpus tokens,
        #    e.g. corpus "Didier Drogba" ⊆ FIFA "Didier Yves Drogba Tebily". Require agreement on nationality
        #    OR a shared club token, AND reject the dangerous relative-collision shape (see below).
        toks = set(n.split())
        if not toks:
            return None, None
        last = n.split()[-1]
        cands = [k for k in self.long_keys_by_lasttoken.get(last, ()) if toks <= set(k.split())]
        # Only trust a subset match when it is unambiguous (exactly one candidate family).
        if len(cands) != 1:
            return None, None
        cand = cands[0]
        cand_toks = cand.split()
        # Anti-relative gate: if the candidate has an EXTRA leading given-name before the corpus name's
        # first token (e.g. corpus "peter schmeichel" vs cand "kasper peter schmeichel"), the FIFA row is
        # very likely a different (often related) person. Reject outright — curation covers the real legend.
        first_corpus = n.split()[0]
        if cand_toks[0] != first_corpus and first_corpus in cand_toks[1:]:
            return None, None
        # Nationality / club agreement gate.
        nat_ok = bool(cn) and cn in self.nat_long.get(cand, set())
        club_ok = bool(cclub_toks) and bool(cclub_toks & self.club_long.get(cand, set()))
        if nat_ok or club_ok:
            return self.peak_long[cand], "fifa-subset-gated"
        return None, None


# ===== Transfermarkt value tier (Tier 3) =========================================================
class TmValueIndex:
    def __init__(self):
        self.peak_value = {}   # norm(name) -> highest_market_value_in_eur (float)

    def load(self, path):
        if not os.path.exists(path):
            return
        with open(path, newline="", encoding="utf-8") as f:
            for row in csv.DictReader(f):
                n = _norm(row.get("name", ""))
                if not n:
                    continue
                v = row.get("highest_market_value_in_eur", "")
                try:
                    v = float(v)
                except (ValueError, TypeError):
                    continue
                if v > 0 and v > self.peak_value.get(n, 0):
                    self.peak_value[n] = v

    def lookup(self, corpus_name):
        v = self.peak_value.get(_norm(corpus_name))
        if not v:
            return None
        # log-scale band: €100k (log10=5.0) -> 64 ; €100M (log10=8.0) -> 88. Clamp into [64, 88].
        x = math.log10(v)
        ovr = 64 + (x - 5.0) / (8.0 - 5.0) * (88 - 64)
        return max(64, min(88, int(round(ovr))))


# ===== Tier 4: legacy heuristic floor ============================================================
def club_base(club):
    if club in ELITE:  return 80
    if club in STRONG: return 77
    return 74

def fee_bump(signed_from):
    s = signed_from or ""
    m = re.search(r"€\s*([\d.]+)\s*m", s)
    if m:
        try:    return min(11.0, float(m.group(1)) * 0.35)
        except ValueError: return 0.0
    return 0.0

def age_adj(raw_age):
    try:    a = int(float(str(raw_age).strip()))
    except (ValueError, TypeError): return 0
    if 24 <= a <= 31: return 1
    if a < 20 or a > 34: return -1
    return 0

def jitter(name):
    h = int(hashlib.md5(name.encode("utf-8")).hexdigest(), 16)
    return (h % 7) - 3  # -3..+3

def heuristic_rating(club, signed_from, raw_age, name):
    v = club_base(club) + fee_bump(signed_from) + age_adj(raw_age) + jitter(name)
    return max(64, min(93, int(round(v))))


# ===== misc helpers (unchanged emit-affecting behaviour) =========================================
def map_pos(raw):
    return POS_MAP.get((raw or "").strip().lower(), "MID")

def first_nation(raw):
    # raw like "['Australia', 'Croatia']" or "['England']"
    m = re.findall(r"'([^']+)'", raw or "")
    return m[0] if m else ""

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


# ===== career-best resolution ====================================================================
def resolve_ovr(name, nat, club, signed_from, raw_age, fifa, tm, stats):
    """One career-best OVR per distinct player NAME. Tier 2 (curated) overrides tier 1 (FIFA)."""
    n = _norm(name)
    # Tier 2 curated OVERRIDES tier 1 for the marquee names.
    if n in CURATED:
        stats["curated"] += 1
        return _clamp(CURATED[n]), "curated"
    # Tier 1 FIFA.
    ovr, how = fifa.lookup(name, nat, club)
    if ovr is not None:
        stats[how] += 1
        return _clamp(ovr), how
    # Tier 3 TM value band.
    tmovr = tm.lookup(name)
    if tmovr is not None:
        stats["tm-value"] += 1
        return _clamp(tmovr), "tm-value"
    # Tier 4 heuristic floor.
    stats["heuristic"] += 1
    return _clamp(heuristic_rating(club, signed_from, raw_age, name)), "heuristic"

def _clamp(v):
    return max(OVR_MIN, min(OVR_MAX, int(round(v))))


def main():
    files = sorted(glob.glob(os.path.join(DATA_DIR, "Season_*", "*.csv")))
    if not files:
        raise SystemExit(f"No CSVs under {DATA_DIR}/Season_*/")

    # Load the ratings indices once (read-only, outside the repo).
    fifa = FifaIndex(); fifa.load(FIFA_CSV)
    tm = TmValueIndex(); tm.load(TM_CSV)
    if not fifa.ok:
        print(f"  ! FIFA dataset not found/empty at {FIFA_CSV} — tier 1 disabled (curated+TM+heuristic only).", file=sys.stderr)
    if not tm.peak_value:
        print(f"  ! TM values not found/empty at {TM_CSV} — tier 3 disabled.", file=sys.stderr)

    # FIRST PASS over all rosters: collect each distinct player NAME with one representative
    # (nat, club, signedFrom, age) so we can resolve ONE career-best OVR per name. We prefer the
    # representative from an ELITE/STRONG club (best heuristic-floor signal) and a parseable fee.
    rep = {}        # norm(name) -> (name, nat, club, signed_from, age, score)
    def rep_score(club, signed_from):
        s = 0
        if club in ELITE: s += 3
        elif club in STRONG: s += 2
        if re.search(r"€\s*[\d.]+\s*m", signed_from or ""): s += 1
        return s

    for path in files:
        stem = os.path.splitext(os.path.basename(path))[0]
        club, year = parse_club_year(stem)
        if not club:
            continue
        with open(path, newline="", encoding="utf-8-sig") as f:
            for row in csv.DictReader(f):
                name = (row.get("name") or "").strip()
                if not name:
                    continue
                n = _norm(name)
                nat = first_nation(row.get("nationality"))
                sf = row.get("signedFrom") or ""
                age = row.get("age")
                sc = rep_score(club, sf)
                if n not in rep or sc > rep[n][5]:
                    rep[n] = (name, nat, club, sf, age, sc)

    # Resolve ONE career-best OVR per distinct player name.
    stats = defaultdict(int)
    ovr_by_name = {}                 # norm(name) -> ovr
    tier_by_name = {}                # norm(name) -> tier label
    for n, (name, nat, club, sf, age, _sc) in rep.items():
        ovr, how = resolve_ovr(name, nat, club, sf, age, fifa, tm, stats)
        ovr_by_name[n] = ovr
        tier_by_name[n] = how

    # SECOND PASS: build the squads, applying each player's career-best OVR to every season-card.
    squads = []  # (club, season, [(name, POS, ovr, nation), ...])
    total = 0
    tier_seasoncount = defaultdict(int)  # player-SEASON rows resolved by each tier
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
                n = _norm(name)
                ovr = ovr_by_name.get(n)
                if ovr is None:  # shouldn't happen, but stay safe
                    ovr = _clamp(heuristic_rating(club, row.get("signedFrom"), row.get("age"), name))
                    tier_by_name[n] = "heuristic"
                nat = first_nation(row.get("nationality"))
                players.append((name, pos, ovr, nat))
                tier_seasoncount[tier_by_name.get(n, "heuristic")] += 1
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
        "// OVR is each player's CAREER-BEST rating (one value per person, applied to every season-card):",
        "//   FIFA 15-24 career-peak, else a curated marquee value, else a Transfermarkt value band, else a heuristic floor.",
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

    # ---- report ----
    distinct = len(ovr_by_name)
    print(f"Wrote {OUT_PATH}")
    print(f"  {len(squads)} club-seasons, {total} player-seasons, {distinct} distinct players.")
    print()
    print("Tier breakdown (DISTINCT players / player-SEASONS):")
    order = ["fifa-long-exact", "fifa-short-exact", "fifa-subset-gated", "curated", "tm-value", "heuristic"]
    label = {"fifa-long-exact": "Tier1 FIFA (long exact)", "fifa-short-exact": "Tier1 FIFA (short exact)",
             "fifa-subset-gated": "Tier1 FIFA (subset gated)", "curated": "Tier2 Curated marquee",
             "tm-value": "Tier3 TM value band", "heuristic": "Tier4 Heuristic floor"}
    dtier = defaultdict(int)
    for n, t in tier_by_name.items():
        dtier[t] += 1
    for k in order:
        dn = dtier.get(k, 0); ds = tier_seasoncount.get(k, 0)
        print(f"  {label[k]:30s}: {dn:5d} players ({100*dn/max(1,distinct):4.1f}%) | {ds:6d} seasons ({100*ds/max(1,total):4.1f}%)")
    fifa_d = dtier['fifa-long-exact'] + dtier['fifa-short-exact'] + dtier['fifa-subset-gated']
    fifa_s = tier_seasoncount['fifa-long-exact'] + tier_seasoncount['fifa-short-exact'] + tier_seasoncount['fifa-subset-gated']
    print(f"  {'(all FIFA tiers)':30s}: {fifa_d:5d} players ({100*fifa_d/max(1,distinct):4.1f}%) | {fifa_s:6d} seasons ({100*fifa_s/max(1,total):4.1f}%)")

    print()
    print("OVR distribution (distinct players):")
    bands = [("90+", lambda o: o >= 90), ("85-89", lambda o: 85 <= o <= 89),
             ("80-84", lambda o: 80 <= o <= 84), ("75-79", lambda o: 75 <= o <= 79),
             ("<75", lambda o: o < 75)]
    for bname, pred in bands:
        c = sum(1 for o in ovr_by_name.values() if pred(o))
        print(f"  {bname:6s}: {c:5d} ({100*c/max(1,distinct):4.1f}%)")
    print("OVR distribution (player-seasons):")
    season_ovrs = []
    for (_c, _s, players) in squads:
        season_ovrs.extend(p[2] for p in players)
    for bname, pred in bands:
        c = sum(1 for o in season_ovrs if pred(o))
        print(f"  {bname:6s}: {c:6d} ({100*c/max(1,len(season_ovrs)):4.1f}%)")

    # 25-name spot check
    print()
    print("Spot check (25 marquee names) — career-best OVR + matching tier:")
    SPOT = ["Thierry Henry", "Alan Shearer", "Dennis Bergkamp", "Steven Gerrard", "Frank Lampard",
            "Didier Drogba", "Cristiano Ronaldo", "Wayne Rooney", "Paul Scholes", "Ryan Giggs",
            "Peter Schmeichel", "Patrick Vieira", "Roy Keane", "Gianfranco Zola", "Eric Cantona",
            "Rio Ferdinand", "John Terry", "Ashley Cole", "Ruud van Nistelrooy", "Petr Čech",
            "Vincent Kompany", "Sergio Agüero", "Luis Suárez", "Harry Kane", "Mohamed Salah"]
    for nm in SPOT:
        n = _norm(nm)
        o = ovr_by_name.get(n)
        t = tier_by_name.get(n, "—")
        if o is None:
            print(f"  {nm:24s}: NOT IN CORPUS")
        else:
            print(f"  {nm:24s}: OVR {o:3d}  [{label.get(t, t)}]")
    # Schmeichel safety assertion
    ps = ovr_by_name.get(_norm("Peter Schmeichel"))
    ks = ovr_by_name.get(_norm("Kasper Schmeichel"))
    print()
    print(f"  Schmeichel check: Peter={ps} (tier {tier_by_name.get(_norm('Peter Schmeichel'))}), "
          f"Kasper={ks} (tier {tier_by_name.get(_norm('Kasper Schmeichel'))}) — must differ.")


if __name__ == "__main__":
    main()
