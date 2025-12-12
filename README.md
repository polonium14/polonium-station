<div class="header" align="center">
<img alt="Space Station 14" width="880" height="300" src="https://raw.githubusercontent.com/space-wizards/asset-dump/de329a7898bb716b9d5ba9a0cd07f38e61f1ed05/github-logo.svg">
</div>

Space Station 14 to remake SS13 działający na [Robust Toolbox](https://github.com/space-wizards/RobustToolbox), naszym autorskim silniku napisanym w C#.

To jest polska wersja gry, oparta na repozytorium [Funky Station](https://github.com/funky-station/funky-station).

Aby zapobiec forkom RobustToolbox, klient i serwer ładują paczkę "content". Ta paczka zawiera wszystko, co potrzebne do gry na jednym konkretnym serwerze.

Jeśli chcesz hostować lub tworzyć zawartość dla SS14, to właśnie to repozytorium jest ci potrzebne. Zawiera zarówno RobustToolbox, jak i paczkę content do rozwoju nowych paczek zawartości.

## Linki

<div class="header" align="center">

[Strona WWW](https://spacestation14.com/) | [Discord](https://discord.ss14.io/) | [Forum](https://forum.spacestation14.com/) | [Mastodon](https://mastodon.gamedev.place/@spacestation14) | [Lemmy](https://lemmy.spacestation14.com/) | [Patreon](https://www.patreon.com/spacestation14) | [Steam](https://store.steampowered.com/app/1255460/Space_Station_14/) | [Pobierz wersję standalone](https://spacestation14.com/about/nightlies/)

</div>

## Dokumentacja/Wiki

Nasza [strona z dokumentacją](https://docs.spacestation14.com/) zawiera informacje o zawartości SS14, silniku, projektowaniu gry i nie tylko.
Dodatkowo, zobacz te zasoby dotyczące licencji i atrybucji:
- [Robust Generic Attribution](https://docs.spacestation14.com/en/specifications/robust-generic-attribution.html)
- [Robust Station Image](https://docs.spacestation14.com/en/specifications/robust-station-image.html)

Mamy też wiele materiałów dla nowych kontrybutorów projektu.

## Wkład w projekt

Chętnie przyjmujemy wkład od każdego. Dołącz na Discord, jeśli chcesz pomóc. Mamy [listę zadań](https://github.com/space-wizards/space-station-14-content/issues), które trzeba wykonać, i każdy może je podjąć. Nie bój się pytać o pomoc!
Upewnij się tylko, że twoje zmiany i pull requesty są zgodne z [wytycznymi dotyczącymi kontrybucji](https://docs.spacestation14.com/en/general-development/codebase-info/pull-request-guidelines.html).

Obecnie nie przyjmujemy tłumaczeń gry w naszym głównym repozytorium. Jeśli chcesz przetłumaczyć grę na inny język, rozważ stworzenie forka lub kontrybuowanie do istniejącego forka.

## Budowanie

1. Sklonuj repozytorium:
```shell
git clone https://github.com/polonium14/polonium-station.git
```
2. Przejdź do folderu projektu i uruchom `RUN_THIS.py`, aby zainicjalizować submoduły i załadować silnik:
```shell
cd space-station-14
python RUN_THIS.py
```
3. Skompiluj rozwiązanie:

Zbuduj serwer używając `dotnet build`.

[Dokładniejsze instrukcje dotyczące budowania projektu.](https://docs.spacestation14.com/en/general-development/setup.html)

## Licencja

Repozytorium jest objęte licencją AGPL-3.0, jednak niektóre pliki są udostępniane na licencji MIT. Pełne treści obu licencji znajdują się w katalogu [LICENSES/](https://github.com/polonium14/polonium-station-14/blob/master/LICENSES).

Większość assetów jest również licencjonowana na [CC-BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/), chyba że zaznaczono inaczej. Assety mają swoją licencję i prawa autorskie określone w pliku metadata. Na przykład zobacz [metadatę dla łomu](https://github.com/space-wizards/space-station-14/blob/master/Resources/Textures/Objects/Tools/crowbar.rsi/meta.json).

> [!NOTE]
> Niektóre assety są licencjonowane na zasadach niekomercyjnych, takich jak [CC-BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/) lub podobnych. Należy je usunąć, jeśli chcesz używać tego projektu komercyjnie.
