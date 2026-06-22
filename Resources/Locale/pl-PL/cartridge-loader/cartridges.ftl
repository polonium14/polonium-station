# Polskie tłumaczenie przez: @Tofi-Dev

device-pda-slot-component-slot-name-cartridge = Kartridż
default-program-name = Program
notekeeper-program-name = Notatnik
nano-task-program-name = NanoTask
news-read-program-name = Wiadomości Stacyjne
crew-manifest-program-name = Manifest Załogi
crew-manifest-cartridge-loading = Ładowanie ...
net-probe-program-name = NetProbe
net-probe-scan = Zeskanowano { $device }!
net-probe-label-name = Nazwa
net-probe-label-address = Adres
net-probe-label-frequency = Częstotliwość
net-probe-label-network = Sieć
log-probe-program-name = LogProbe
log-probe-scan = Pobrano logi z { $device }!
log-probe-label-time = Czas
log-probe-label-accessor = Odblokowane przez:
log-probe-label-number = #
log-probe-print-button = Wydrukuj Logi
log-probe-printout-device = Zeskanowane Urządzenie: { $name }
wordle-program-name = Wordle
log-probe-printout-header = Najnowsze logi:
log-probe-printout-entry = #{ $number } / { $time } / { $accessor }
astro-nav-program-name = AstroNav
med-tek-program-name = MedTek
plant-scan-program-name = PlantScan

# NanoTask cartridge

nano-task-ui-heading-high-priority-tasks =
    { $amount ->
        [zero] Brak
        [one] { $amount } Zadanie
        [few] { $amount } Zadania
        [4] { $amount } Zadania
       *[other] { $amount } Zadań
    } o Wysokim Priorytecie
nano-task-ui-heading-medium-priority-tasks =
    { $amount ->
        [zero] Brak
        [one] { $amount } Zadanie
        [few] { $amount } Zadania
        [4] { $amount } Zadania
       *[other] { $amount } Zadań
    } o Średnim Priorytecie
nano-task-ui-heading-low-priority-tasks =
    { $amount ->
        [zero] Brak
        [one] { $amount } Zadanie
        [few] { $amount } Zadania
        [4] { $amount } Zadania
       *[other] { $amount } Zadań
    } o Niskim Priorytecie
nano-task-ui-done = Gotowe
nano-task-ui-revert-done = Cofnij
nano-task-ui-priority-low = Niski
nano-task-ui-priority-medium = Średni
nano-task-ui-priority-high = Wysoki
nano-task-ui-cancel = Anuluj
nano-task-ui-print = Wydrukuj
nano-task-ui-delete = Usuń
nano-task-ui-save = Zapisz
nano-task-ui-new-task = Nowe Zadanie
nano-task-ui-description-label = Opis:
nano-task-ui-description-placeholder = Zrób coś ważnego.
nano-task-ui-requester-label = Żądający:
nano-task-ui-requester-placeholder = Jan Nanotrasen
nano-task-ui-item-title = Edytuj Zadanie
nano-task-printed-description = [bold]Opis[/bold]: { $description }
nano-task-printed-requester = [bold]Żądający[/bold]: { $requester }
nano-task-printed-high-priority = [bold]Priorytet[/bold]: [color=red]Wysoki[/color]
nano-task-printed-medium-priority = [bold]Priorytet[/bold]: Średni
nano-task-printed-low-priority = [bold]Priorytet[/bold]: Niski
# Wanted list cartridge
wanted-list-program-name = Lista Poszukiwanych
wanted-list-label-no-records = Jest wszystko dobrze, żołnierzu!
wanted-list-search-placeholder = Szukaj poprzez imie i status
wanted-list-age-label = [color=darkgray]Wiek:[/color] [color=white]{ $age }[/color]
wanted-list-job-label = [color=darkgray]Zawód:[/color] [color=white]{ $job }[/color]
wanted-list-species-label = [color=darkgray]Gatunek:[/color] [color=white]{ $species }[/color]
wanted-list-gender-label = [color=darkgray]Płeć:[/color] [color=white]{ $gender }[/color]
wanted-list-reason-label = [color=darkgray]Powód:[/color] [color=white]{ $reason }[/color]
wanted-list-unknown-reason-label = Nieznany Powód
wanted-list-initiator-label = [color=darkgray]Inicjator:[/color] [color=white]{ $initiator }[/color]
wanted-list-unknown-initiator-label = Nieznany Inicjator
wanted-list-status-label = [color=darkgray]status:[/color] { $status ->
        [suspected] [color=yellow]podejrzany[/color]
        [wanted] [color=red]poszukiwany[/color]
        [detained] [color=#b18644]zatrzymany[/color]
        [paroled] [color=green]zwol. warunkowo[/color]
        [discharged] [color=green]zwoloniony[/color]
       *[other] nic
    }
wanted-list-history-table-time-col = Czas
wanted-list-history-table-reason-col = Przestępstwo
wanted-list-history-table-initiator-col = Inicjator
