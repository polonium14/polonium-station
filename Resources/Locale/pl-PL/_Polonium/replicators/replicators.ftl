materials-replisteel = replistal
stack-replisteel = replistal
replicator-hand-replisteel-empty = Replistal
tiles-replicator-floor = podłoga replikatorów

alerts-replicator-recall-immune-name = Ślad nanitów
alerts-replicator-recall-immune-desc = Po Przywołaniu Roju wciąż oblepiają cię nanity. Replikatory nie mogą cię ponownie teleportować, dopóki ślad nie zniknie.

replicator-construction-fail-no-floor = Nie ma tu podłogi, na której dałoby się pracować.
replicator-construction-fail-already-ours = To już należy do roju.
replicator-construction-fail-wall = Nanity nie są w stanie przejąć tej ściany.
replicator-construction-fail-budget = W gnieździe brakuje replistali, to kosztuje { $cost } { $cost ->
        [one] arkusz
        [few] arkusze
        [many] arkuszy
       *[other] arkusza
    }.
replicator-construction-fail-occupied = Coś stoi na przeszkodzie.
replicator-construction-fail-no-nest = Nie masz gniazda, z którego można czerpać replistal.

replicator-fabricator-recipe-name = korpus replikatora
replicator-fabricator-recipe-desc = Śmiertelna maszyna, która lada chwila się obudzi
replicator-fabricator-examine-working = Składa korpus, zostało [color=violet]{ $seconds } s[/color].
replicator-fabricator-examine-ready = Gotowy korpus czeka, aż zajmie go umysł.
replicator-fabricator-examine-idle = Stoi bezczynnie.
replicator-fabricator-limit = Rój nie utrzyma teraz więcej niż { $limit } replikatorów.

replicator-hive-unlocked = Gniazdo osiągnęło poziom { $level }. Teraz dostępne: { $structures }.
replicator-hive-examine = W gnieździe leży [color=violet]{ $budget }[/color] arkusza replistali, z czego { $reserve } zostaje w rezerwie. Gniazdo jest na poziomie { $level }.

replicator-teleport-prey-fail-immune = Ten cel wciąż oblepiają nanity, nie da się go jeszcze przywołać.
replicator-on-structure-attack-fail = Nie możesz niszczyć tego, co zbudował rój.

name-identifier-format-replicator = RL-{ $number }
name-identifier-format-replicator-colossus = KL-{ $number }

replicator-colossus-pulse-target = Fala bluespace'u porywa cię daleko stąd!

replicator-colossus-announcement = Centralne Dowództwo wykryło anomalny skok energii bluespace'u. Namiar: { $location }. Zniszczcie zagrożenie za wszelką cenę!
replicator-colossus-defeat-announcement = Sygnatura bluespace'owa zniknęła. Usuńcie pozostałości zagrożenia!
replicator-colossus-foundry-warning = Centralne Dowództwo odbiera rosnące odczyty bluespace'u. Namiar: { $location }. Replikatory składają coś ogromnego. Znajdźcie to i rozbierzcie, zanim się uruchomi.

replicator-colossus-foundry-fail-built = Rój już postawił w tej zmianie swoją odlewnię kolosa.
replicator-colossus-foundry-fail-level = Gniazdo musi najpierw osiągnąć poziom { $level }.
replicator-colossus-foundry-fail-floor = Odlewnia potrzebuje litej podłogi na { $size } płytek wszerz, bez dziur w kosmos.
replicator-colossus-foundry-fail-blocked = Coś stoi na drodze. Odlewnia potrzebuje wolnego kawałka podłogi { $size }x{ $size }.
replicator-colossus-foundry-fail-hive-floor = Odlewnia potrzebuje pod sobą kawałka podłogi replikatorów { $size }x{ $size }.
replicator-colossus-foundry-fail-mob = Ktoś stoi w miejscu, gdzie ma powstać odlewnia.
replicator-colossus-foundry-part-not-replicator = Nie masz pojęcia, gdzie to pasuje.
replicator-colossus-foundry-part-complete = Kolos jest już złożony.
replicator-colossus-foundry-part-wrong = Odlewnia potrzebuje teraz części: { $part }.
replicator-colossus-foundry-complete = Kolos jest złożony. Zainicjuj go przy odlewni.
replicator-colossus-foundry-verb-initialize = Inicjalizuj
replicator-colossus-foundry-awaiting = Kolos już czeka na umysł.
replicator-colossus-foundry-incomplete = Nie wszystkie części są jeszcze na miejscu.
replicator-colossus-foundry-initializing = Rdzeń kolosa budzi się i szuka umysłu.
replicator-colossus-foundry-examine-stage = Montaż: [color=violet]{ $stage }/{ $total }[/color] części na miejscu.
replicator-colossus-foundry-examine-next = Następna część: [color=violet]{ $part }[/color].
replicator-colossus-foundry-examine-awaiting = Rdzeń pulsuje w ciemności, czekając na umysł.

