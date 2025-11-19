# Skrypt autorstwa DmitryChevich
# Synchronizuje pliki lokalizacji en-US z pl-PL
# Alternatywne podejście do Tools/translationTools/keyfinder.py

import shutil
from pathlib import Path

from fluent.syntax import FluentParser, FluentSerializer
from fluent.syntax.ast import Message

BASE_DIR = Path(__file__).resolve().parent.parent
SRC = BASE_DIR.parent / "Resources" / "Locale" / "en-US"
DST = BASE_DIR.parent / "Resources" / "Locale" / "pl-PL"

parser = FluentParser()
serializer = FluentSerializer(with_junk=True)


def adjust_conflict_path(dst_file: Path, dst_root: Path) -> Path:
    """
    Rozwiązuje konflikt plik/katalog:
    - jeśli w katalogu docelowym istnieje plik tam, gdzie w en-US jest katalog,
      plik zostaje nietknięty, ale wypisywane jest ostrzeżenie.
    - w pozostałych przypadkach brak katalogu jest uzupełniany przez mkdir().
    """
    try:
        dst_root_resolved = dst_root.resolve()
        parents_stack = []
        cur = dst_file.parent.resolve()
        while True:
            parents_stack.append(cur)
            if cur == dst_root_resolved or cur == cur.parent:
                break
            cur = cur.parent

        for ancestor in reversed(parents_stack):
            if not str(ancestor).startswith(str(dst_root_resolved)):
                continue

            if ancestor.exists():
                if ancestor.is_file():
                    # nie przenosimy pliku, zostawiamy go i tylko ostrzegamy
                    print(f"[OSTRZEŻENIE] W katalogu '{ancestor.parent}' istnieje plik "
                          f"'{ancestor.name}', ale w en-US to katalog. Plik został zachowany.")
                    return dst_file
            else:
                try:
                    ancestor.mkdir()
                except Exception as e:
                    print(f"[BŁĄD] Nie udało się utworzyć katalogu '{ancestor}': {e}")
                    return dst_file

    except Exception as e:
        print(f"[BŁĄD adjust_conflict_path] {e}")
        return dst_file

    return dst_file


def sync_locales(src: Path, dst: Path):
    """
    Kopiuje wszystkie pliki .ftl z katalogu en-US do pl-PL,
    odtwarzając dokładną strukturę katalogów.
    Wypisuje tylko, gdy znajdzie nowy plik i go skopiuje.
    """
    for src_path in src.rglob("*"):
        if not (src_path.is_file() and src_path.suffix == ".ftl"):
            continue

        rel_path = src_path.relative_to(src)
        dst_file = dst / rel_path

        dst_file = adjust_conflict_path(dst_file, dst)

        if dst_file.exists() and dst_file.is_file():
            continue

        try:
            dst_file.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src_path, dst_file)
            print(f"[NOWY PLIK] Skopiowano: {dst_file}")
        except Exception as e:
            print(f"[BŁĄD] Nie udało się skopiować '{src_path}' do '{dst_file}': {e}")
    print("Struktura poprawna")


def sync_missing_keys(src: Path, dst: Path):
    """
    Dla każdego pliku .ftl z en-US sprawdza odpowiedni plik w pl-PL.
    Jeśli w en-US są klucze, których brakuje w pl-PL → dodaje je.
    Wypisuje tylko, gdy dodano brakujące klucze.
    """
    for src_path in src.rglob("*"):
        if not (src_path.is_file() and src_path.suffix == ".ftl"):
            continue

        rel = src_path.relative_to(src)
        dst_file = dst / rel

        dst_file = adjust_conflict_path(dst_file, dst)

        if not dst_file.exists():
            continue

        try:
            src_text = src_path.read_text(encoding="utf-8")
            src_ast = parser.parse(src_text)
            keys_en = {e.id.name: e for e in src_ast.body if isinstance(e, Message)}
        except Exception as e:
            print(f"[BŁĄD] Odczyt/parsowanie '{src_path}': {e}")
            continue

        try:
            dst_text = dst_file.read_text(encoding="utf-8")
            dst_ast = parser.parse(dst_text)
            keys_pl = {e.id.name: e for e in dst_ast.body if isinstance(e, Message)}
        except Exception as e:
            print(f"[BŁĄD] Odczyt/parsowanie '{dst_file}': {e}")
            continue

        missing = [k for k in keys_en if k not in keys_pl]
        if not missing:
            continue

        for key in missing:
            dst_ast.body.append(keys_en[key])
            print(f"[BRAKUJĄCY KLUCZ] Dodano '{key}' do {dst_file}")

        try:
            dst_file.write_text(serializer.serialize(dst_ast), encoding="utf-8")
        except Exception as e:
            print(f"[BŁĄD] Zapis '{dst_file}': {e}")
    print("Klucze poprawne")


if __name__ == "__main__":
    sync_locales(SRC, DST)
    sync_missing_keys(SRC, DST)
