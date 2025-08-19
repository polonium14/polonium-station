command-help-usage =
    Użycie:
command-help-invertible =
    Zachowanie tej komendy można odwrócić, używając prefiksu "not".
command-description-tpto =
    Teleportuje podane encje do wskazanej encji docelowej.
command-description-player-list =
    Zwraca listę wszystkich sesji graczy.
command-description-player-self =
    Zwraca aktualną sesję gracza.
command-description-player-imm =
    Zwraca sesję powiązaną z graczem podanym jako argument.
command-description-player-entity =
    Zwraca encje należące do podanych sesji.
command-description-self =
    Zwraca aktualnie podpiętą encję.
command-description-physics-velocity =
    Zwraca prędkość liniową podanych encji.
command-description-physics-angular-velocity =
    Zwraca prędkość kątową podanych encji.
command-description-buildinfo =
    Podaje informacje o kompilacji gry.
command-description-cmd-list =
    Zwraca listę wszystkich komend po tej stronie.
command-description-explain =
    Tłumaczy podany wyrażenie, podając opis i sygnatury komend. Działa tylko dla poprawnych wyrażeń, nie potrafi tłumaczyć komend, które nie zostały poprawnie sparsowane.
command-description-search =
    Wyszukuje podaną wartość w wejściu.
command-description-stopwatch =
    Mierzy czas wykonania podanego wyrażenia.
command-description-types-consumers =
    Podaje wszystkie komendy, które mogą przetwarzać dany typ.
command-description-types-tree =
    Narzędzie debugowania zwracające wszystkie typy, do których interpreter komend może rzutować wejście.
command-description-types-gettype =
    Zwraca typ podanego wejścia.
command-description-types-fullname =
    Zwraca pełną nazwę typu według CoreCLR.
command-description-as =
    Rzutuje wejście na podany typ.
    Efektywnie wskazówka typu, jeśli znasz typ, a interpreter go nie rozpoznaje.
command-description-count =
    Zlicza ilość elementów wejścia, zwracając liczbę całkowitą.
command-description-map =
    Mapuje wejście przy użyciu podanego bloku.
command-description-select =
    Wybiera N obiektów lub N% obiektów z wejścia.
    Można dodatkowo odwrócić działanie komendy za pomocą "not", aby wybrać wszystko poza N obiektami.
command-description-comp =
    Zwraca podany komponent z encji wejściowych, odrzucając encje, które go nie posiadają.
command-description-delete =
    Usuwa podane encje.
command-description-ent =
    Zwraca podany identyfikator encji.
command-description-entities =
    Zwraca wszystkie encje na serwerze.
command-description-paused =
    Filtruje encje wejściowe według tego, czy są zatrzymane (paused).
command-description-with =
    Filtruje encje wejściowe według tego, czy posiadają podany komponent.
command-description-fuck =
    Wywołuje wyjątek.
command-description-ecscomp-listty =
    Wypisuje wszystkie zarejestrowane typy komponentów.
command-description-cd =
    Zmienia bieżący katalog sesji na podaną ścieżkę względną lub absolutną.
command-description-ls-here =
    Wypisuje zawartość bieżącego katalogu.
command-description-ls-in =
    Wypisuje zawartość podanej ścieżki względnej lub absolutnej.
command-description-methods-get =
    Zwraca wszystkie metody powiązane z podanym typem.
command-description-methods-overrides =
    Zwraca wszystkie metody nadpisane w podanym typie.
command-description-methods-overridesfrom =
    Zwraca wszystkie metody nadpisane z podanego typu w podanym typie.
command-description-cmd-moo =
    Zadaje ważne pytania.
command-description-cmd-descloc =
    Zwraca string lokalizacyjny dla opisu komendy.
command-description-cmd-getshim =
    Zwraca szynę wykonawczą komendy.
command-description-help =
    Podaje szybki przewodnik po narzędziach toolshed.
command-description-ioc-registered =
    Zwraca wszystkie typy zarejestrowane w IoCManager na aktualnym wątku (zwykle wątek gry).
command-description-ioc-get =
    Pobiera instancję zarejestrowanego typu w IoC.
