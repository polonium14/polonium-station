# **Ogólne konwencje prowadzenia bazy kodowej projektu:**

#### 1. Jeden temat zmian = jeden PR (Pull Request)

Zezwalamy na łączenie w jednym PR niewielkich, powiązanych ze sobą zmian w kodzie (o ile każda z nich zostanie opisana w treści PR) lub tłumaczeń gry. Wymagamy jednak, aby większe zmiany były rozdzielane na osobne PR-y.

Pełną treść wytycznych dotyczących PR można znaleźć [niżej](#wytyczne-dotyczące-pull-requestów).

#### 2. Nie duplikuj kodu

Jak patrzysz na jakiś fragment kodu i myślisz sobie "Chcę, żeby mój kod robił to samo", to lepiej wydziel ten fragment do osobnej metody albo w inną formę abstrakcji, która umożliwi Ci ponownie wykorzystać go w miarę możliwości.

Są jednak sytuacje, w których nie da się uniknąć kopiowania. Na przykład: niektóre zależności, blok `override void Initialize()`, czy konstrukcja bazowa przy tworzeniu tzw. `EntitySystem`, która zawsze jest definicją klasy.

#### 3. Trzymaj się określonego stylu kodowania

- `PascalCase` dla klas, struktur, interfejsów, zdarzeń, metod, właściwości, stałych, pól publicznych i pól `static readonly` (także prywatnych)
- `camelCase` dla zmiennych lokalnych i parametrów funkcji
- `_camelCase` dla pozostałych pól prywatnych, np. wstrzykiwanych zależności ([Dependency Injection](https://docs.spacestation14.com/en/robust-toolbox/ioc.html)) albo wartości CVarów zapamiętanych w systemie:

```csharp
public abstract partial class SharedStaminaSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    // ...
}
```

Te zasady opisuje też plik `.editorconfig`, więc IDE samo podpowie błędną nazwę.

#### 4. Komentuj swój kod

Zostawiaj komentarze w kodzie, szczególnie w miejscach, które nie są oczywiste na pierwszy rzut oka. Pisz w nich, _co_ robi kod, a przede wszystkim _dlaczego_ kod robi to.

W przypadkach klas, struktur, metod, pól/właściwości i członków klasy korzystaj z [dokumentacji XML](https://learn.microsoft.com/pl-pl/dotnet/csharp/language-reference/xmldoc/). Wszystkie publiczne metody i konstrukcje oznaczone atrybutem `DataField` zawsze powinny być udokumentowane.

#### 5. Unikaj "magicznych" wartości

Są to wartości, które są wpisane na sztywno w kodzie i które muszą zgadzać się z identycznymi wartościami w innych jego miejscach. Jeśli zmienisz tę wartość w jednym miejscu, ale zapomnisz o innych - program może działać niepoprawnie.

Zamiast tego, w prostych sytuacjach należy wydzielać takie wartości do stałych oznaczonych modyfikatorami `const` lub `static readonly`. W bardziej złożonych sytuacjach należy zadbać o to, by te wartości nie mogły się różnić - można to osiągnąć np. poprzez wyjątki, testy jednostkowe lub inne mechanizmy sprawdzające. Najważniejsze jest, aby ewentualny błąd był natychmiast zauważalny.

#### 6. Prawidłowo porządkuj elementy w typie

Podczas deklarowania elementów składowych w typie, zawsze należy umieszczać pola i właściwości nad pozostałymi elementami. Ułatwia to zrozumienie, na jakich danych operuje typ. Pomieszanie pól i innych członków utrudnia czytelność i orientację w kodzie.

Zły przykład:

```csharp
public class Player
{
    public void Move() { /* ... */ }

    private int _health;

    public Player(string name)
    {
        Name = name;
    }

    public string Name { get; private set; }

    public void TakeDamage(int amount)
    {
        _health -= amount;
    }

    private const int MaxHealth = 100;
}
```

Dobry przykład:

```csharp
public sealed class Player
{
    // Stałe
    private const int MaxHealth = 100;

    // Pola prywatne
    private int _health;

    // Właściwości
    public string Name { get; private set; }

    // Konstruktor
    public Player(string name)
    {
        Name = name;
    }

    // Metody publiczne
    public void Move() { /* ... */ }

    public void TakeDamage(int amount)
    {
        _health -= amount;
    }
}
```

#### 7. Również należy się trzymać

- [typowych konwencji kodowania w języku C#](http://learn.microsoft.com/pl-pl/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [konwencji przyjętych przez Wizardów](https://docs.spacestation14.com/en/general-development/codebase-info/conventions.html)

#### 8. Lokalizacja

Gra jest domyślnie po polsku, a źródłem wszystkich tekstów jest angielski. Dlatego:

- **Każdy tekst widoczny dla gracza** (popupy, interfejs, komunikaty, opisy) trafia do plików `.ftl` i jest pobierany przez `Loc.GetString`. W kodzie C# i w YAML nie wpisujemy tekstów na sztywno. Wyjątkiem są angielskie pola `name`, `description` i `suffix` encji, z których [skrypty](#4-szybkie-generowanie-tłumaczeń-translationbat--translationsh) generują pliki `.ftl`, oraz treść stron podręcznika, pisana po polsku w plikach XML (zobacz [konwencję 10](#10-opisuj-nowości-w-podręczniku)).
- **Nowe klucze dodajesz w tym samym PR do `en-US` i `pl-PL`** (treści Polonium do folderów `_Polonium`). Bez wersji angielskiej klucz nie trafi do narzędzi tłumaczeniowych, a bez polskiej gracze zobaczą angielski tekst.
- **Nie składaj zdań z kawałków w kodzie.** Jeden komunikat to jeden klucz ze zmiennymi, a liczbę mnogą i rodzaj obsługuje plik `.ftl` (np. `[one]`, `[few]`, `*[other]`). Po polsku sklejane zdania prawie zawsze wychodzą niegramatycznie.
- **Istniejące tłumaczenia poprawiamy na [Crowdinie](https://crowdin.com/project/space-station-14-polska)**, a nie w plikach repozytorium. Do gry trafiają tylko tłumaczenia zatwierdzone przez korektorów, a zmiana w repozytorium omija korektę i łatwo koliduje z automatycznymi PR-ami z Crowdina. W repozytorium tłumaczymy tylko pliki wyłączone z Crowdina (m.in. foldery `_Polonium`, `datasets`, `accent`, `chat` i `speech`, pełna lista w [crowdin.yml](crowdin.yml)), w osobnym PR, bez mieszania z innymi zmianami.
- **Trzymaj się ustalonych terminów** z [glosariusza](glossary.csv), np. Komendant (Head of Security) czy Nadzorca Ochrony (Warden). Zmienne, selektory i znaczniki (`{ $user }`, `[color=...]`) zostawiaj dokładnie takie jak w oryginale.
- **Pliki `.ftl` zapisuj w UTF-8 bez BOM.** Z BOM-em Crowdin gubi pierwszy klucz w pliku.

Więcej o samym tłumaczeniu znajdziesz w [FAQ](#3-jak-tłumaczyć-grę).

#### 9. Zmiany w plikach upstreamu i porty

Polonium bazuje na kodzie Wizard's Den i innych forków, a ich zmiany trafiają do nas przy aktualizacjach. Każda nasza zmiana w cudzym pliku może wtedy wywołać konflikt, dlatego:

- **Nowy kod i zasoby umieszczaj w folderach `_Polonium`** (np. `Content.Shared/_Polonium/`, `Resources/Prototypes/_Polonium/`, `Resources/Textures/_Polonium/`). Od razu widać, co jest nasze, a aktualizacje tych folderów nie dotykają.
- **Pliki spoza `_Polonium` zmieniaj tylko wtedy, gdy nie da się inaczej.** Najpierw sprawdź, czy nie wystarczy event, nowy komponent, `partial class` albo własny prototyp dziedziczący po oryginalnym (`parent`).
- **Każdą zmianę w cudzym pliku oznacz komentarzem ze słowem `Polonium` i powodem**, np. `// Polonium: longer time, because we have a small online`. Dłuższy blok zamknij komentarzem `// Polonium end`. Przy aktualizacji od razu widać, co zmieniliśmy i dlaczego.
- **Nie poprawiaj stylu ani nie porządkuj kodu z upstreamu**, nawet jeśli da się go napisać ładniej. Graczom nic to nie daje, a konflikty wracają przy każdej aktualizacji.
- **Nie zmieniaj submodułu `RobustToolbox`.** Silnik aktualizują maintainerzy w osobnym PR, a automatyczne sprawdzenie odrzuca PR, który zmienia submoduł razem z innymi plikami.

Przenosząc funkcję z innego forka:

- **Sprawdź licencję** i upewnij się, że pozwala na przeniesienie. W razie wątpliwości zapytaj na Discordzie.
- **Podaj źródło**: w opisie PR link do repozytorium i oryginalnego PR lub commita, a przy grafikach i dźwiękach prawdziwych autorów w `meta.json` lub `attributions.yml`. Zachowaj nagłówki licencyjne przenoszonych plików. Port bez atrybucji narusza licencję i nie zostanie przyjęty.
- **Przenieś wszystko, czego funkcja potrzebuje, i nic ponad to**: zależności, prototypy i teksty (przeniesione do `.ftl` w `en-US` i `pl-PL`), ale bez nieużywanych plików i resztek obcego forka.
- **Dostosuj funkcję do naszego serwera.** Liczby z innego forka często zakładają inną populację i inny balans.

#### 10. Opisuj nowości w podręczniku

Podręcznik (guidebook) to dla graczy główne źródło wiedzy o grze. Jeśli dodajesz coś, co pasuje do któregoś z jego działów, **opisz to w podręczniku w tym samym PR**. Dotyczy to m.in. nowych antagonistów i ról, odczynników chemicznych, maszyn (np. inżynieryjnych czy medycznych) i nowych mechanik. Bez opisu gracze nie dowiedzą się, że nowość istnieje ani jak z niej korzystać. Antagonista bez strony w podręczniku nie zostanie przyjęty.

- **Strony Polonium** to dokumenty XML po polsku w `Resources/ServerInfo/_Polonium/Guidebook/`. Często wystarczy dopisać nowość na istniejącej stronie działu, np. maszynę przez `<GuideEntityEmbed Entity="IdPrototypu"/>` z krótkim opisem.
- **Odczynniki i przepisy kuchenne** pojawiają się w podręczniku automatycznie według pola `group`. Wystarczy nadać im grupę, którą podręcznik wyświetla (np. `Medicine` albo `Toxins`), i dobrze je opisać.
- **Nową stronę** dodajesz prototypem `guideEntry` (nazwa jako klucz FTL w `en-US` i `pl-PL`) i dopisujesz ją do `children` strony nadrzędnej.
- **Komponent `GuideHelp`** na encji pozwala graczowi otworzyć właściwą stronę prosto z gry.

Nie wiesz, gdzie opisać swoją zmianę? Zapytaj na Discordzie.

# Wytyczne dotyczące pull requestów

## Zasady ogólne

#### 0. Ogólny przebieg pracy nad zmianami

1. Tworzysz forka naszego repozytorium (robisz to tylko raz, a później już zawsze korzystasz z tego forka).

2. Klonujesz forka na swój komputer.

3. Tworzysz nową gałąź w swoim forku zgodnie z zasadami opisanymi [tutaj](#2-twórz-osobne-gałęzie-dla-każdego-tematu-zmian).

4. Wprowadzasz zmiany na tej gałęzi, a następnie tworzysz pull request (twoja gałąź -> `master` naszego repozytorium).

> [!IMPORTANT]
> Nie zapomnij również o sklonowaniu modułów podrzędnych: `git submodule update --init --recursive`

Szczegółowe informacje na temat pracy na gałęziach znajdziesz [tutaj](#1-jak-pracować-w-jednym-lokalnym-repozytorium-nad-zmianami-zarówno-z-ps14-jak-i-z-innymi-forkami-ss14).

#### 1. Twórz osobne PR dla zmian funkcji, poprawek błędów, czyszczenia i refaktoryzacji

Nie jest to konieczne, ale ułatwia to przeglądanie zmian, zmniejsza liczbę konfliktów oraz upraszcza przywracanie wcześniejszego stanu w razie problemów.

- **Zmiany treści i poprawki błędów** powinny znajdować się w osobnych pull requestach.
- **Czyszczenie i refaktoryzacja** - w tym zmiany nazw zmiennych, poprawki wcięć i inne modyfikacje formatowania (np. spacje w nazwach plików) - muszą być zgłaszane w osobnym PR.
- **Refaktoryzacje obejmujące znaczną liczbę interfejsów publicznych** (np. pól, metod itp.), które wymagają modyfikacji w wielu systemach, **również muszą być wydzielone do osobnego pull requesta**. Nie należy łączyć ich ze zmianami treści lub poprawkami błędów.
- **Jeśli przenosisz plik do innego folderu lub przestrzeni nazw**, umieść tę zmianę w osobnym commicie (jeśli to możliwe), aby łatwiej było odróżnić, co zostało zmodyfikowane w samym pliku, a co jedynie przeniesione.
- **Zmiany w mapowaniu** powinny być zgłaszane w osobnym PR dla **każdej** edytowanej mapy - nawet jeśli zmiana jest niewielka. W jednym PR możesz za to zebrać kilka zmian na tej samej mapie.

> [!IMPORTANT]
> **Prosimy nie łączyć kilku niepowiązanych zmian w jednym PR.** Nie należy, np., zmieniać odporność termiczną rękawic wraz z dodawaniem nowej broni. Dobrą praktyką jest podzielenie dużego PR na mniejsze, jeżeli to rzeczywiście ma sens. Ułatwi i przyśpieszy to przegląd zmian.

Przy pracy nad mapami:

- **Mapę z upstreamu lub innego forka najpierw skopiuj** do `Resources/Maps/_Polonium/` i zmieniaj kopię, a prototyp mapy w `Resources/Prototypes/Maps/` przestaw na nią. Zmiany w cudzym pliku przy aktualizacji zostałyby nadpisane albo wywołałyby konflikt, który w pliku mapy bardzo trudno rozwiązać.
- **Przed zapisem cofnij przypadkowe zmiany** z edytora, np. drzwi otwarte kliknięciem.
- **Do opisu PR dołącz zdjęcie mapy** ([jak je zrobić](#5-jak-wygenerować-zdjęcie-mapy-dla-prów-lub-na-discorda)).

#### 2. Twórz osobne gałęzie dla każdego tematu zmian

Należy traktować gałąź `master` jako główną gałąź projektu, do której trafiają zatwierdzone zmiany, a także na niej działa serwer.

Przed dokonywaniem jakichkolwiek zmian w kodzie, upewnij się, że masz odpowiednią gałąź do tych zmian w swoim forku tego repozytorium.

- **Nie otwieraj PR z gałęzi `master` swojego forka.** Taki PR zostanie automatycznie zamknięty, a twój `master` przestanie odpowiadać naszemu, co utrudni kolejne PR-y.
- **W nazwie gałęzi używaj tylko znaków ASCII**, bez polskich liter i spacji (np. `naprawa-kaucjomatu`). Niektóre programy do obsługi gita źle radzą sobie z innymi znakami.
- **Po rozpoczęciu przeglądu nie rób rebase ani force-push.** Gdy pojawią się konflikty, zmerguj aktualny `master` do swojej gałęzi (`git fetch polonium`, a potem `git merge polonium/master`). Recenzenci widzą wtedy, co zmieniło się od ich ostatniego przeglądu, a ich komentarze nie gubią się w historii. Historia gałęzi i tak nie ma znaczenia, bo przy merge'u commity PR są łączone w jeden.

Więcej na temat procesu tworzenia pull requestów znajdziesz w [tym artykule](https://docs.spacestation14.com/en/general-development/setup/git-for-the-ss14-developer.html).

> [!TIP]
> Każdy nowy temat zaczynaj od nowej gałęzi utworzonej z aktualnego `master` naszego repozytorium:
>
> ```bash
> git fetch polonium                                 # Upewnij się, że masz ustawiony odpowiedni remote.
> git switch -c nazwa-nowej-galezi polonium/master
> ```
>
> Nie używaj ponownie gałęzi z innego PR, dopóki tamten PR jest otwarty. Każdy push na tę gałąź trafi do niego.

#### 3. Zawsze testuj swoje zmiany w grze

Sklonuj swoją gałąź, [skonfiguruj środowisko programistyczne](https://docs.spacestation14.com/en/general-development/setup/setting-up-a-development-environment.html) i sprawdź, czy wszystko działa zgodnie z Twoimi oczekiwaniami.

- **Sprawdź zmianę tak, jak zrobi to gracz**, łącznie z nietypowymi sytuacjami. Zmiany w kodzie współdzielonym (`Content.Shared`) i w predykcji sprawdź na dwóch klientach naraz, bo błędy synchronizacji często widać dopiero u drugiego gracza.
- **Uruchom testy, zanim oddasz PR do przeglądu.** Testy integracyjne potrzebują sporo pamięci RAM, a ich wyniki w GitHub Actions nie zawsze są wiarygodne, dlatego warto uruchomić je lokalnie:

```bash
dotnet build --configuration DebugOpt
dotnet test --configuration DebugOpt Content.Tests/Content.Tests.csproj -- NUnit.ConsoleOut=0
dotnet test --configuration DebugOpt Content.IntegrationTests/Content.IntegrationTests.csproj -- NUnit.ConsoleOut=0 NUnit.MapWarningTo=Failed
```

- **Po zmianach w prototypach YAML** uruchom linter: `dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj`.
- **Po zmianach w plikach `.ftl`** sprawdź lokalizację: `python Tools/translationTools/check_locales.py --ignore "**/tutorial" --ignore "**/datasets"` i `python Tools/check_locale_bom.py` (na Linuksie i macOS `python3`).
- **Nowy system powinien mieć testy integracyjne** logiki po stronie serwera (w `Content.IntegrationTests/Tests/_Polonium/`). Dzięki nim nikt nie zepsuje go przypadkiem przy kolejnych zmianach albo aktualizacji z upstreamu.
- **Ostrzeżenia w testach i błędy lokalizacji w konsoli gry traktuj jak błędy**, nawet jeśli wszystko pozornie działa.

#### 4. Wypełnij opis PR najważniejszymi informacjami

Opis PR to pierwsze, co zobaczy recenzent, a po latach często jedyne źródło wiedzy o tym, dlaczego coś działa tak, a nie inaczej. Korzystaj z [szablonu](.github/PULL_REQUEST_TEMPLATE.md).

1. **Krótko podsumuj, co robi PR.**
   Zwięźle opisz wprowadzone zmiany.

2. **Wyjaśnij, dlaczego.**
   Opisz konkretny problem, który rozwiązuje zmiana (zobacz [punkt 6](#6-naprawiaj-prawdziwe-problemy)). Przy zmianach balansu podaj liczby: jak było, jak jest i z czym to porównujesz.

3. **Dołącz materiały wizualne.**
   Zamieść zrzuty ekranu lub nagrania prezentujące efekty działania zmian lub poprawnego wyniku testowania.

4. **Opisz wszystkie istotne zmiany.**
   Wypisz, co dokładnie zostało zmienione. Uwzględnij wpływ na rozgrywkę, wydajność lub strukturę kodu. **Wymień każde usunięcie** (przedmiotu, mechaniki, receptury), bo w dużym diffie łatwo je przeoczyć.

5. **Wyjaśnij nowe mechaniki.**
   Jeśli dodajesz coś nowego, napisz, czym to jest, do czego służy i jak tego używać. Recenzent nie zna twojego pomysłu, a gracze dowiedzą się o nim z changelogu i podręcznika (zobacz [konwencję 10](#10-opisuj-nowości-w-podręczniku)).

6. **Dodaj instrukcje testowania.**
   Wymień kroki, które należy wykonać, aby samodzielnie przetestować PR. Jeśli zmiany były testowane automatycznie, opisz sposób testowania.

7. **Połącz PR ze zgłoszeniem (jeśli dotyczy).**
   Jeśli PR rozwiązuje zgłoszenie, wpisz w opisie `Closes #123`. Zgłoszenie zamknie się samo po merge'u.

8. **Dodaj changelog.**
   Changelog wyświetla się w grze i czytają go gracze, dlatego:

   - pisz po polsku, pełnymi zdaniami, a każdy wiersz zaczynaj od jednej z kategorii: `add`, `remove`, `tweak` lub `fix`,
   - opisuj konkretnie to, co gracz zauważy w grze, bez szczegółów technicznych i bez stylizacji na roleplay,
   - pomiń changelog przy zmianach niewidocznych dla gracza (refaktoryzacja, testy, narzędzia),
   - usuń przykładowe wiersze z szablonu i zostaw tylko swoje.

   Podstawowy format:

```
:cl: nazwa_użytkownika
- add: Dodano coś.
- remove: Usunięto coś.
- tweak: Zmieniono coś.
- fix: Naprawiono coś.
```

#### 5. Duże funkcjonalności? Najpierw pogadajmy na Discordzie

Masz pomysł na nowego antagonistę, tryb gry albo cały nowy system? Zanim jednak usiądziesz do kodu, **przedstaw swoją koncepcję maintainerom na naszym [Discordzie](https://discord.ss14.pl) i poczekaj na ich zgodę.**

Dotyczy to w szczególności:

- nowych antagonistów i ról,
- nowych trybów gry,
- nowych systemów i mechanik oraz dużych przeróbek istniejących (wszystkiego, co można uznać za osobny podsystem).

Wystarczy krótki opis: co chcesz dodać, dlaczego to dobry pomysł i jak wpłynie to na rozgrywkę. Nie potrzebujesz od razu szczegółowej dokumentacji ani dokładnych liczb, chodzi o ogólną koncepcję.

Po co to wszystko? Żeby oszczędzić Twój czas. Nawet świetnie napisany kod może nie zostać przyjęty, jeśli pomysł nie pasuje do wizji serwera albo ktoś już pracuje nad czymś podobnym. Kilka wiadomości na starcie może oszczędzić Ci wielu godzin pracy i rozczarowania przy przeglądzie. W opisie PR podlinkuj wiadomość lub wątek, w którym koncepcja została zaakceptowana.

#### 6. Naprawiaj prawdziwe problemy

Każda zmiana powinna rozwiązywać **rzeczywisty, istniejący problem**: błąd, który da się odtworzyć w grze, niedociągnięcie zgłoszone przez graczy lub administrację, mierzalny problem z wydajnością albo kłopot z balansem widoczny w rundach. Zanim zaczniesz coś poprawiać, upewnij się, że problem naprawdę występuje.

Czasem fragment kodu wygląda podejrzanie albo wydaje się, że "na pewno kiedyś się zepsuje", a w praktyce działa bez zarzutu. Poprawka wymyślonego problemu niewiele daje, za to dokłada pracy przy przeglądzie, może wprowadzić nowe błędy i utrudnia scalanie zmian z upstreamu.

Dlatego w sekcji "Powód / Balans" opisz konkretnie, co jest nie tak:

- **przy błędach**: kroki do odtworzenia oraz jak było przed zmianą i jak jest po niej (najlepiej ze zrzutem ekranu lub nagraniem),
- **przy balansie**: co dokładnie dzieje się w rundach i skąd o tym wiesz (zgłoszenia graczy, logi, własne obserwacje),
- **przy wydajności**: pomiar przed zmianą i po niej.

Nie masz pewności, czy coś jest problemem? Zapytaj na Discordzie, chętnie pomożemy to ustalić :)

#### 7. Narzędzia AI i automatyczny przegląd

Możesz korzystać z narzędzi AI (np. Claude Code, Copilota czy Cursora) i nie musisz tego zaznaczać w PR. Za PR odpowiadasz jednak ty, dlatego:

- **Rozumiesz każdą linię zmian** i potrafisz wyjaśnić, dlaczego działa. "Tak napisało AI" nie jest odpowiedzią na pytanie recenzenta.
- **Sprawdzasz zmiany w grze**, a nie tylko to, czy kod się kompiluje.
- **Opis PR piszesz samodzielnie.** AI może pomóc zebrać fakty, ale opis ma oddawać twoją wiedzę o zmianie i zgadzać się z kodem.
- **Tłumaczenia od AI czytasz tak samo uważnie jak własne**: sprawdź terminy z glosariusza, odmianę i zmienne.
- **Nie dodajemy grafik, dźwięków ani muzyki wygenerowanych przez AI.** Każdy asset musi mieć autora i licencję, a przy treściach z AI nie da się ich rzetelnie ustalić.

Instrukcje dla agentów AI są w pliku [AGENTS.md](AGENTS.md) i w plikach o tej samej nazwie w podkatalogach. Większość narzędzi wczytuje je automatycznie.

Każdy PR przeglądają też automatycznie boty (CodeRabbit i Copilot). Zanim poprosisz o przegląd maintainera:

- **odpowiedz na każdy komentarz bota**: wprowadź poprawkę albo krótko napisz, dlaczego się z nim nie zgadzasz,
- **sprawdź każdą sugestię bota w kodzie, zanim ją zastosujesz.** Boty się mylą i nie znają wszystkich zasad projektu.

---

# Często Zadawane Pytania

## 1. Jak pracować w jednym lokalnym repozytorium nad zmianami zarówno z PS14, jak i z innymi forkami SS14?

Oto jak wygląda przebieg pracy, gdy chcesz mieć jedno lokalne repozytorium do pracy nad dowolnymi zmianami zarówno z PS, jak i z innymi forkami SS14, bez konieczności ponownego klonowania.

1. Upewnij się, że masz już sklonowany istniejący fork na swoim komputerze.

2. Dodaj `remote` na repozytorium PS14.

   Na tym etapie twoje `remote`'y mogą wyglądać tak:

   ```bash
   $ git remote -v
   origin  https://github.com/nikitosych/space-station-14 (fetch)
   origin  https://github.com/nikitosych/space-station-14 (push)
   upstream        https://github.com/space-wizards/space-station-14 (fetch)
   upstream        https://github.com/space-wizards/space-station-14 (push)
   ```

   - `origin` to wskaźnik na twój własny fork SS14.
   - `upstream` to wskaźnik na oryginalne repozytorium SS14 (opcjonalnie, ale zalecane).

   Teraz dodaj kolejny `remote`, który będzie wskazywać na repozytorium PS14 i wczytaj z niego dane:

   ```bash
   git remote add polonium https://github.com/polonium14/polonium-station
   git fetch polonium
   ```

   Teraz twoje lokalne repo powinno wyglądać tak:

   ```bash
   $ git remote -v
   origin  https://github.com/ghostxne/space-station-14 (fetch)
   origin  https://github.com/ghostxne/space-station-14 (push)
   polonium        https://github.com/polonium14/polonium-station (fetch)
   polonium        https://github.com/polonium14/polonium-station (push)
   upstream        https://github.com/space-wizards/space-station-14 (fetch)
   upstream        https://github.com/space-wizards/space-station-14 (push)
   ```

3. Stwórz nową gałąź bezpośrednio z `master` repozytorium PS14:

   ```bash
   $ git checkout -b new-feature polonium/master
   branch 'new-feature' set up to track 'polonium/master'.
   Switched to a new branch 'new-feature'
   ```

4. Wyślij nową gałąź na GitHub:

   ```bash
   git push -u origin new-feature
   ```

   Przykładowy wynik:

   ```bash
   Enumerating objects: 760, done.
   Counting objects: 100% (744/744), done.
   Delta compression using up to 16 threads
   Compressing objects: 100% (493/493), done.
   Writing objects: 100% (718/718), 172.37 KiB | 17.24 MiB/s, done.
   Total 718 (delta 219), reused 662 (delta 202), pack-reused 0 (from 0)
   remote: Resolving deltas: 100% (219/219), completed with 9 local objects.
   remote:
   To https://github.com/nikitosych/space-station-14
    * [new branch]            new-feature -> new-feature
   branch 'new-feature' set up to track 'origin/new-feature'.
   ```

✅ **Gotowe!** Teraz możesz wprowadzać potrzebne zmiany i utworzyć Pull Request do PS14 z czystą historią commitów.

## 2. Jak pracować z repozytorium, aby wprowadzać zmiany?

Można podejść do tego na dwa sposoby - w zależności od tego, jakie masz doświadczenie i jakie repozytoria już posiadasz. Dla ułatwienia podzieliliśmy to na poziomy trudności.

### Poziom 1 – Podstawowy (edycja FTL/YAML w web-edytorze GitHub)

Ten sposób polecamy, jeśli dopiero zaczynasz i chcesz zmienić coś prostego (np. tłumaczenie FTL, lekkie zmiany w YML). Wszystko możesz zrobić w przeglądarce, bez znajomości Gita.

Kroki:

#### 1. **Zrób forka repozytorium (jeśli jeszcze go nie masz).**

Na stronie głównej repozytorium kliknij przycisk Fork w prawym górnym rogu -> **"Create Fork"**.

![1](https://github.com/user-attachments/assets/77617856-2bf3-480e-9fcd-0b9f680e7fbe)

#### 2. **Edytuj pliki w forku.**

- Wejdź w interesujący cię plik `.ftl` lub `.yml`, kliknij ikonę ołówka (✏️) i dokonaj zmian.
- Zapisz zmiany poprzez Commit changes.

![2](https://github.com/user-attachments/assets/3a36894c-5eb1-4cca-9713-1cb12dc7d1d4)

- Zrób to dla każdego pliku, który chcesz zmienić.

#### 3. **Przejdź do oryginalnego repozytorium (polonium14/polonium-station).**

- Kliknij zakładkę Pull Requests i wybierz opcję utworzenia nowego.

![3](https://github.com/user-attachments/assets/c7fd6ead-37fa-4391-acb9-b20a5e89fcca)

#### 4. **Wybierz źródło i cel PRa.**

- Upewnij się, że źródłem jest twój fork, a celem – nasze repozytorium (gałąź master).

![4](https://github.com/user-attachments/assets/97f7d2a1-1697-4191-9d27-b0dc5350da34)

#### 5. **Wypełnij formularz Pull Requesta zgodnie ze wzorem (tytuł, opis zmian, powód, ewentualne dodatkowe informacje).**

Po wysłaniu PR trafi do nas do sprawdzenia. Jeśli wszystko będzie w porządku, zostanie scalony i twoje zmiany pojawią się w głównym repozytorium 🎉

### Poziom 2 – Zaawansowany (praca lokalna, brak drugiego forka)

Ten sposób sprawdza się wtedy, gdy:

- masz już forka innego repozytorium (np. SS14),
- chcesz pracować nie tylko nad tłumaczeniami, ale też nad kodem lub większymi zmianami,
- potrzebujesz pełnej kontroli nad gałęziami i historią commitów.

W takim przypadku postępujesz zgodnie z procedurą z [tego punktu](#1-jak-pracować-w-jednym-lokalnym-repozytorium-nad-zmianami-zarówno-z-ps14-jak-i-z-innymi-forkami-ss14).

**Najważniejsze kroki:**

1. Tworzysz nową gałąź bezpośrednio z `master` repozytorium głównego (PS14),
2. Pracujesz tylko na tej czystej historii,
3. Wysyłasz gałąź na swojego forka i robisz Pull Request.

Dzięki temu unikasz bałaganu w commitach i możesz spokojnie pracować równolegle nad różnymi projektami.

## 3. Jak tłumaczyć grę?

> [!IMPORTANT]
> Poniższe informacje dotyczą wyłącznie zaawansowanego tłumaczenia. Do ogólnej lokalizacji używamy [projektu na Crowdinie](https://crowdin.com/project/space-station-14-polska), gdzie odbywa się większość prac. Kiedy tłumaczyć na Crowdinie, a kiedy w repozytorium, opisuje [konwencja 8](#8-lokalizacja).

1. Lokalizacja plików

Wszystkie teksty wyświetlane w interfejsie gry (dialogi, nazwy ról, opisy przedmiotów) znajdują się w dedykowanym katalogu zasobów: `Resources/Locale/`. Każdy język ma swój własny podkatalog (np. `pl-PL` dla polskiego).

- Wszystkie dane przechowywane są **w plikach z rozszerzeniem .ftl** (Fluent).
- **Pliki są pogrupowane** tematycznie w podfolderach (np. `/job/` dla profesji, `/chat/` dla UI czatu).

2. Składnia i format danych

System wykorzystuje pary **klucz = wartość**.

**Przykład z pliku** `pl-PL/job/job-names.ftl`:

```ftl
job-name-warden = Nadzorca Ochrony
job-name-security = Oficer Ochrony
job-name-cadet = Kadet Ochrony
job-name-hos = Komendant
job-name-scientist = Naukowiec
```

3. Tłumaczenie prototypów

Wszystkie prototypy przedmiotów, istot i innych elementów gry zawierają tłumaczenia w katalogu `/prototypes/generated/`. Na przykład, plik `/prototypes/generated/_goobstation/entities/mobs/npcs/animals.ftl` zawiera nazwy i opisy niektórych zwierząt:

```ftl
# ...
ent-MobSyndieAngryBee = pszczoła syndykatu    # pole 'name' prototypu
    .desc = Jaka miła pszczoła.               # pole 'description' prototypu
    .suffix = Wściekła, syndykat              # pole 'suffix' prototypu
    .gender = female                          # pole 'gender' prototypu
ent-MobAngryBeeWeak = { ent-MobAngryBee }     # odniesienie do klucza innego pliku lokalizacyjnego - działa tylko w obrębie tłumaczeń prototypów
    .suffix = Wściekła, słaba
    .desc = { ent-MobAngryBee.desc }          # odniesienie do opisu encji z innego pliku
    .gender = female
```


## 4. Szybkie generowanie tłumaczeń (`translation.bat` / `translation.sh`)

Po dodaniu nowego przedmiotu, moba albo innego prototypu YAML nie musisz ręcznie tworzyć plików `.ftl`. Mamy specjalne [skrypty](https://github.com/polonium14/polonium-station/tree/master/Tools/translationTools), które same wyciągają nazwy, opisy i sufiksy z prototypów, a potem wygenerują (albo uzupełnią) szkielety tłumaczeń.

**Potrzebujesz:** [Pythona 3.9](https://www.python.org/downloads) lub nowszego. Przy pierwszym uruchomieniu skrypt sam doinstaluje zależności.

**Jak użyć:**

1. Wejdź do katalogu `Tools/translationTools`.
2. Uruchom:
   - **Windows:** `translation.bat`
   - **Linux / macOS:** `./translation.sh`
3. Skrypt wygeneruje brakujące wpisy w:
   - `Resources/Locale/en-US/prototypes/generated/`
   - `Resources/Locale/pl-PL/prototypes/generated/`
4. Otwórz nowy plik `.ftl` po stronie `pl-PL` i zamień angielskie szkice na polskie nazwy i opisy.

Istniejące polskie tłumaczenia zostaną zachowane — skrypt dopisuje tylko to, czego jeszcze nie ma. Struktura folderów w `generated/` odpowiada ścieżkom prototypów (np. prototyp `Resources/Prototypes/Entities/Objects/foo.yml` trafi do `.../prototypes/generated/entities/objects/foo.ftl`).

Szczegółowy opis narzędzi znajdziesz w [README narzędzi tłumaczeniowych](https://github.com/polonium14/polonium-station/blob/master/Tools/translationTools/README.md).

## 5. Jak wygenerować zdjęcie mapy (dla PRów lub na Discorda)?

**Potrzebujesz:** [dotnet SDK v10](https://aka.ms/dotnet-download) lub nowszy.

**Jak użyć:**

1. Wejdź do katalogu korzenia repozytorium.
2. Uruchom: `dotnet run --project Content.MapRenderer <nazwa_mapy>`.
   - Nazwy map muszą odpowiadać nazwom prototypów map w katalogu `Resources/Prototypes/Maps/` ([przykład](https://github.com/polonium14/polonium-station/blob/2beb68aec6959004ed86a71b449d6fddfa2777e6/Resources/Prototypes/Maps/reach.yml#L15)).
3. Skrypt wygeneruje zdjęcie mapy i zapisze je w katalogu `Resources/MapImages/`. Możesz go skopiować i wrzucać do dowolnego miejsca!


---

> [!NOTE]
> W razie jakichkolwiek pytań lub sugestii zapraszamy na nasz [Discord](https://discord.gg/AnCVTJEmnN) :)
> Zaleca się również zapoznanie z [poradnikiem Slartiego](https://hackmd.io/@Slart/S1hsoGFm1l), który ułatwia naukę struktury i zasad działania codebase SS14.

---

_Te konwencje zostały opracowane na podstawie [zasad](https://docs.spacestation14.com/en/general-development/codebase-info.html) zdefiniowanych przez Wizardów (uzupełnione/przetłumaczone/skrócone)_
