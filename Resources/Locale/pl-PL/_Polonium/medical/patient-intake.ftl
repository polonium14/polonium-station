## Maszyna

patient-intake-verb-on = Włącz
patient-intake-verb-off = Wyłącz
patient-intake-verb-missing = Zgłoś zaginięcie pacjenta

patient-intake-popup-on = Izba przyjęć uruchamia się.
patient-intake-popup-off = Izba przyjęć wyłącza się.
patient-intake-popup-access = Brak dostępu: do włączenia potrzebny jest dostęp medyczny.
patient-intake-popup-missing-access = Brak dostępu: może to zrobić tylko ordynator albo kapitan.
patient-intake-popup-mind = Szuflada się nie zamknie: leży na niej osoba albo coś, w czym jest osoba.
patient-intake-popup-no-patient = Szuflada się nie zamknie: do środka może trafić tylko ciało przydzielonego pacjenta.

patient-intake-examine-running = [color=green]Przyjmuje zlecenia[/color].
patient-intake-examine-stopped = [color=red]Nie przyjmuje zleceń[/color].
patient-intake-examine-case = Aktywne zlecenie nr {$number}: {$case}.
patient-intake-examine-time = Zostało około {$minutes} min.
patient-intake-examine-overdue = [color=red]Limit czasu minął.[/color]
patient-intake-examine-clock-waiting = Czas zacznie płynąć po pierwszym otwarciu szuflady.

patient-intake-examine-fit = [color=green]Gotowość do wypisu.[/color]
patient-intake-examine-unfit = [color=red]Nie nadaje się do wypisu:[/color]
patient-intake-examine-line = - {$line}

patient-intake-radio-arrived = Izba przyjęć: dotarło zlecenie nr {$number} ({$case}){ $total ->
    [1] .
   *[other] , pacjent {$patient} z {$total}.
}
patient-intake-radio-finished = Izba przyjęć: zlecenie nr {$number} zamknięte, {$amount} spesos dla budżetu medycznego.
patient-intake-radio-finished-debit = Izba przyjęć: zlecenie nr {$number} zamknięte, budżet medyczny obciążony kwotą {$amount} spesos.

patient-intake-round-end-header = [bold]Izba przyjęć[/bold]
patient-intake-round-end-line = - zamknięte zlecenia: {$cases}, wynik dla budżetu medycznego: {$amount} spesos

## Rodzaje zleceń

patient-intake-case-easy = Rutynowe
patient-intake-case-medium = Standardowe
patient-intake-case-hard = Skomplikowane
patient-intake-case-timed = Zlecenie pilne
patient-intake-case-super-hard = Zlecenie krytyczne
patient-intake-case-mass-casualty = Zdarzenie masowe
patient-intake-case-request = Życzenie pacjenta
patient-intake-case-carp-attack = Atak kosmicznych karpi
patient-intake-case-burn-ward = Oddział oparzeń
patient-intake-case-workplace-accident = Wypadek przy pracy

## Karta pacjenta

patient-intake-file-name = karta pacjenta nr {$number}
patient-intake-file-header = [head=2]Izba przyjęć: zlecenie nr {$number}[/head]
patient-intake-kind = [bold]{$case}[/bold]
patient-intake-file-patient = Pacjent: {$name} ({$species})
patient-intake-file-deadline = Limit czasu: {$minutes} min od pierwszego otwarcia szuflady. Im dłużej trwa leczenie, tym niższa zapłata.
patient-intake-file-deadline-hard = Limit czasu: {$minutes} min od pierwszego otwarcia szuflady. [color=red]Spóźnienie obciąża budżet medyczny.[/color]
patient-intake-file-payout = Zapłata: do {$amount} spesos.
patient-intake-file-mass = To zlecenie obejmuje {$count} pacjentów. Przybywają pojedynczo: wyjmij pacjenta i zamknij pustą szufladę, aby przyjąć kolejnego. Każdego oddaj osobno.
patient-intake-file-issues = [bold]Rozpoznanie:[/bold]
patient-intake-file-symptoms = [bold]Zgłaszane objawy:[/bold]
patient-intake-file-gear = [bold]Rzeczy osobiste (oddaj je w szufladzie):[/bold]
patient-intake-file-rules = Oddaj pacjenta żywego i w pełni zdrowego: bez obrażeń, złamań i uszkodzeń narządów, bez krwawienia, bez brakujących części ciała, z zamkniętymi nacięciami, z prawidłową ilością krwi, bez trucizn, ze zdrowymi oczami i bez implantów. Połóż pacjenta na szufladzie razem z rzeczami osobistymi i ją zamknij. [bold]Wszystko inne na szufladzie zostanie zniszczone.[/bold] Zamknięcie szuflady oddaje pacjenta, wyleczonego lub nie.
    Zbadaj pacjenta z analizatorem zdrowia w ręce, aby zobaczyć, co jeszcze trzeba wyleczyć.

