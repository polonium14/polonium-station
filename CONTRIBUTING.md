# **Ogólne konwencje prowadzenia bazy kodowej projektu:**



#### 1. Sztuczna inteligencja jest narzędziem, a nie współautorem kodu.

Oczywiście nie można zabronić wykorzystywania AI, ale powinno ono służyć jako narzędzie do szybkiego uczenia się, poszukiwania informacji lub podstawowej pomocy. Lepiej napisać nieidealny, ale własny i zrozumiały kod, który zawsze da się wyjaśnić, zdebugować i w razie potrzeby naprawić, niż generować kod za pomocą AI nie wiedząc do końca, co się w nim dzieje.  Ustalamy twardą regułę: **generowanie kodu przez AI to tabu**.

#### 2. Jedna zmiana = jeden PR (Pull Request).

Pełną treść wytycznych dotyczących PR można znaleźć [niżej](#wytyczne-dotyczące-pull-requestów).


#### 3. Nie duplikuj kodu.

Jak patrzysz na jakiś fragment kodu i myślisz sobie "Chcę, żeby mój kod robił to samo", to lepiej wydziel ten fragment do osobnej metody albo w inną formę abstrakcji, która umożliwi Ci ponownie wykorzystać go w miarę możliwości. 

Są jednak sytuacje, w których nie da się uniknąć kopiowania. Na przykład: niektóre zależności, blok `override void Initialize()`, czy konstrukcja bazowa przy tworzeniu tzw. `EntitySystem`, która zawsze jest definicją klasy.

#### 4.  Trzymaj się określonego stylu kodowania.

- `PascalCase` dla stałych, klas, struktur, zdarzeń, metod, właściwości, interfejsów itp.
- `camelCase` dla zmiennych lokalnych, zmiennych prywatnych i parametrów funkcji
- `_camelCase` do definiowania wstrzykiwanych zależności ([Dependcy Injection](https://docs.spacestation14.com/en/robust-toolbox/ioc.html)), na przykład:

```csharp
public abstract partial class SharedStaminaSystem : EntitySystem 
{
	[Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
	// ...
}	
```
- `UPPER_CASE` - rzadko, ale może być stosowane dla stałych globalnych

#### 5. Komentuj swój kod.

Zostawiaj komentarze w kodzie, szczególnie w miejscach, które nie są oczywiste na pierwszy rzut oka. Pisz w nich, *co* robi kod, a przede wszystkim *dlaczego* kod robi to. 

W przypadkach klas, struktur, metodów, pól/właściwości i członków klasy korzystaj z [dokumentacji XML](https://learn.microsoft.com/pl-pl/dotnet/csharp/language-reference/xmldoc/). Wszystkie publiczne metody i konstrukcje oznaczone atrybutem `DataField` zawsze powinny być udokumentowane.

#### 6. Unikaj "magicznych" wartości.

Są to wartości, które są wpisane na sztywno w kodzie i które muszą zgadzać się z identycznymi wartościami w innych jego miejscach. Jeśli zmienisz tą wartość w jednym miejscu, ale zapomnisz o innych - program może działać niepoprawnie. 

Zamiast tego, w prostych sytuacjach należy wydzielać takie wartości do stałych oznaczonych modyfikatorami `const` lub `static readonly`. W bardziej złożonych sytuacjach należy zadbać o to, by te wartości nie mogły się różnić - można to osiągnąć np. poprzez wyjątki, testy jednostkowe lub inne mechanizmy sprawdzające. Najważniejsze jest, aby ewentualny błąd był natychmiast zauważalny.

#### 7.  Prawdłowo porządkuj elementy w typie.

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
- [typowych konwencji kodowania w języku C#](http://learn.microsoft.com/pl-pl/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [konwencji przyjętych przez Wizardów](https://docs.spacestation14.com/en/general-development/codebase-info/conventions.html)


# Wytyczne dotyczące pull requestów

## Zasady ogólne

#### 1. Twórz osobne PR dla zmian funkcji, poprawek błędów, czyszczenia i refaktoryzacji.

Ułatwia to przeglądanie zmian, zmniejsza liczbę konfliktów oraz upraszcza przywracanie wcześniejszego stanu w razie problemów.

-   **Zmiany treści i poprawki błędów** powinny znajdować się w osobnych pull requestach.
    
-   **Czyszczenie i refaktoryzacja** - w tym zmiany nazw zmiennych, poprawki wcięć i inne modyfikacje formatowania (np. spacje w nazwach plików) - muszą być zgłaszane w osobnym PR.
    
-   **Refaktoryzacje obejmujące znaczną liczbę interfejsów publicznych** (np. pól, metod itp.), które wymagają modyfikacji w wielu systemach, **również muszą być wydzielone do osobnego pull requesta**. Nie należy łączyć ich ze zmianami treści lub poprawkami błędów.
    
-   **Jeśli przenosisz plik do innego folderu lub przestrzeni nazw**, umieść tę zmianę w osobnym commicie (jeśli to możliwe), aby łatwiej było odróżnić, co zostało zmodyfikowane w samym pliku, a co jedynie przeniesione.
    
-   **Zmiany w mapowaniu** powinny być zgłaszane w osobnym PR dla **każdej** edytowanej mapy - nawet jeśli zmiana jest niewielka.

- ⚠️ **Nie łącz kilku niepowiązanych zmian w jednym PR.** Nie należy, np., zmieniać odporność termiczną rękawic wraz z dodawaniem nowej broni. Dobrą praktyką jest podzielenie dużego PR na mniejsze, jeżeli to rzeczywiście ma sens. Ułatwi i przyśpieszy to przegląd zmian.

#### 2. Twórz osobne gałęzie dla każdego tematu zmian.

Należy traktować gałąź `master` jako główną gałąź projektu, do której trafiają zatwierdzone zmiany, a także na niej działa serwer. 

Przed dokonywaniem jakichkolwiek zmian w kodzie, upewnij się, że masz odpowiednią gałąź do tych zmian w swoim forku tego repozytorium. Powinna ona mieć nazwę, która jest technicznym "tytułem" zmiany, którą chcesz wprowadzić. 

- W przypadku poprawek kodu, nazwa gałęzi powinna zawierać przyrostek `-fix` na końcu. Przykłady: `belt-fix`, `item-tooltip-fix`, `clown-mask-sprite-fix`
- W przypadku zmian wymagających pilnego przęglądu przez maintainerów, nazwa powinna kończyć się przyrostkiem `-hotfix`. Przykład: `chat-crash-fix`

Więcej na temat procesu tworzenia pull requestów znajdziesz w [tym artykule](https://docs.spacestation14.com/en/general-development/setup/git-for-the-ss14-developer.html).

#### 3. Zawsze testuj swoje zmiany w grze.

Sklonuj swoją gałąź, [skonfiguruj środowisko programistyczne](https://docs.spacestation14.com/en/general-development/setup/setting-up-a-development-environment.html) i sprawdź, czy wszystko działa zgodnie z Twoimi oczekiwaniami.


#### 4. Wypełnij swój PR ważniejszą informacją

1. **Krótko podsumuj, co robi PR.**  
Zwięźle opisz wprowadzone zmiany.

2. **Dołącz materiały wizualne.**  
Zamieść zrzuty ekranu lub nagrania prezentujące efekty działania zmian lub poprawnego wyniku testowania.

4. **Opisz wszystkie istotne zmiany.**  
Wypisz, co dokładnie zostało zmienione i dlaczego. Uwzględnij wpływ na rozgrywkę, wydajność lub strukturę kodu.

5. **Dodaj instrukcje testowania.**  
Wymień kroki, które należy wykonać, aby samodzielnie przetestować PR. Jeśli zmiany były testowane automatycznie, opisz sposób testowania.

6. **Połącz PR z issue (jeśli dotyczy).**  
Jeśli PR naprawia błąd, dodaj powiązanie do zgłoszenia (np. `Fixes #123`).

9. **Dodaj changelog.**  
Bardzo krótko przelicz, co zostało zmienione w grze. 
Każdy wiersz changelogu powinien zaczynać się od jednej z kategorii: `add`, `remove`, `tweak` lub `fix`. Podstawowy format:
```
:cl:
- add: Dodano coś
- remove: Usunieto coś
- tweak: Zmieniono coś
- fix: Poprawiono coś
```

#### 5. Wstępna akceptacja dużych funkcjonalności

Duże nowe funkcjonalności oraz kompleksowe przeróbki istniejących, rozbudowanych elementów (np. antagonistów lub czegokolwiek, co można uznać za osobny podsystem), powinny zostać najpierw zaproponowane i zaakceptowane **w formie ogólnej koncepcji**, zanim rozpoczniesz ich właściwą implementację

---

W razie jakichkolwiek pytań lub sugestii zapraszamy na nasz [Discord](https://discord.gg/AnCVTJEmnN) :)

---

*Te konwecje zostały opracowane na podstawie [zasad](https://docs.spacestation14.com/en/general-development/codebase-info.html) zdefiniowanych przez Wizardów (uzupełnione/przetłumaczone/skrócone)*
