# **Ogólne konwencje prowadzenia bazy kodowej projektu:**

#### 1. Sztuczna inteligencja jest narzędziem, a nie współautorem kodu.

Oczywiście nie można zabronić wykorzystywania AI, ale powinno ono służyć jako narzędzie do szybkiego uczenia się, poszukiwania informacji lub podstawowej pomocy. Lepiej napisać nieidealny, ale własny i zrozumiały kod, który zawsze da się wyjaśnić, zdebugować i w razie potrzeby naprawić, niż generować kod za pomocą AI nie wiedząc do końca, co się w nim dzieje.

#### 2. Jeden teman zmian = jeden PR (Pull Request).

Pełną treść wytycznych dotyczących PR można znaleźć [niżej](#wytyczne-dotyczące-pull-requestów).

#### 3. Nie duplikuj kodu.

Jak patrzysz na jakiś fragment kodu i myślisz sobie "Chcę, żeby mój kod robił to samo", to lepiej wydziel ten fragment do osobnej metody albo w inną formę abstrakcji, która umożliwi Ci ponownie wykorzystać go w miarę możliwości.

Są jednak sytuacje, w których nie da się uniknąć kopiowania. Na przykład: niektóre zależności, blok `override void Initialize()`, czy konstrukcja bazowa przy tworzeniu tzw. `EntitySystem`, która zawsze jest definicją klasy.

#### 4. Trzymaj się określonego stylu kodowania.

-   `PascalCase` dla stałych, klas, struktur, zdarzeń, metod, właściwości, interfejsów itp.
-   `camelCase` dla zmiennych lokalnych, zmiennych prywatnych i parametrów funkcji
-   `_camelCase` do nazw zmiennych CCVars oraz do definiowania wstrzykiwanych zależności ([Dependcy Injection](https://docs.spacestation14.com/en/robust-toolbox/ioc.html)), na przykład:

```csharp
public abstract partial class SharedStaminaSystem : EntitySystem
{
	[Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
	// ...
}
```

-   `UPPER_CASE` - rzadko, ale może być stosowane dla stałych globalnych

#### 5. Komentuj swój kod.

Zostawiaj komentarze w kodzie, szczególnie w miejscach, które nie są oczywiste na pierwszy rzut oka. Pisz w nich, _co_ robi kod, a przede wszystkim _dlaczego_ kod robi to.

W przypadkach klas, struktur, metodów, pól/właściwości i członków klasy korzystaj z [dokumentacji XML](https://learn.microsoft.com/pl-pl/dotnet/csharp/language-reference/xmldoc/). Wszystkie publiczne metody i konstrukcje oznaczone atrybutem `DataField` zawsze powinny być udokumentowane.

#### 6. Unikaj "magicznych" wartości.

Są to wartości, które są wpisane na sztywno w kodzie i które muszą zgadzać się z identycznymi wartościami w innych jego miejscach. Jeśli zmienisz tą wartość w jednym miejscu, ale zapomnisz o innych - program może działać niepoprawnie.

Zamiast tego, w prostych sytuacjach należy wydzielać takie wartości do stałych oznaczonych modyfikatorami `const` lub `static readonly`. W bardziej złożonych sytuacjach należy zadbać o to, by te wartości nie mogły się różnić - można to osiągnąć np. poprzez wyjątki, testy jednostkowe lub inne mechanizmy sprawdzające. Najważniejsze jest, aby ewentualny błąd był natychmiast zauważalny.

#### 7. Prawdłowo porządkuj elementy w typie.

Podzas deklarowania elementów składowych w typie, zawsze należy umieszczać pola i własciwości nad pozostałymi elementami. Ułatwia to zrozumienie, na jakich danych operuje typ. Pomieszanie pól i innych członków utrudnia czytelność i orientację w kodzie.

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
public class Player
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

#### 8. Również należy się trzymać:

-   [typowych konwencji kodowania w języku C#](http://learn.microsoft.com/pl-pl/dotnet/csharp/fundamentals/coding-style/coding-conventions)
-   [konwencji przyjętych przez Wizardów](https://docs.spacestation14.com/en/general-development/codebase-info/conventions.html)

# Wytyczne dotyczące pull requestów

## Zasady ogólne

#### 0. Ogólny przebieg pracy nad zmianami

1. Tworzysz forka naszego repozytorium (robisz to tylko raz, a później już zawsze korzystasz z tego forka).

2. Klonujesz forka na swój komputer.

3. Tworzysz nową gałąź w swoim forku zgodnie z zasadami opisanymi [tutaj](#2-twórz-osobne-gałęzie-dla-każdego-tematu-zmian).

4. Wprowadzasz zmiany na tej gałęzi, a następnie tworzysz pull request (twoja gałąź -> `master` naszego repozytorium).

> [!IMPORTANT]
> Nie zapomnij również o sklonowaniu modułów podrzędnych: `git submodule update --init --recursive`

Szczegółowe informacje na temat pracy na gałęziach znajdziesz [tutaj](#1-mam-ju%C5%BC-forka-ss14-na-swoim-profilu-wi%C4%99c-github-nie-pozwala-mi-utworzy%C4%87-forka-ps14-jak-mog%C4%99-to-obej%C5%9B%C4%87)

#### 1. Twórz osobne PR dla zmian funkcji, poprawek błędów, czyszczenia i refaktoryzacji.

Nie jest to konieczne, ale ułatwia to przeglądanie zmian, zmniejsza liczbę konfliktów oraz upraszcza przywracanie wcześniejszego stanu w razie problemów.

-   **Zmiany treści i poprawki błędów** powinny znajdować się w osobnych pull requestach.
-   **Czyszczenie i refaktoryzacja** - w tym zmiany nazw zmiennych, poprawki wcięć i inne modyfikacje formatowania (np. spacje w nazwach plików) - muszą być zgłaszane w osobnym PR.
-   **Refaktoryzacje obejmujące znaczną liczbę interfejsów publicznych** (np. pól, metod itp.), które wymagają modyfikacji w wielu systemach, **również muszą być wydzielone do osobnego pull requesta**. Nie należy łączyć ich ze zmianami treści lub poprawkami błędów.
-   **Jeśli przenosisz plik do innego folderu lub przestrzeni nazw**, umieść tę zmianę w osobnym commicie (jeśli to możliwe), aby łatwiej było odróżnić, co zostało zmodyfikowane w samym pliku, a co jedynie przeniesione.
-   **Zmiany w mapowaniu** powinny być zgłaszane w osobnym PR dla **każdej** edytowanej mapy - nawet jeśli zmiana jest niewielka.

> [!IMPORTANT]
> **Prosimy nie łączyć kilku niepowiązanych zmian w jednym PR.** Nie należy, np., zmieniać odporność termiczną rękawic wraz z dodawaniem nowej broni. Dobrą praktyką jest podzielenie dużego PR na mniejsze, jeżeli to rzeczywiście ma sens. Ułatwi i przyśpieszy to przegląd zmian.

#### 2. Twórz osobne gałęzie dla każdego tematu zmian.

Należy traktować gałąź `master` jako główną gałąź projektu, do której trafiają zatwierdzone zmiany, a także na niej działa serwer.

Przed dokonywaniem jakichkolwiek zmian w kodzie, upewnij się, że masz odpowiednią gałąź do tych zmian w swoim forku tego repozytorium.

Więcej na temat procesu tworzenia pull requestów znajdziesz w [tym artykule](https://docs.spacestation14.com/en/general-development/setup/git-for-the-ss14-developer.html).

> [!TIP]
> Jeśli zaczynasz pracę na gałęzi, z której poprzednio korzystałeś do utworzenia innego PRa, nie zapomnij zresetować jej treść zanim będziesz na niej pracować:
> ```bash
> git fetch polonium                 # Upewnij się, że masz ustawiony odpowiedni remote.
> git reset --hard polonium/master   # UWAGA: zresetuje to wszystkie niezapisane zmiany
> git push --force
> ```

#### 3. Zawsze testuj swoje zmiany w grze.

Sklonuj swoją gałąź, [skonfiguruj środowisko programistyczne](https://docs.spacestation14.com/en/general-development/setup/setting-up-a-development-environment.html) i sprawdź, czy wszystko działa zgodnie z Twoimi oczekiwaniami.

#### 4. Wypełnij swój PR ważniejszą informacją

1. **Krótko podsumuj, co robi PR.**
   Zwięźle opisz wprowadzone zmiany.

2. **Dołącz materiały wizualne.**
   Zamieść zrzuty ekranu lub nagrania prezentujące efekty działania zmian lub poprawnego wyniku testowania.

3. **Opisz wszystkie istotne zmiany.**
   Wypisz, co dokładnie zostało zmienione i dlaczego. Uwzględnij wpływ na rozgrywkę, wydajność lub strukturę kodu.

4. **Dodaj instrukcje testowania.**
   Wymień kroki, które należy wykonać, aby samodzielnie przetestować PR. Jeśli zmiany były testowane automatycznie, opisz sposób testowania.

5. **Połącz PR z issue (jeśli dotyczy).**
   Jeśli PR naprawia błąd, dodaj powiązanie do zgłoszenia (np. `Fixes #123`).

6. **Dodaj changelog.**
   Bardzo krótko przelicz, co zostało zmienione w grze. Te zmiany będą wyświetlane w grze.
   Każdy wiersz changelogu powinien zaczynać się od jednej z kategorii: `add`, `remove`, `tweak` lub `fix`. Podstawowy format:

```
:cl: nazwa_użytkownika
- add: Dodano coś
- remove: Usunieto coś
- tweak: Zmieniono coś
- fix: Poprawiono coś
```

#### 5. Wstępna akceptacja dużych funkcjonalności

Duże nowe funkcjonalności oraz kompleksowe przeróbki istniejących, rozbudowanych elementów (np. antagonistów lub czegokolwiek, co można uznać za osobny podsystem), powinny zostać najpierw zaproponowane i zaakceptowane **w formie ogólnej koncepcji**, zanim rozpoczniesz ich właściwą implementację

---

# Często Zadawane Pytania

## 1. Mam już forka SS14 na swoim profilu, więc GitHub nie pozwala mi utworzyć forka PS14. Jak mogę to obejść?

Rzeczywiście, zgodnie z zasadami GitHuba, w profilu może znajdować się _tylko jeden_ fork danego repozytorium.
Aby to obejść, powinieneś mieć jeden **wspólny fork**, na którym będziesz pracować z różnymi repozytoriami.
Kluczowe jest, aby ten fork miał **poprawnie skonfigurowane remote'y**.

Oto jak wyglądają twoje działania, gdy chesz mieć "drugi fork":
*(Te kroki opisują również zwykły przebieg pracy na poszczególnych gałęziach)*

1. Upewnij się, że masz już sklonowany istniejący fork na swoim komputerze.

2. Dodaj `remote` na repozytorium PS14:

Na tym etapie twoje `remote`'y mogą wyglądać tak:

```bash
$ git remote -v
origin  https://github.com/nikitosych/space-station-14 (fetch)
origin  https://github.com/nikitosych/space-station-14 (push)
upstream        https://github.com/space-wizards/space-station-14 (fetch)
upstream        https://github.com/space-wizards/space-station-14 (push)
```

-   `origin` to wskaźnik na twój własny fork SS14.
-   `upstream` to wskaźnik na oryginalne repozytorium SS14 (opcjonalnie, ale zalecane).

Teraz dodaj kolejny `remote`, który będzie wskazywać na repozytorium PS14 i wczytaj z niego dane:

```bash
$ git remote add polonium https://github.com/polonium14/polonium-space
$ git fetch polonium
```

Teraz twoje lokalne repo powinno wyglądać tak:

```bash
$ git remote -v
origin  https://github.com/ghostxne/space-station-14 (fetch)
origin  https://github.com/ghostxne/space-station-14 (push)
polonium        https://github.com/polonium14/polonium-space (fetch)
polonium        https://github.com/polonium14/polonium-space (push)
upstream        https://github.com/space-wizards/space-station-14 (fetch)
upstream        https://github.com/space-wizards/space-station-14 (push)
```

3. Stwórz nową gałąź:

```bash
$ git checkout -b new-feature
Switched to a new branch 'new-feature'
```

4. Zastąp stan gałęzi zawartością PS14 (gałęzi `master`)

```bash
$ git reset --hard polonium/master
HEAD is now at 3c38b14144 Tłumaczenie HUD/UI (#15)
```

> [!NOTE]
> Polecenie `git reset --hard` usuwa wszystkie lokalne zmiany i ustawia gałąź dokładnie w tym samym stanie, co polonium/master.
> Dzięki temu możesz szybko pobrać najnowsze aktualizacje, jeśli lokalnie nie wprowadzałeś własnych zmian.

5. Wyślij nową gałąź na GitHub:

```bash
$ git push -u origin new-feature
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

-   Wejdź w interesujący cię plik `.ftl` lub `.yml`, kliknij ikonę ołówka (✏️) i dokonaj zmian.
-   Zapisz zmiany poprzez Commit changes.

![2](https://github.com/user-attachments/assets/3a36894c-5eb1-4cca-9713-1cb12dc7d1d4)

-   Zrób to dla każdego pliku, który chcesz zmienić.

#### 3. **Przejdź do oryginalnego repozytorium (polonium14/polonium-station).**

-   Kliknij zakładkę Pull Requests i wybierz opcję utworzenia nowego.

![3](https://github.com/user-attachments/assets/c7fd6ead-37fa-4391-acb9-b20a5e89fcca)

#### 4. **Wybierz źródło i cel PRa.**

-   Upewnij się, że źródłem jest twój fork, a celem – nasze repozytorium (gałąź master).

![4](https://github.com/user-attachments/assets/97f7d2a1-1697-4191-9d27-b0dc5350da34)

#### 5. **Wypełnij formularz Pull Requesta zgodnie ze wzorem (tytuł, opis zmian, powód, ewentualne dodatkowe informacje).**

Po wysłaniu PR trafi do nas do sprawdzenia. Jeśli wszystko będzie w porządku, zostanie scalony i twoje zmiany pojawią się w głównym repozytorium 🎉

### Poziom 2 – Zaawansowany (praca lokalna, brak drugiego forka)

Ten sposób sprawdza się wtedy, gdy:

-   masz już forka innego repozytorium (np. SS14),
-   chcesz pracować nie tylko nad tłumaczeniami, ale też nad kodem lub większymi zmianami,
-   potrzebujesz pełnej kontroli nad gałęziami i historią commitów.

W takim przypadku postępujesz zgodnie z procedurą z [tego punktu](https://github.com/polonium14/polonium-station/blob/master/CONTRIBUTING.md#1-mam-ju%C5%BC-forka-ss14-na-swoim-profilu-wi%C4%99c-github-nie-pozwala-mi-utworzy%C4%87-forka-ps14-jak-mog%C4%99-to-obej%C5%9B%C4%87).

**Najważniejsze kroki:**

1. Tworzysz nową gałąź,
2. Resetujesz ją do stanu `master` repozytorium głównego (PS14),
3. Pracujesz tylko na tej czystej historii,
4. Wysyłasz gałąź na swojego forka i robisz Pull Request.

Dzięki temu unikasz bałaganu w commitach i możesz spokojnie pracować równolegle nad różnymi projektami.

---

> [!NOTE]
> W razie jakichkolwiek pytań lub sugestii zapraszamy na nasz [Discord](https://discord.gg/AnCVTJEmnN) :)
> Zaleca się również zapoznanie z [poradnikiem Slartiego](https://hackmd.io/@Slart/S1hsoGFm1l), który ułatwia naukę struktury i zasad działania codebase SS14.

---

_Te konwecje zostały opracowane na podstawie [zasad](https://docs.spacestation14.com/en/general-development/codebase-info.html) zdefiniowanych przez Wizardów (uzupełnione/przetłumaczone/skrócone)_
