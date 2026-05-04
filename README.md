<div class="header" align="center">
<img alt="Space Station 14" width="880" height="300" src="https://raw.githubusercontent.com/space-wizards/asset-dump/de329a7898bb716b9d5ba9a0cd07f38e61f1ed05/github-logo.svg">
</div>

Space Station 14 to remake SS13 działający na [Robust Toolbox](https://github.com/space-wizards/RobustToolbox), silniku napisanym w C#.

To jest polska wersja gry, oparta na repozytorium [Funky Station](https://github.com/funky-station/funky-station).

Aby zapobiec forkom RobustToolbox, klient i serwer ładują paczkę "Content". Ta paczka zawiera wszystko, co potrzebne do gry na jednym konkretnym serwerze.

Jeśli chcesz hostować lub tworzyć zawartość dla SS14, to właśnie to repozytorium jest ci potrzebne. Zawiera zarówno RobustToolbox, jak i paczkę content do rozwoju nowych paczek zawartości.

## Linki

<div align="center">

[Strona WWW](https://ss14.pl/) | [Discord](https://discord.ss14.pl/) | [Wiki](https://wiki.ss14.pl/) | [Pobierz grę](https://ss14.pl/#download)

</div>

## Dokumentacja/Wiki

[Strona z dokumentacją autorstwa Wizden](https://docs.spacestation14.com/) zawiera informacje w języku angielskim o zawartości SS14, silniku, projektowaniu gry i nie tylko.
Dodatkowo, zobacz te zasoby dotyczące licencji i atrybucji:
- [Robust Generic Attribution](https://docs.spacestation14.com/en/specifications/robust-generic-attribution.html)
- [Robust Station Image](https://docs.spacestation14.com/en/specifications/robust-station-image.html)

## Wkład w projekt

[Polonizujemy nasz projekt na platformie Crowdin!](https://crowdin.com/project/space-station-14-polska) Wesprzyj nas w tłumaczeniach i odbierz unikalną rolę na Discordzie.

---

<div align="center">
  <a href="https://crowdin.com/project/space-station-14-polska">
    <img src="https://badges.crowdin.net/space-station-14-polska/localized.svg" alt="Crowdin">
  </a>
</div>

---

Chętnie przyjmujemy wkład od każdego. Dołącz na Discord, jeśli chcesz pomóc. Mamy [listę zadań](https://github.com/polonium14/polonium-station/issues), które trzeba wykonać, i każdy może je podjąć. Nie bój się pytać o pomoc!

> [!TIP]
> Zalecamy zapoznanie się z **[wytycznymi dotyczącymi kontrybucji](https://github.com/polonium14/polonium-station/blob/master/CONTRIBUTING.md)**, ponieważ zawierają one wiele przydatnych informacji dla nowych kontrybutorów.

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