command-description-loc-tryloc =
    Próbuje pobrać string lokalizacyjny, zwracając null w razie niepowodzenia.
command-description-loc-loc =
    Pobiera string lokalizacyjny, zwracając nieprzetłumaczony string w razie niepowodzenia.
command-description-physics-angular_velocity =
    Zwraca prędkość kątową podanych encji.
command-description-vars =
    Zwraca listę wszystkich zmiennych ustawionych w tej sesji.
command-description-any =
    Zwraca true, jeśli wejście zawiera jakiekolwiek wartości, w przeciwnym razie false.
command-description-contains =
    Zwraca true, jeśli wejście zawiera podaną wartość.
command-description-ArrowCommand =
    Przypisuje wejście do zmiennej.
command-description-isempty =
    Zwraca true, jeśli wejście jest puste, w przeciwnym razie false.
command-description-isnull =
    Zwraca true, jeśli wejście jest null, w przeciwnym razie false.
command-description-unique =
    Filtruje wejście, pozostawiając tylko unikalne wartości, usuwając duplikaty.
command-description-where =
    Dla sekwencji wejściowej IEnumerable<T> stosuje blok o sygnaturze T -> bool, decydujący czy dany element ma znaleźć się w wyjściowej sekwencji.
command-description-do =
    Wsteczna kompatybilność z BQL, stosuje stare komendy na wejściowej sekwencji.
command-description-named =
    Filtruje encje według nazwy, używając regex ^selector$.
command-description-prototyped =
    Filtruje encje według prototypu.
command-description-nearby =
    Tworzy nową listę wszystkich encji w pobliżu wejściowych w podanym zakresie.
command-description-first =
    Zwraca pierwszy element podanej sekwencji.
command-description-splat =
    "Rozmnaża" blok, wartość lub zmienną, tworząc N kopii w liście.
command-description-val =
    Rzutuje podaną wartość, blok lub zmienną na dany typ. Przeważnie używane jako obejście ograniczeń zmiennych.
command-description-var =
    Zwraca zawartość podanej zmiennej. Próbuje automatycznie wywnioskować typ zmiennej. Złożone komendy modyfikujące zmienną mogą wymagać użycia komendy 'val'.
command-description-actor-controlled =
    Filtruje encje według tego, czy są aktywnie kontrolowane.
command-description-actor-session =
    Zwraca sesje powiązane z podanymi encjami.
command-description-physics-parent =
    Zwraca rodziców podanych encji.
command-description-emplace =
    Wykonuje podany blok na wejściach, umieszczając wartość wejściową w zmiennej $value wewnątrz bloku.
    Dodatkowo rozdziela wartości $wx, $wy, $proto, $desc, $name oraz $paused dla encji.
    Możliwe też inne breakout values dla innych typów – patrz dokumentacja danego typu.
command-description-AddCommand =
    Wykonuje dodawanie liczb.
command-description-SubtractCommand =
    Wykonuje odejmowanie liczb.
command-description-MultiplyCommand =
    Wykonuje mnożenie liczb.
command-description-DivideCommand =
    Wykonuje dzielenie liczb.
command-description-min =
    Zwraca wartość minimalną z dwóch podanych.
command-description-max =
    Zwraca wartość maksymalną z dwóch podanych.
command-description-BitAndCommand =
    Wykonuje bitowe AND.
command-description-bitor =
    Wykonuje bitowe OR.
command-description-BitXorCommand =
    Wykonuje bitowe XOR.
command-description-neg =
    Neguje wejście.
command-description-GreaterThanCommand =
    Wykonuje porównanie "większe niż", x > y.
command-description-LessThanCommand =
    Wykonuje porównanie "mniejsze niż", x < y.
command-description-GreaterThanOrEqualCommand =
    Wykonuje porównanie "większe lub równe", x >= y.
command-description-LessThanOrEqualCommand =
    Wykonuje porównanie "mniejsze lub równe", x <= y.
command-description-EqualCommand =
    Sprawdza równość, zwracając true jeśli wartości są równe.
command-description-NotEqualCommand =
    Sprawdza nierówność, zwracając true jeśli wartości nie są równe.
command-description-append =
    Dodaje wartość do wejściowej sekwencji.
