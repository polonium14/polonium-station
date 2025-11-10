## Rev Head

roles-antag-rev-head-name = Główny Rewolucjonista
roles-antag-rev-head-objective = Twoim celem jest przejęcie stacji poprzez konwertowanie ludzi na swoją sprawę i zabicie całego personelu Dowództwa na stacji.

head-rev-role-greeting =
    Jesteś Głównym Rewolucjonistą.
    Twoim zadaniem jest usunięcie całego Dowództwa ze stacji poprzez konwersję, śmierć lub uwięzienie.
    Syndykat wyposażył cię w flash, który konwertuje załogę na twoją stronę.
    Uwaga, nie zadziała to na osoby z Mindshieldem lub noszące ochronę oczu.
    Viva la revolución!

head-rev-briefing =
    Używaj flashy, aby konwertować ludzi na swoją sprawę.
    Pozbądź się lub przekonwertuj wszystkich szefów, aby przejąć kontrolę nad stacją.

head-rev-break-mindshield = Mindshield został zniszczony!

## Rev

roles-antag-rev-name = Rewolucjonista
roles-antag-rev-objective = Twoim celem jest zapewnienie bezpieczeństwa i wykonywanie poleceń Głównych Rewolucjonistów oraz pozbycie się lub konwersja całego personelu Dowództwa na stacji.

rev-break-control = {$name} przypomniał sobie swoją prawdziwą lojalność!

rev-role-greeting =
    Jesteś Rewolucjonistą.
    Twoim zadaniem jest przejęcie stacji i ochrona Głównych Rewolucjonistów.
    Pozbądź się lub przekonwertuj cały personel Dowództwa.
    Viva la revolución!

    rev-briefing = Pomóż swoim Głównym Rewolucjonistom pozbyć się dowództwa, aby przejąć kontrolę nad stacją.

## General

rev-title = Rewolucjoniści
rev-description = Rewolucjoniści są wśród nas.

rev-not-enough-ready-players = Za mało graczy przygotowanych do gry. Gotowych było {$readyPlayersCount} graczy z wymaganych {$minimumPlayers}. Nie można rozpocząć Rewolucji.
rev-no-one-ready = Żaden gracz nie jest gotowy! Nie można rozpocząć Rewolucji.
rev-no-heads = Nie wybrano żadnego Głównego Rewolucjonisty. Nie można rozpocząć Rewolucji.

rev-won = Główni Rewolucjoniści przetrwali i skutecznie przejęli kontrolę nad stacją.

rev-lost = Dowództwo przetrwało i zabiło wszystkich Głównych Rewolucjonistów.

rev-stalemate = Wszyscy Główni Rewolucjoniści i Dowództwo zginęli. Remis.

rev-reverse-stalemate = Zarówno Dowództwo, jak i Główni Rewolucjoniści przetrwali.

rev-headrev-count = {$initialCount ->
    [one] Był jeden Główny Rewolucjonista:
    *[other] Było {$initialCount} Głównych Rewolucjonistów:
}

rev-headrev-name-user = [color=#5e9cff]{$name}[/color] ([color=gray]{$username}[/color]) skonwertował(a) {$count} {$count ->
    [one] osobę
    *[other] osoby
}

rev-headrev-name = [color=#5e9cff]{$name}[/color] skonwertował(a) {$count} {$count ->
    [one] osobę
    *[other] osoby
}

## Deconverted window

rev-deconverted-title = Dekonwertowany!
rev-deconverted-text =
    Gdy ostatni Główny Rewolucjonista zginął, rewolucja dobiegła końca.

    Nie jesteś już rewolucjonistą, więc zachowuj się przyzwoicie.
rev-deconverted-confirm = Potwierdź