## Pokwitowanie

patient-intake-receipt-name = pokwitowanie izby przyjęć nr {$number}
patient-intake-receipt-header = [head=2]Izba przyjęć: pokwitowanie nr {$number}[/head]
patient-intake-receipt-pass = {$name}: [color=green]wynik pozytywny[/color], {$amount} spesos.
patient-intake-receipt-pass-late = {$name}: [color=green]wynik pozytywny[/color], ale [color=yellow]po terminie[/color], {$amount} spesos.
patient-intake-receipt-fail = {$name}: [color=red]leczenie niezakończone[/color], bez zapłaty.
patient-intake-receipt-dead = {$name}: [color=red]zgon przed oddaniem[/color], obciążenie {$amount} spesos.
patient-intake-receipt-late = {$name}: [color=red]oddanie po terminie[/color], obciążenie {$amount} spesos.
patient-intake-receipt-gibbed = {$name}: [color=red]ciało zniszczone[/color], obciążenie {$amount} spesos.
patient-intake-receipt-destroyed = {$name}: [color=red]ciało utracone[/color], obciążenie {$amount} spesos.
patient-intake-receipt-mind = {$name}: [color=red]ciało przejęte[/color], obciążenie {$amount} spesos.
patient-intake-receipt-missing = Zlecenie zgłoszone jako zaginione, obciążenie {$amount} spesos.
patient-intake-receipt-gear = - Brak rzeczy osobistych (sztuk: {$count}): -{$amount} spesos ({$percent}%)
patient-intake-receipt-time = - Czas: {$time} (limit {$limit} min)
patient-intake-receipt-time-unopened = - Czas: szuflada nie została otwarta
patient-intake-receipt-base = - Stawka podstawowa: {$amount} spesos
patient-intake-receipt-time-cut = - Kara za czas: -{$amount} spesos
patient-intake-receipt-total = [bold]Razem: {$amount} spesos.[/bold]

## Niezaliczone

patient-intake-failure-dead = Pacjent nie żyje
patient-intake-failure-damage = Ciało wciąż uszkodzone
patient-intake-failure-limbdamage = {$part}: obrażenia
patient-intake-failure-organdamage = {$part}: uszkodzenie narządu
patient-intake-failure-trauma = {$part}: uraz (złamanie, uszkodzenie narządu lub nerwu)
patient-intake-failure-bleeding = Krwawienie
patient-intake-failure-missingpart = Brak: {$part}
patient-intake-failure-unfinishedsurgery = {$part}: niedokończona operacja
patient-intake-failure-lowblood = Za mało krwi
patient-intake-failure-harmfulreagent = Szkodliwa substancja we krwi: {$part}
patient-intake-failure-blind = Uszkodzone oczy lub ślepota
patient-intake-failure-implant = Wciąż wszczepiony implant: {$part}
patient-intake-failure-requestnotdone = Życzenie niespełnione: wymień narząd ({$part})

patient-intake-part-torso = tułów
patient-intake-part-head = głowa
patient-intake-part-armleft = lewa ręka
patient-intake-part-armright = prawa ręka
patient-intake-part-handleft = lewa dłoń
patient-intake-part-handright = prawa dłoń
patient-intake-part-legleft = lewa noga
patient-intake-part-legright = prawa noga
patient-intake-part-footleft = lewa stopa
patient-intake-part-footright = prawa stopa
patient-intake-part-brain = mózg
patient-intake-part-eyes = oczy
patient-intake-part-tongue = język
patient-intake-part-appendix = wyrostek
patient-intake-part-ears = uszy
patient-intake-part-lungs = płuca
patient-intake-part-heart = serce
patient-intake-part-stomach = żołądek
patient-intake-part-liver = wątroba
patient-intake-part-kidneys = nerki
patient-intake-part-beak = dziób