command-description-DefaultIfNullCommand =
    Zastępuje wejście wartością domyślną typu, jeśli jest null (tylko dla typów wartości, nie obiektów).
command-description-OrValueCommand =
    Jeśli wejście jest null, używa podanej wartości alternatywnej.
command-description-DebugPrintCommand =
    Przezroczyste drukowanie wartości, używane do debugowania w trakcie wykonania komendy.
command-description-i =
    Stała całkowita (integer).
command-description-f =
    Stała zmiennoprzecinkowa (float).
command-description-s =
    Stała typu string.
command-description-b =
    Stała typu bool.
command-description-join =
    Łączy dwie sekwencje w jedną sekwencję.
command-description-reduce =
    Dla podanego bloku używanego jako reduktor, przekształca sekwencję w pojedynczą wartość.
    Lewa strona bloku jest implikowana, prawa strona przechowywana w $value.
command-description-rep =
    Powtarza wartość wejściową N razy, tworząc sekwencję.
command-description-take =
    Pobiera N wartości z wejściowej sekwencji.
command-description-spawn-at =
    Tworzy encję w podanych współrzędnych.
command-description-spawn-on =
    Tworzy encję na podanej encji, w jej współrzędnych.
command-description-spawn-in =
    Tworzy encję w podanym kontenerze na podanej encji, upuszczając ją na jej współrzędnych jeśli nie zmieści się w kontenerze.
command-description-spawn-attached =
    Tworzy encję podpiętą do podanej encji, w punkcie (0,0) względem niej.
command-description-mappos =
    Zwraca współrzędne encji względem jej mapy.
command-description-pos =
    Zwraca współrzędne encji.
command-description-tp-coords =
    Teleportuje podane encje do wskazanych współrzędnych.
command-description-tp-to =
    Teleportuje podane encje do wskazanej encji.
command-description-tp-into =
    Teleportuje podane encje „do” wskazanej encji, przyczepiając je w punkcie (0,0) względem niej.
command-description-comp-get =
    Pobiera podany komponent z podanej encji.
command-description-comp-add =
    Dodaje podany komponent do podanej encji.
command-description-comp-ensure =
    Zapewnia, że podana encja posiada dany komponent.
command-description-comp-has =
    Sprawdza, czy podana encja posiada dany komponent.
command-description-AddVecCommand =
    Dodaje skalara (pojedynczą wartość) do każdego elementu wejściowej sekwencji.
command-description-SubVecCommand =
    Odejmuje skalar od każdego elementu wejściowej sekwencji.
command-description-MulVecCommand =
    Mnoży każdy element wejścia przez skalar.
command-description-DivVecCommand =
    Dzieli każdy element wejścia przez skalar.
command-description-rng-to =
    Zwraca liczbę między wejściem (włącznie) a argumentem (wyłącznie).
command-description-rng-from =
    Zwraca liczbę między argumentem (włącznie) a wejściem (wyłącznie).
command-description-rng-prob =
    Zwraca bool na podstawie podanego prawdopodobieństwa (0–1).
command-description-sum =
    Oblicza sumę elementów wejściowych.
command-description-bin =
    „Grupuje” wejście, zliczając ile razy każdy unikalny element występuje.
command-description-extremes =
    Zwraca skrajne wartości listy, przeplatane.
command-description-sortby =
    Sortuje wejście od najmniejszego do największego według obliczanego klucza.
command-description-sortmapby =
    Sortuje wejście od najmniejszego do największego według obliczanego klucza, zastępując wartość jej kluczem.
command-description-sort =
    Sortuje wejście od najmniejszego do największego.
command-description-sortdownby =
    Sortuje wejście od największego do najmniejszego według obliczanego klucza.
command-description-sortmapdownby =
    Sortuje wejście od największego do najmniejszego według obliczanego klucza, zastępując wartość jej kluczem.
command-description-sortdown =
    Sortuje wejście od największego do najmniejszego.
command-description-iota =
    Zwraca listę liczb od 1 do N.
command-description-to =
    Zwraca listę liczb od N do M.
command-description-curtick =
    Aktualny tick gry.
command-description-curtime =
    Aktualny czas gry (TimeSpan).
