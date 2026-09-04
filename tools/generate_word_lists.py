import json
import re
import unicodedata
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DATA_DIR = ROOT / "WordleItaliano" / "Data"
SOURCE_DIR = ROOT / ".tmp" / "word-sources"


SOURCE_FILES = {
    "curated": "pietro_curated.txt",
    "word_list": "pietro_word_list.txt",
    "large": "pietro_60000.txt",
    "morphit": "italian_it_it_morphit.txt",
    "napolux_large": "napolux_280000_parole_italiane.txt",
    "napolux_verbs": "napolux_coniugazione_verbi.txt",
}

PYDEPS_DIR = ROOT / ".tmp" / "pydeps"
if PYDEPS_DIR.exists():
    import sys

    sys.path.insert(0, str(PYDEPS_DIR))

try:
    from wordfreq import zipf_frequency
except ImportError:
    zipf_frequency = None


BLOCKLIST = {
    # Proper names or words that feel like proper names in play.
    "alice",
    "angela",
    "angelo",
    "anna",
    "elena",
    "giulia",
    "laura",
    "lucia",
    "luigi",
    "marco",
    "maria",
    "mario",
    "paolo",
    "piero",
    "pietro",
    "sara",
    # Reported or suspicious words.
    "crosto",
    "dosavamo",
    "osavamo",
    "posavamo",
    "scrosto",
    "tosavamo",
    # Very awkward forms found in automatic sources.
    "abata",
    "abato",
    "abavi",
    "abavo",
    "abuna",
    "acati",
    "acato",
    "acazi",
    "achea",
    "achee",
    "achei",
    "acheo",
    "actea",
    "actee",
    "adale",
    "adali",
    "addai",
    "addua",
    "addui",
    "adduo",
    "adone",
    "agata",
}


SOLUTION_EXCEPTIONS = {
    "amai",
    "assai",
    "caldo",
    "freddo",
    "feste",
    "liste",
    "meste",
    "peste",
    "quando",
    "queste",
    "teste",
    "triste",
    "veste",
}


FIVE_LETTER_SOLUTION_BAD_SUFFIXES = (
    "ava",
    "avi",
    "avo",
    "erei",
    "erai",
    "irei",
    "irai",
)


SOLUTION_BAD_SUFFIXES = (
    "ammo",
    "ando",
    "arono",
    "atemi",
    "avano",
    "avamo",
    "avate",
    "avavi",
    "avavo",
    "emmo",
    "endo",
    "erai",
    "erei",
    "eremo",
    "erete",
    "erono",
    "etemi",
    "evamo",
    "evano",
    "evate",
    "evavi",
    "evavo",
    "immo",
    "irono",
    "irei",
    "iremo",
    "irete",
    "itelo",
    "itemi",
    "ivamo",
    "ivano",
    "ivate",
    "ivavi",
    "ivavo",
    "rebbe",
    "remmo",
    "reste",
    "ssero",
    "vamo",
)


BONUS_SOLUTION_BAD_SUFFIXES = SOLUTION_BAD_SUFFIXES + (
    "asse",
    "assi",
    "aste",
    "asti",
    "endo",
    "esse",
    "essi",
    "este",
    "esti",
    "irai",
    "irei",
    "isce",
    "isci",
    "isco",
    "isse",
    "issi",
    "iste",
    "isti",
    "remo",
    "rete",
)


BONUS_SOLUTION_BAD_EXACT_SUFFIXES_BY_LENGTH = {
    6: (
        "ammo",
        "ando",
        "are",
        "ato",
        "ava",
        "avi",
        "avo",
        "ere",
        "evi",
        "evo",
        "ire",
        "ita",
        "ite",
        "iti",
        "ito",
        "iva",
        "ivi",
        "ivo",
        "uto",
    ),
    7: (
        "ando",
        "are",
        "asse",
        "asti",
        "ato",
        "ava",
        "avi",
        "avo",
        "endo",
        "ere",
        "esse",
        "esti",
        "ire",
        "isse",
        "ita",
        "ite",
        "iti",
        "ito",
        "uta",
        "ute",
        "uti",
        "uto",
    ),
}


ALPHA_RE = re.compile(r"^[a-z]+$")


