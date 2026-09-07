import argparse
import gzip
import json
import re
import unicodedata
from pathlib import Path


SOLUTION_FILES = (
    "dailyWords.json",
    "bonusWords5.json",
    "bonusWords6.json",
    "bonusWords7.json",
)

POS_LABELS = {
    "adj": "aggettivo",
    "adv": "avverbio",
    "conj": "congiunzione",
    "interj": "interiezione",
    "name": "nome proprio",
    "noun": "sostantivo",
    "num": "numero",
    "prep": "preposizione",
    "pron": "pronome",
    "verb": "verbo",
}

BAD_GLOSS_PARTS = (
    "approfondimento",
    "citazioni",
    "definizione mancante",
    "etimologia",
    "con flemma",
    "per la coniugazione",
    "pronuncia",
)

PENALIZED_TAGS = {
    "archaic",
    "broadly",
    "colloquial",
    "dated",
    "dialectal",
    "figuratively",
    "historical",
    "informal",
    "literally",
    "obsolete",
    "rare",
    "regional",
    "slang",
}

PENALIZED_RAW_TAGS = {
    "antico",
    "arcaico",
    "dialettale",
    "familiare",
    "figurato",
    "gergale",
    "letterario",
    "obsoleto",
    "popolare",
    "regionale",
    "raro",
}

TOPIC_PENALTIES = {
    "anatomy": 28,
    "biology": 18,
    "Christianity": 14,
    "commerce": 14,
    "electronics": 6,
    "engineering": 12,
    "geometry": 18,
    "heraldry": 24,
    "law": 16,
    "mathematics": 18,
    "medicine": 28,
    "music": 8,
    "navy": 24,
    "pharmacology": 10,
    "physics": 18,
    "physiology": 24,
    "religion": 14,
    "soccer": 8,
    "sports": 8,
    "statistics": 18,
    "technology": 8,
    "weaponry": 18,
}

CATEGORY_PENALTIES = {
    "Anatomia": 28,
    "Araldica": 24,
    "Armi": 18,
    "Biologia": 18,
    "Commercio": 14,
    "Cristianesimo": 14,
    "Fisica": 18,
    "Fisiologia": 24,
    "Geometria": 18,
    "Elementi chimici": 30,
    "Marina": 24,
    "Matematica": 18,
    "Medicina": 28,
    "Parole antiche": 50,
    "Parole disusate": 50,
    "Religione": 14,
    "Statistica": 18,
}

POS_PENALTIES = {
    "sostantivo": 0,
    "verbo": 1,
    "aggettivo": 2,
    "avverbio": 3,
}

FORM_GLOSS_RE = re.compile(
    r"^(?:forma|voce|prima|seconda|terza|femminile|maschile|plurale|singolare|"
    r"participio|gerundio|imperativo|indicativo|congiuntivo|condizionale|"
    r"futuro|presente|passato|imperfetto).+? di ([A-Za-zÀ-ÿ']+)",
    re.IGNORECASE,
)


def normalize(text: str) -> str:
    normalized = unicodedata.normalize("NFD", text.strip().lower())
    letters = [
        char
        for char in normalized
        if unicodedata.category(char) != "Mn" and char.isalpha()
    ]
    return unicodedata.normalize("NFC", "".join(letters))


def clean_text(text: str | None) -> str:
    if not text:
        return ""

    cleaned = re.sub(r"\s+", " ", text).strip()
    cleaned = cleaned.replace("�", "e'")
    cleaned = cleaned.strip(" ;")
    if not cleaned:
        return ""

    lowered = cleaned.lower()
    if any(part in lowered for part in BAD_GLOSS_PARTS):
        return ""

    return cleaned[:1].upper() + cleaned[1:]


def load_solution_words(data_dir: Path) -> set[str]:
    words: set[str] = set()
    for file_name in SOLUTION_FILES:
        path = data_dir / file_name
        values = json.loads(path.read_text(encoding="utf-8"))
        words.update(normalize(value) for value in values)
    return words


def extract_lemma_from_gloss(gloss: str) -> str:
    match = FORM_GLOSS_RE.match(gloss)
    return normalize(match.group(1)) if match else ""


def tag_description(tags: list[str]) -> str:
    labels = {
        "feminine": "femminile",
        "masculine": "maschile",
        "plural": "plurale",
        "singular": "singolare",
    }
    return " ".join(labels[tag] for tag in tags if tag in labels)