command-description-realtime =
    Aktualny czas rzeczywisty od uruchomienia (TimeSpan).
command-description-servertime =
    Aktualny czas gry na serwerze lub zero jeśli jesteśmy serwerem (TimeSpan).
command-description-replace =
    Zastępuje podane encje danym prototypem, zachowując pozycję i rotację (nic więcej).
command-description-allcomps =
    Zwraca wszystkie komponenty podanej encji.
command-description-entitysystemupdateorder-tick =
    Wypisuje kolejność aktualizacji systemów encji według ticków.
command-description-entitysystemupdateorder-frame =
    Wypisuje kolejność aktualizacji systemów encji według klatek.
command-description-more =
    Wypisuje zawartość $more, czyli dodatkowe elementy, które Toolshed nie wypisał w ostatniej komendzie.
command-description-ModulusCommand =
    Oblicza resztę z dzielenia dwóch wartości.
    Zwykle jest to reszta, patrz dokumentacja C# dla typu.
command-description-ModVecCommand =
    Wykonuje operację modulo na wejściu z podaną stałą wartością po prawej stronie.
command-description-BitAndNotCommand =
    Wykonuje bitowe AND-NOT na wejściu.
command-description-bitornot =
    Wykonuje bitowe OR-NOT na wejściu.
command-description-BitXnorCommand =
    Wykonuje bitowe XNOR na wejściu.
command-description-BitNotCommand =
    Wykonuje bitowe NOT na wejściu.
command-description-abs =
    Zwraca wartość bezwzględną wejścia (usuwa znak).
command-description-average =
    Oblicza średnią arytmetyczną wejścia.
command-description-bibytecount =
    Zwraca rozmiar wejścia w bajtach, jeśli implementuje IBinaryInteger.
    To NIE jest sizeof.
command-description-shortestbitlength =
    Zwraca minimalną liczbę bitów potrzebnych do reprezentacji wartości.
command-description-countleadzeros =
    Zlicza liczbę wiodących zer binarnych w wartości wejściowej.
command-description-counttrailingzeros =
    Zlicza liczbę zer końcowych w wartości wejściowej.
command-description-fpi =
    pi (3.14159...) jako float.
command-description-fe =
    e (2.71828...) jako float.
command-description-ftau =
    tau (6.28318...) jako float.
command-description-fepsilon =
    Wartość epsilon dla float, dokładnie 1.4e-45.
command-description-dpi =
    pi (3.14159...) jako double.
command-description-de =
    e (2.71828...) jako double.
command-description-dtau =
    tau (6.28318...) jako double.
command-description-depsilon =
    Wartość epsilon dla double, dokładnie 4.9406564584124654E-324.
command-description-hpi =
    pi (3.14...) jako half.
command-description-he =
    e (2.71...) jako half.
command-description-htau =
    tau (6.28...) jako half.
command-description-hepsilon =
    Wartość epsilon dla half, dokładnie 5.9604645E-08.
command-description-floor =
    Zwraca podłogę wartości wejściowej (zaokrągla w kierunku zera).
command-description-ceil =
    Zwraca sufit wartości wejściowej (zaokrągla od zera).
command-description-round =
    Zaokrągla wartość wejściową.
command-description-trunc =
    Obcina część ułamkową wartości wejściowej.
command-description-round2frac =
    Zaokrągla wartość wejściową do podanej liczby miejsc po przecinku.
command-description-exponentbytecount =
    Zwraca liczbę bajtów potrzebnych do przechowania wykładnika.
command-description-significandbytecount =
    Zwraca liczbę bajtów potrzebnych do przechowania mantysy.
command-description-significandbitcount =
    Zwraca dokładną liczbę bitów mantysy.
command-description-exponentshortestbitcount =
    Zwraca minimalną liczbę bitów potrzebnych do przechowania wykładnika.
command-description-stepnext =
    Przechodzi do następnej wartości float, dodając 1 do mantysy z przeniesieniem.
command-description-stepprev =
    Przechodzi do poprzedniej wartości float, odejmując 1 od mantysy z przeniesieniem.
command-description-checkedto =
    Konwertuje z podanego typu liczbowego do docelowego, zwracając błąd jeśli niemożliwe.
