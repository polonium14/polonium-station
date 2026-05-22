## Rev Head

roles-antag-rev-head-name = Główny Rewolucjonista
roles-antag-rev-head-objective = Twoim celem jest przejęcie stacji poprzez konwertowanie ludzi na swoją sprawę i zabicie całego personelu Dowództwa na stacji.
head-rev-role-greeting =
    You are a Head Revolutionary.
    You are tasked with removing all of Command from station via converting, death, exilement or imprisonment.
    The Syndicate has sponsored you with a flash that converts the crew to your side, which may be retrieved from your Uplink using code [color = lightgray]{ $code }[/color].
    Beware, this won't work on those wearing flash-protection, or on Mindshielded crew such as Security or Command.
    Vivu la revolucio!
head-rev-briefing =
    Use flashes to convert people to your cause.
    Eliminate all heads of staff, and secure the station.
    You have been graciously sponsored with an uplink from
    the YLF, in-coordination with the Syndicate.
    Your uplink code is: { $code }
head-rev-break-mindshield = Mindshield został zniszczony!

## Rev

roles-antag-rev-name = Rewolucjonista
roles-antag-rev-objective = Twoim celem jest zapewnienie bezpieczeństwa i wykonywanie poleceń Głównych Rewolucjonistów oraz pozbycie się lub konwersja całego personelu Dowództwa na stacji.
rev-break-control = { $name } przypomniał sobie swoją prawdziwą lojalność!
rev-lieutenant-greeting =
    You are a Revolutionary Lieutenant.
    You are able to see your comrades, but are unable to convert anyone.
    Lead your department and co-ordinate with your fellow revolutionaries and head revolutionaries.
    Viva la revolución!
rev-role-greeting =
    Jesteś Rewolucjonistą.
    Twoim zadaniem jest przejęcie stacji i ochrona Głównych Rewolucjonistów.
    Pozbądź się lub przekonwertuj cały personel Dowództwa.
    Viva la revolución!
    
    rev-briefing = Pomóż swoim Głównym Rewolucjonistom pozbyć się dowództwa, aby przejąć kontrolę nad stacją.
rev-briefing = Help your head revolutionaries convert or get rid of every head to take over the station.

## General

rev-title = Rewolucjoniści
rev-description = Rewolucjoniści są wśród nas.
rev-not-enough-ready-players = Za mało graczy przygotowanych do gry. Gotowych było { $readyPlayersCount } graczy z wymaganych { $minimumPlayers }. Nie można rozpocząć Rewolucji.
rev-no-one-ready = Żaden gracz nie jest gotowy! Nie można rozpocząć Rewolucji.
rev-no-heads = Nie wybrano żadnego Głównego Rewolucjonisty. Nie można rozpocząć Rewolucji.
rev-won = Główni Rewolucjoniści przetrwali i skutecznie przejęli kontrolę nad stacją.
rev-lost = Dowództwo przetrwało i zabiło wszystkich Głównych Rewolucjonistów.
rev-stalemate = Wszyscy Główni Rewolucjoniści i Dowództwo zginęli. Remis.
rev-reverse-stalemate = Zarówno Dowództwo, jak i Główni Rewolucjoniści przetrwali.
rev-total-victory = All of Command and Head Revs survived, with all of Command being converted.
rev-headrev-count =
    { $initialCount ->
        [one] Był jeden Główny Rewolucjonista:
       *[other] Było { $initialCount } Głównych Rewolucjonistów:
    }
rev-headrev-name-user = [color=#5e9cff]{ $name }[/color] ([color=gray]{ $username }[/color]) skonwertował(a) { $count } { $count ->
        [one] osobę
       *[other] osoby
    }
rev-headrev-name = [color=#5e9cff]{ $name }[/color] skonwertował(a) { $count } { $count ->
        [one] osobę
       *[other] osoby
    }

## Deconverted window

rev-deconverted-title = Dekonwertowany!
rev-deconverted-text =
    Gdy ostatni Główny Rewolucjonista zginął, rewolucja dobiegła końca.
    
    Nie jesteś już rewolucjonistą, więc zachowuj się przyzwoicie.
rev-deconverted-confirm = Potwierdź
rev-headrev-must-return = The Revolution is leaderless. We must return to the station within a minute!
rev-headrev-returned = A Head Revolutionary has returned to the station, the Revolution continues!
rev-headrev-abandoned = You have disgraced the revolution by abandoning your station. The Revolution is over.