## Dolegliwości

patient-intake-issue-bruising = Uraz tępy kończyny
patient-intake-symptom-bruising = Siniaki i ból
patient-intake-issue-crush = Ciężki uraz zmiażdżeniowy kończyny
patient-intake-symptom-crush = Przygniecenie ciężkim przedmiotem
patient-intake-issue-laceration = Głęboka rana cięta
patient-intake-symptom-laceration = Poważne skaleczenie
patient-intake-issue-puncture = Rana kłuta
patient-intake-symptom-puncture = Upadek na coś ostrego
patient-intake-issue-burn = Oparzenia
patient-intake-symptom-burn = Kontakt z gorącą powierzchnią
patient-intake-issue-severe-burn = Ciężkie oparzenia
patient-intake-symptom-severe-burn = Pożar
patient-intake-issue-frostbite = Odmrożenia
patient-intake-symptom-frostbite = Długi pobyt w chłodni
patient-intake-issue-fracture = Złamana kość
patient-intake-symptom-fracture = Upadek ze schodów, kończyna niesprawna
patient-intake-issue-organ = Uszkodzenie narządu wewnętrznego
patient-intake-symptom-organ = Ból brzucha po wypadku
patient-intake-issue-eye-trauma = Uraz oka
patient-intake-symptom-eye-trauma = Uderzenie w głowę
patient-intake-issue-poisoning = Zatrucie toksyną
patient-intake-symptom-poisoning = Mdłości po zjedzeniu czegoś dziwnego
patient-intake-issue-carp-poisoning = Zatrucie karpiotoksyną
patient-intake-symptom-carp-poisoning = Źle przyrządzona ryba
patient-intake-issue-blood-loss = Utrata krwi z rany
patient-intake-symptom-blood-loss = Bladość, zawroty głowy, duża utrata krwi
patient-intake-issue-blind = Uszkodzenie oczu
patient-intake-symptom-blind = Nieostre widzenie
patient-intake-issue-implant = Nielegalny implant śledzący (usuń go)
patient-intake-symptom-implant = Uczucie czegoś pod skórą
patient-intake-issue-missing-organ = Brak narządu wewnętrznego
patient-intake-symptom-missing-organ = Ofiara kradzieży narządu
patient-intake-issue-missing-extremity = Brak dłoni lub stopy
patient-intake-symptom-missing-extremity = Utrata dłoni lub stopy w wypadku
patient-intake-issue-replace-eyes = Prośba o nowe oczy (przeszczep)
patient-intake-issue-replace-liver = Prośba o przeszczep wątroby
patient-intake-issue-replace-lungs = Prośba o przeszczep płuc

## Łączenie urządzeń

signal-port-name-patient-intake-fax = Faks
signal-port-description-patient-intake-fax = Wysyła karty pacjentów i pokwitowania do faksu.
signal-port-name-patient-intake-fax-receiver = Izba przyjęć
signal-port-description-patient-intake-fax-receiver = Drukuje karty pacjentów i pokwitowania z izby przyjęć.

## Komenda administracyjna

cmd-patientintake_start-desc = Od razu rozpoczyna zlecenie izby przyjęć, z pominięciem licznika czasu i wymogu obsady.
cmd-patientintake_start-help = Użycie: patientintake_start <uid maszyny> [id zlecenia]
    Bez id zlecenia może zostać wylosowany dowolny rodzaj.
cmd-patientintake_start-arg-machine = <uid maszyny>
cmd-patientintake_start-arg-case = [id zlecenia]
cmd-patientintake_start-no-machine = {$uid} nie jest izbą przyjęć.
cmd-patientintake_start-busy = Ta maszyna ma już aktywne zlecenie.
cmd-patientintake_start-open = Najpierw zamknij szufladę.
cmd-patientintake_start-no-case = Nie ma zlecenia izby przyjęć o nazwie {$case}.
cmd-patientintake_start-started = Rozpoczęto zlecenie {$case}.