alert-level-delta-colossus-announcement = Na pokładzie stacji grasuje Kolosalny Replikator. Załoga ma schodzić mu z drogi i wykonywać polecenia szefów działów.

ghost-role-information-replicator-colossus-name = Kolosalny Replikator
ghost-role-information-replicator-colossus-desc = Ukoronowanie dzieła roju. Przejdź przez stację, przerób ją dla roju i zmiażdż każdego, kto stanie ci na drodze.

replicator-construction-fail-reinforce-foreign = To nie jest ściana roju.
replicator-construction-fail-reinforce-done = Ta ściana jest już wzmocniona.
replicator-construction-fail-no-wall = Nie ma tam ściany do przejęcia.
replicator-construction-fail-not-hive-floor = Rój buduje tylko na podłodze replikatorów.
replicator-build-selected = Wybrano: { $structure }. Wskaż podłogę replikatorów, trzymając replistal w ręce.
replicator-build-fail-range = To za daleko.
replicator-build-fail-level = Gniazdo musi najpierw osiągnąć poziom { $level }.
replicator-build-fail-sheets = Potrzebujesz do tego { $cost } { $cost ->
        [one] arkusz
        [few] arkusze
        [many] arkuszy
       *[other] arkusza
    } replistali. Arkusze w twojej ręce i zapas gniazda razem na to nie wystarczą.
replicator-build-menu-entry = { CAPITALIZE($structure) } ({ $cost } { $cost ->
        [one] arkusz
        [few] arkusze
        [many] arkuszy
       *[other] arkusza
    })
replicator-build-menu-locked = { CAPITALIZE($structure) }: gniazdo musi osiągnąć poziom { $level }
replicator-hive-withdraw-reserve = Gniazdo trzyma { $reserve } arkusza w rezerwie na zdolności roju.
replicator-duplicator-recipe-name = powiel replistal
replicator-duplicator-recipe-desc = Jeden arkusz wchodzi, dwa wychodzą.

replicator-evolution-title = Komora ewolucji
replicator-evolution-stock = Zapas gniazda: { $budget } arkusza
replicator-evolution-level = Poziom gniazda: { $level }
replicator-evolution-grown = Wyhodowane stadia: { $grown } z { $limit }
replicator-evolution-grow = Wyhoduj ({ $cost } { $cost ->
        [one] arkusz
        [few] arkusze
        [many] arkuszy
       *[other] arkusza
    })