def normalize(text: str) -> str:
    decomposed = unicodedata.normalize("NFD", text.strip().lower())
    without_accents = "".join(
        char for char in decomposed if unicodedata.category(char) != "Mn"
    )
    return "".join(char for char in without_accents if char.isalpha())


def load_source(name: str) -> set[str]:
    path = SOURCE_DIR / SOURCE_FILES[name]
    if not path.exists():
        raise FileNotFoundError(
            f"Missing source file: {path}. Download the wordle-it dict files first."
        )

    words: set[str] = set()
    for line in path.read_text(encoding="utf-8", errors="ignore").splitlines():
        for token in re.split(r"[^A-Za-zÀ-ÿ]+", line):
            word = normalize(token)
            if word:
                words.add(word)
    return words


def load_optional_source(name: str) -> set[str]:
    path = SOURCE_DIR / SOURCE_FILES[name]
    if not path.exists():
        return set()

    return load_source(name)


def is_basic_word(word: str, length: int) -> bool:
    return (
        len(word) == length
        and bool(ALPHA_RE.match(word))
        and word not in BLOCKLIST
        and len(set(word)) > 1
    )


def is_accepted_word(word: str, length: int) -> bool:
    return is_basic_word(word, length)


def is_daily_solution(word: str) -> bool:
    if not is_accepted_word(word, 5):
        return False
    if italian_frequency(word) < 2.45:
        return False
    if word in SOLUTION_EXCEPTIONS:
        return True
    if word.endswith(SOLUTION_BAD_SUFFIXES):
        return False
    if word.endswith(FIVE_LETTER_SOLUTION_BAD_SUFFIXES):
        return False
    if word.endswith(("ai", "ii")):
        return False
    return True


def is_bonus_solution(word: str, length: int) -> bool:
    if not is_accepted_word(word, length):
        return False
    if italian_frequency(word) < (2.65 if length == 6 else 2.75):
        return False
    if word in SOLUTION_EXCEPTIONS:
        return True
    if word.endswith(BONUS_SOLUTION_BAD_SUFFIXES):
        return False
    if word.endswith(BONUS_SOLUTION_BAD_EXACT_SUFFIXES_BY_LENGTH[length]):
        return False
    return True


def italian_frequency(word: str) -> float:
    if zipf_frequency is None:
        return 99.0
    return zipf_frequency(word, "it")


def write_json(name: str, words: list[str]) -> None:
    (DATA_DIR / name).write_text(
        json.dumps(words, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def main() -> None:
    curated = load_source("curated")
    word_list = load_source("word_list")
    large = load_source("large")
    accepted_extra_sources = (
        load_optional_source("morphit")
        | load_optional_source("napolux_large")
        | load_optional_source("napolux_verbs")
    )

    valid_5 = sorted(
        word for word in word_list | large | accepted_extra_sources if is_accepted_word(word, 5)
    )
    valid_6 = sorted(word for word in large | accepted_extra_sources if is_accepted_word(word, 6))
    valid_7 = sorted(word for word in large | accepted_extra_sources if is_accepted_word(word, 7))

    valid_5_set = set(valid_5)
    daily_5 = sorted(
        word for word in curated if is_daily_solution(word) and word in valid_5_set
    )
    bonus_5 = daily_5[:]
    bonus_6 = sorted(word for word in large if is_bonus_solution(word, 6))
    bonus_7 = sorted(word for word in large if is_bonus_solution(word, 7))

    outputs = {
        "validWords.json": valid_5,
        "validWords6.json": valid_6,
        "validWords7.json": valid_7,
        "dailyWords.json": daily_5,
        "bonusWords5.json": bonus_5,
        "bonusWords6.json": bonus_6,
        "bonusWords7.json": bonus_7,
    }

    for filename, words in outputs.items():
        write_json(filename, words)
        print(f"{filename}: {len(words)}")

    for word in ("maria", "mario", "crosto", "osavamo", "amore", "cuore", "pizza"):
        memberships = [
            filename for filename, words in outputs.items() if word in set(words)
        ]
        print(f"{word}: {', '.join(memberships) if memberships else 'excluded'}")


if __name__ == "__main__":
    main()