def metadata_penalty(
    tags: list[str] | None,
    raw_tags: list[str] | None,
    topics: list[str] | None,
    categories: list[str] | None,
) -> int:
    penalty = 0
    tag_set = set(tags or [])
    raw_tag_set = {tag.lower() for tag in raw_tags or []}
    if tag_set & PENALIZED_TAGS:
        penalty += 50
    if raw_tag_set & PENALIZED_RAW_TAGS:
        penalty += 35

    for topic in topics or []:
        penalty += TOPIC_PENALTIES.get(topic, 0)

    for category in categories or []:
        for prefix, value in CATEGORY_PENALTIES.items():
            if category.startswith(prefix):
                penalty += value
                break

    return penalty


def definition_score(entry: dict[str, object]) -> int:
    part_of_speech = str(entry.get("partOfSpeech", ""))
    definition = str(entry.get("definition", "")).lower()
    score = POS_PENALTIES.get(part_of_speech, 4)
    score += int(entry.get("order", 0))
    score += int(entry.get("metadataPenalty", 0))
    if definition.startswith("sinonimo di"):
        score += 20
    if "elemento chimico" in definition:
        score += 30
    if "pistola" in definition or "arma" in definition or "armi da fuoco" in definition:
        score += 20
    if "pallone" in definition or "sport di squadra" in definition:
        score -= 12
    if "apparecchio elettronico" in definition or "trasmissioni radiofoniche" in definition:
        score -= 8
    return score


def best_entries(
    word: str,
    direct: dict[str, list[dict[str, object]]],
    preferred_part_of_speech: str = "",
) -> list[dict[str, object]]:
    entries = direct.get(word) or []
    if preferred_part_of_speech:
        preferred = [
            entry
            for entry in entries
            if entry.get("partOfSpeech") == preferred_part_of_speech
        ]
        if preferred:
            entries = preferred

    if not entries:
        return []

    ranked = sorted(entries, key=definition_score)
    good = [
        entry
        for entry in ranked
        if definition_score(entry) < 45
        and not str(entry.get("definition", "")).lower().startswith("sinonimo di")
    ]
    chosen = good[:3] if good else ranked[:1]
    return chosen


def parse_kaikki(path: Path):
    direct: dict[str, list[dict[str, str]]] = {}
    form_refs: dict[str, list[dict[str, object]]] = {}
    reverse_forms: dict[str, list[dict[str, object]]] = {}

    with gzip.open(path, "rt", encoding="utf-8") as stream:
        for line in stream:
            item = json.loads(line)
            if item.get("lang_code") != "it":
                continue

            word = normalize(item.get("word", ""))
            if not word:
                continue

            pos = item.get("pos") or ""
            pos_label = POS_LABELS.get(pos, pos or "parola")
            direct_entries: list[dict[str, str]] = []

            for sense_index, sense in enumerate(item.get("senses") or [], 1):
                glosses = [
                    clean_text(gloss)
                    for gloss in sense.get("glosses") or []
                ]
                glosses = [gloss for gloss in glosses if gloss]
                if not glosses:
                    continue

                examples = [
                    clean_text(example.get("text"))
                    for example in sense.get("examples") or []
                    if isinstance(example, dict)
                ]
                examples = [example for example in examples if example]

                refs = [
                    normalize(ref.get("word", ""))
                    for ref in sense.get("form_of") or []
                    if isinstance(ref, dict) and ref.get("word")
                ]
                refs = [ref for ref in refs if ref]
                inferred_ref = extract_lemma_from_gloss(glosses[0])
                if inferred_ref and inferred_ref not in refs:
                    refs.append(inferred_ref)

                is_form = bool(refs) or "form-of" in (sense.get("tags") or [])
                if is_form:
                    if refs:
                        form_refs.setdefault(word, []).append(
                            {
                                "partOfSpeech": pos_label,
                                "relation": glosses[0],
                                "lemmas": refs,
                            }
                        )
                    continue

                entry = {
                    "partOfSpeech": pos_label,
                    "definition": glosses[0],
                    "order": sense_index,
                    "metadataPenalty": metadata_penalty(
                        sense.get("tags"),
                        sense.get("raw_tags"),
                        sense.get("topics"),
                        sense.get("categories"),
                    ),
                }
                if examples:
                    entry["example"] = examples[0]
                direct_entries.append(entry)

            if direct_entries:
                direct.setdefault(word, []).extend(direct_entries)
                for form in item.get("forms") or []:
                    if not isinstance(form, dict):
                        continue

                    form_word = normalize(form.get("form", ""))
                    if form_word and form_word != word:
                        reverse_forms.setdefault(form_word, []).append(
                            {
                                "partOfSpeech": pos_label,
                                "lemma": word,
                                "tags": form.get("tags") or [],
                            }
                        )

    return direct, form_refs, reverse_forms