command-description-saturateto =
    Konwertuje z podanego typu liczbowego do docelowego, saturując wartość jeśli wychodzi poza zakres.
    Na przykład konwersja 382 na byte daje 255 (maksymalna wartość byte).
command-description-truncto =
    Konwertuje z podanego typu liczbowego do docelowego, z obcięciem.
    Dla liczb całkowitych to rzut bitowy z rozszerzeniem znaku.
command-description-iscanonical =
    Zwraca true jeśli wejście jest w formie kanonicznej.
command-description-iscomplex =
    Zwraca true jeśli wejście jest liczbą zespoloną (wartość, nie typ).
command-description-iseven =
    Zwraca true jeśli wejście jest liczbą parzystą.
    Nie jest pakietem JavaScript.
command-description-isodd =
    Zwraca true, jeśli wejście jest nieparzyste.
command-description-isfinite =
    Zwraca true, jeśli wejście jest skończone.
command-description-isimaginary =
    Zwraca true, jeśli wejście jest czysto urojone (bez części rzeczywistej).
command-description-isinfinite =
    Zwraca true, jeśli wejście jest nieskończone.
command-description-isinteger =
    Zwraca true, jeśli wejście jest liczbą całkowitą (wartość, nie typ).
command-description-isnan =
    Zwraca true, jeśli wejście jest Not a Number (NaN).
    To specjalna wartość zmiennoprzecinkowa, więc sprawdzane jest po wartości, nie po typie.
command-description-isnegative =
    Zwraca true, jeśli wejście jest ujemne.
command-description-ispositive =
    Zwraca true, jeśli wejście jest dodatnie.
command-description-isreal =
    Zwraca true, jeśli wejście jest czysto rzeczywiste (bez części urojonej).
command-description-issubnormal =
    Zwraca true, jeśli wejście jest w formie subnormalnej.
command-description-iszero =
    Zwraca true, jeśli wejście jest równe zero.
command-description-pow =
    Oblicza potęgę: lewa strona podniesiona do prawej. x^y.
command-description-sqrt =
    Oblicza pierwiastek kwadratowy z wejścia.
command-description-cbrt =
    Oblicza pierwiastek sześcienny z wejścia.
command-description-root =
    Oblicza N-ty pierwiastek z wejścia.
command-description-hypot =
    Oblicza długość przeciwprostokątnej trójkąta o bokach A i B.
command-description-sin =
    Oblicza sinus wartości wejściowej.
command-description-sinpi =
    Oblicza sinus wartości wejściowej pomnożonej przez pi.
command-description-asin =
    Oblicza arcsinus wartości wejściowej.
command-description-asinpi =
    Oblicza arcsinus wartości wejściowej pomnożonej przez pi.
command-description-cos =
    Oblicza cosinus wartości wejściowej.
command-description-cospi =
    Oblicza cosinus wartości wejściowej pomnożonej przez pi.
command-description-acos =
    Oblicza arccosinus wartości wejściowej.
command-description-acospi =
    Oblicza arccosinus wartości wejściowej pomnożonej przez pi.
command-description-tan =
    Oblicza tangens wartości wejściowej.
command-description-tanpi =
    Oblicza tangens wartości wejściowej pomnożonej przez pi.
command-description-atan =
    Oblicza arctangens wartości wejściowej.
command-description-atanpi =
    Oblicza arctangens wartości wejściowej pomnożonej przez pi.
command-description-iterate =
    Wykonuje daną funkcję na wejściu N razy, zwracając listę wyników.
    Można to traktować jako kolejne stosowanie funkcji do wartości, śledząc wszystkie pośrednie wyniki.
command-description-pick =
    Wybiera losową wartość z wejścia.
command-description-tee =
    Przekazuje wejście do podanego bloku, ignorując wynik bloku.
    Pozwala to na wykonanie wielu operacji na jednej wartości w kodzie.
command-description-cmd-info =
    Zwraca CommandSpec dla podanej komendy.
    Samodzielnie oznacza to, że wypisze komunikat pomocy dla komendy.
command-description-comp-rm =
    Usuwa podany komponent z encji.