replicator-evolution-complete = W pełni rozwinięte
replicator-module-fail-complete = Ta gałąź jest już w pełni rozwinięta.
replicator-module-fail-full = Twój korpus pomieści najwyżej { $limit } stadiów ulepszeń.
replicator-module-started = Ramiona komory zaciskają się wokół ciebie.
replicator-module-installed = { $module }: wyhodowano stadium { $stage }.
replicator-evolution-no-class = Korpus: uniwersalny
replicator-evolution-class = Klasa: { $class }
replicator-evolution-choose = Wybierz klasę. Komora przebudowuje cały twój korpus do jednej roli. Od tej roli zależy, które gałęzie możesz hodować. [color=#ffff91]Wybór jest ostateczny.[/color]
replicator-evolution-branches = Gałęzie: { $branches }
replicator-evolution-specialize = Przebuduj korpus
replicator-evolution-specialize-cost = Przebuduj korpus ({ $cost } { $cost ->
        [one] arkusz
        [few] arkusze
        [many] arkuszy
       *[other] arkusza
    })
replicator-evolution-form = { $stages ->
        [0] Forma { $number }: zaraz po przebudowie
       *[other] Forma { $number }: po wyhodowaniu { $stages } stadiów
    }
replicator-class-fail-chosen = Twój korpus został już przebudowany do jednej roli.
replicator-module-fail-class = Twoja klasa nie hoduje tej gałęzi.
replicator-class-installed = Komora przebudowuje twój korpus: { $class }.
replicator-class-fighter-name = Wojownik
replicator-class-fighter-desc = Klinowata głowa między dwiema skrzynkowymi lufami z rogami stateczników, a pod brodą ząbkowany lemiesz taranu. Kruszy konstrukcje i nie ustępuje w walce.
replicator-class-defender-name = Obrońca
replicator-class-defender-desc = Ciężki opancerzony kadłub na dyszach, z masywnymi barkami i pojedynczym okiem czujnika pod pasem światła. Przyjmuje obrażenia, a w pełni rozwinięty naprawia resztę roju. Porusza się wolniej niż inne korpusy.
replicator-class-scout-name = Zwiadowca
replicator-class-scout-desc = Wysoka wąska iglica między dwoma szerokimi, odchylonymi do tyłu ostrzami ze światłami pozycyjnymi, z przodu jedno oko w kształcie szczeliny. Najszybszy korpus w roju, potrafi też zniknąć z oczu.
replicator-class-builder-name = Budowniczy
replicator-class-builder-desc = Niska, szeroka barka na dyszach, z bębnem silnika po każdej stronie i przypiętym stosem płyt na grzbiecie. Szybciej buduje i przekształca oraz dowozi replistal do gniazda.
replicator-class-hunter-name = Łowca
replicator-class-hunter-desc = Szeroki klin ze świecącymi zębami wzdłuż krawędzi, dwoma emiterami na czole i jednym okiem w kształcie szczeliny. Szybciej przywołuje ofiary i tropi załogę dla całego roju.
replicator-module-thrusters-name = Dopalacze
replicator-module-thrusters-1 = Poruszasz się o 10% szybciej.
replicator-module-thrusters-2 = Poruszasz się o 20% szybciej.
replicator-module-thrusters-3 = Poruszasz się o 30% szybciej i zyskujesz zryw dopalaczy: skok o mniej więcej trzy kafelki co 15 sekund.
replicator-module-cloak-name = Kamuflaż
replicator-module-cloak-1 = Stojąc w miejscu, stajesz się półprzezroczysty. Ruch i atak cię zdradzają.
replicator-module-cloak-2 = Stojąc w miejscu, znikasz całkowicie.
replicator-module-cloak-3 = Pozostajesz ukryty, gdy poruszasz się powoli. Bieg i atak nadal cię zdradzają.
replicator-module-plating-name = Opancerzenie
replicator-module-plating-1 = Otrzymujesz o 15% mniej obrażeń.
replicator-module-plating-2 = Otrzymujesz o 30% mniej obrażeń.
replicator-module-plating-3 = Otrzymujesz o 40% mniej obrażeń, a broń ogłuszająca wyczerpuje cię o połowę wolniej.
replicator-module-emitter-name = Emiter
replicator-module-emitter-1 = Twój promień ogłuszający mieści 8 ładunków zamiast 6 i strzela o 20% szybciej.
replicator-module-emitter-2 = Twój promień ogłuszający mieści 10 ładunków, szybciej je odzyskuje i strzela o 40% szybciej.
replicator-module-emitter-3 = Twój promień ogłuszający mieści 13 ładunków, odzyskuje je jeszcze szybciej i strzela o 50% szybciej, a co 20 sekund możesz wystrzelić wachlarzem trzy promienie naraz.
replicator-module-ram-name = Taran
replicator-module-ram-1 = Twój cios bez broni uderza w konstrukcje dwa razy mocniej.
replicator-module-ram-2 = Twój cios bez broni odbiera też o połowę więcej wytrzymałości.
replicator-module-ram-3 = Wyrasta ci ciężkie ramię: szerokie zamachy, które rozrywają konstrukcje i podważają drzwi.
replicator-module-toolkit-name = Zestaw narzędzi
replicator-module-toolkit-1 = Budujesz i przekształcasz o 20% szybciej.
replicator-module-toolkit-2 = Budujesz i przekształcasz o 40% szybciej i wyrasta ci omninarzędzie.
replicator-module-toolkit-3 = Wyrasta ci spawarka, przekształcenie podłogi pod tobą obejmuje też cztery sąsiednie kafelki i możesz wzmacniać ściany roju.
replicator-module-regeneration-name = Regeneracja
replicator-module-regeneration-1 = Naprawiasz się dwa razy szybciej.
replicator-module-regeneration-2 = Naprawiasz się trzy razy szybciej.
replicator-module-regeneration-3 = Możesz naprawiać inne replikatory i konstrukcje roju: 25 obrażeń za każde sześć sekund stania obok w bezruchu.
replicator-module-capture-name = Układ przywołania
replicator-module-capture-1 = Przywołanie Roju ładuje się o 25% szybciej.
replicator-module-capture-2 = Przywołany cel pozostaje sparaliżowany o dwie sekundy dłużej.
replicator-module-capture-3 = Przywołany cel można przywołać ponownie po 15 sekundach zamiast 25.
replicator-module-cargo-name = Ładownia
replicator-module-cargo-1 = Wyrasta ci druga ręka na replistal.
replicator-module-cargo-2 = To, co ciągniesz, prawie cię nie spowalnia.
replicator-module-cargo-3 = Raz na minutę możesz wysłać trzymaną replistal prosto do zapasu gniazda.
replicator-module-sensors-name = Czujniki
replicator-module-sensors-1 = Strzałka wskazuje ci najbliższego żywego członka załogi.
replicator-module-sensors-2 = Możesz oznaczyć żywy cel: przez 45 sekund cały rój widzi go przez ściany.
replicator-module-sensors-3 = Raz na minutę impuls skanujący oznacza na 20 sekund każdego żywego członka załogi w promieniu dwunastu kafelków.
replicator-repair-fail-self = W ten sposób nie naprawisz samego siebie.
replicator-repair-fail-target = Naprawiać możesz tylko replikatory i konstrukcje roju.
replicator-repair-fail-intact = Nie ma tam nic do naprawy.
replicator-cargo-fail-empty = Nie trzymasz nic, co przydałoby się gniazdu.
replicator-cargo-sent = Wysłano do gniazda równowartość { $amount } arkusza.
replicator-mark-fail-target = Oznaczyć można tylko żywy cel.
replicator-mark-applied = Cel oznaczony na { $seconds } sekund.
replicator-scan-result = { $count ->
        [0] Impuls nikogo nie znalazł.
       *[other] Impuls oznaczył członków załogi: { $count }.
    }

replicator-teleport-prey-fail-warded = Coś na tym celu rozprasza namiar. Nie da się go przywołać.

replicator-colossus-beacon-fail-exists = Twój nadajnik już stoi.
replicator-colossus-beacon-announcement = Centralne Dowództwo namierzyło na waszej stacji nadajnik redspace'u. Namiar: { $location }. Jego energia narasta i za { $minutes } minut osiągnie szczyt.
replicator-colossus-beacon-warning = Sygnał nadajnika ({ $location }) wciąż narasta. Szczyt osiągnie za { $minutes } { $minutes ->
        [one] minutę
        [few] minuty
        [many] minut
       *[other] minuty
    }.
replicator-colossus-beacon-ready-announcement = Sygnał nadajnika ({ $location }) osiągnął szczyt. Cokolwiek miało go usłyszeć, już słucha.
replicator-colossus-beacon-ready = Nadajnik jest naładowany. Podejdź do niego, a podłączy się do twojego rdzenia.
replicator-colossus-beacon-destroyed-announcement = Nadajnik został zniszczony i jego sygnał ucichł. Kolosalny Replikator może postawić kolejny, ale sygnał będzie musiał narosnąć od nowa.
replicator-colossus-beacon-lost = Twój nadajnik został zniszczony!
replicator-colossus-beacon-examine-charging = Naładuje się za [color=orange]{ $time }[/color].
replicator-colossus-beacon-examine-ready = [color=red]Jest w pełni naładowany.[/color]
replicator-colossus-link-start = Nadajnik podłącza się do twojego rdzenia. Dopóki nie skończy, nie możesz się ruszać ani strzelać.
replicator-colossus-link-broken = Połączenie zostało zerwane! Twoje uzbrojenie uruchamia się od nowa.
replicator-colossus-rearmed = Twoje uzbrojenie znów działa.
replicator-colossus-disarmed = Twoje systemy nie odpowiadają.
replicator-colossus-assimilate-announcement = Nadajnik ({ $location }) podłączył się do Kolosalnego Replikatora i czerpie z jego rdzenia. Według naszych obliczeń za { $seconds } sekund wszystko na pokładzie zostanie zniszczone. Kolos nie może się ruszać ani strzelać, dopóki zasila nadajnik. Uderzcie w nadajnik wszystkim, co macie, żeby zerwać połączenie.
replicator-colossus-assimilate-interrupted-announcement = Połączenie zostało zerwane, a nadajnik ({ $location }) stracił część ładunku. Jego sygnał znów osiągnie szczyt za { $minutes } { $minutes ->
        [one] minutę
        [few] minuty
        [many] minut
       *[other] minuty
    }. Kolos jest przez chwilę bezbronny.
replicator-colossus-assimilate-released-announcement = Asymilacja została uwolniona.
replicator-colossus-assimilated = Fala rozkłada cię na części i składa na nowo. Jesteś teraz replikatorem i służysz rojowi.

replicator-colossus-wreck-fail-replicator = Rój nie pozwoli ci rozciąć kolosa.
replicator-colossus-core-verb-study = Zbadaj
replicator-colossus-core-examine = Ktoś, kto poświęci czas na jego zbadanie, mógłby się dowiedzieć, jak trzymać nanity z daleka.
replicator-colossus-core-examine-studied = Został już zbadany.
replicator-colossus-core-announcement = Załoga zbadała rdzeń Kolosalnego Replikatora. Kamizelki antynanitowe można teraz drukować w każdej tokarce na stacji. Nie pozwalają replikatorom teleportować noszącego i odbierają większość siły ich ciosom oraz promieniom ogłuszającym.

chat-radio-replicator-hive = Rój
replicator-emp-stunned = Impuls rozstraja twoją sieć!

replicator-round-end-assimilated = [color=#d70aa0]Kolosalny Replikator zasymilował stację. Rój wygrał.[/color]
replicator-round-end-colossus-destroyed = [color=green]Kolosalny Replikator został zniszczony. Rój przegrał.[/color]

replicator-queen-crowned = Rój stracił królową. Nową królową zostaje { $queen }.
replicator-queen-crowned-self = Rój stracił królową. Od teraz to ty jesteś królową.
replicator-queen-crowned-nestless = Rój stracił królową i nie ma gniazda. Nową królową zostaje { $queen } i tylko ona może otworzyć nowe gniazdo.
replicator-queen-crowned-nestless-self = Rój stracił królową i nie ma gniazda. Od teraz to ty jesteś królową, więc otwórz nowe gniazdo zdolnością Produkuj Gniazdo.
replicator-queen-nest-lost = Gniazdo zostało zniszczone. Nowe może otworzyć tylko królowa, czyli { $queen }.
replicator-queen-nest-lost-self = Gniazdo zostało zniszczone. Jesteś królową, więc otwórz nowe zdolnością Produkuj Gniazdo.