def build_definition(
    word: str,
    direct: dict[str, list[dict[str, object]]],
    form_refs: dict[str, list[dict[str, object]]],
    reverse_forms: dict[str, list[dict[str, object]]],
):
    direct_entries = best_entries(word, direct)
    if direct_entries:
        direct_entry = direct_entries[0]
        definitions = [str(entry["definition"]) for entry in direct_entries]
        return {
            "word": word,
            "displayWord": word.upper(),
            "partOfSpeech": str(direct_entry["partOfSpeech"]),
            "definition": str(direct_entry["definition"]),
            "definitions": definitions,
            **({"example": direct_entry["example"]} if "example" in direct_entry else {}),
            "source": "Wikizionario tramite Kaikki/Wiktextract",
        }, "direct"

    for ref in form_refs.get(word) or []:
        for lemma in ref["lemmas"]:
            lemma_entries = best_entries(
                lemma,
                direct,
                str(ref["partOfSpeech"]),
            )
            if not lemma_entries:
                continue

            lemma_entry = lemma_entries[0]
            definitions = [str(entry["definition"]) for entry in lemma_entries]
            return {
                "word": word,
                "displayWord": word.upper(),
                "partOfSpeech": f"{ref['partOfSpeech']} · {ref['relation'].lower()}",
                "definition": str(lemma_entry["definition"]),
                "definitions": definitions,
                **({"example": lemma_entry["example"]} if "example" in lemma_entry else {}),
                "source": "Wikizionario tramite Kaikki/Wiktextract",
            }, "form-ref"

    for form in reverse_forms.get(word) or []:
        lemma = str(form["lemma"])
        lemma_entries = best_entries(
            lemma,
            direct,
            str(form["partOfSpeech"]),
        )
        if not lemma_entries:
            continue

        lemma_entry = lemma_entries[0]
        definitions = [str(entry["definition"]) for entry in lemma_entries]
        description = tag_description(form.get("tags", []))
        relation = f"{description} di {lemma}" if description else f"forma di {lemma}"
        return {
            "word": word,
            "displayWord": word.upper(),
            "partOfSpeech": f"{form['partOfSpeech']} · {relation}",
            "definition": str(lemma_entry["definition"]),
            "definitions": definitions,
            **({"example": lemma_entry["example"]} if "example" in lemma_entry else {}),
            "source": "Wikizionario tramite Kaikki/Wiktextract",
        }, "reverse-form"

    return None, "missing"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--kaikki", required=True, type=Path)
    parser.add_argument("--data-dir", default=Path("WordleItaliano/Data"), type=Path)
    parser.add_argument("--output", default=Path("WordleItaliano/Data/definitions.json"), type=Path)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    solution_words = load_solution_words(args.data_dir)
    direct, form_refs, reverse_forms = parse_kaikki(args.kaikki)

    definitions: dict[str, dict[str, str]] = {}
    counts = {"direct": 0, "form-ref": 0, "reverse-form": 0, "missing": 0}
    missing: list[str] = []

    for word in sorted(solution_words):
        definition, kind = build_definition(word, direct, form_refs, reverse_forms)
        counts[kind] += 1
        if definition is None:
            missing.append(word)
            continue

        definitions[word] = definition

    covered = len(definitions)
    coverage = covered * 100 / len(solution_words) if solution_words else 0
    print(f"Pool soluzioni: {len(solution_words)}")
    print(f"Coperte: {covered} ({coverage:.2f}%)")
    print(f"Dirette: {counts['direct']}")
    print(f"Forme risolte da form_of/glossa: {counts['form-ref']}")
    print(f"Forme risolte dal lemma: {counts['reverse-form']}")
    print(f"Non trovate: {counts['missing']}")
    print("Esempi non trovati: " + ", ".join(missing[:80]))

    for sample in ("mesta", "meste", "mesti", "case"):
        if sample in definitions:
            item = definitions[sample]
            print(f"{sample.upper()}: {item['partOfSpeech']} | {item['definition']}")

    if args.dry_run:
        return

    args.output.write_text(
        json.dumps(definitions, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
