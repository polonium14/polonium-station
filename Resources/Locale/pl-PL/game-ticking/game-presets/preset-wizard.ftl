## Survivor

roles-antag-survivor-name = Ocalały
# It's a Halo reference
roles-antag-survivor-objective = Aktualny cel: Przetrwać

survivor-role-greeting =
    Jesteś Ocalonym.
    Przede wszystkim musisz wrócić do Dowództwa Centralnego żywy.
    Zbierz tyle uzbrojenia, ile potrzeba, aby zapewnić sobie przetrwanie.
    Nie ufaj nikomu.

survivor-round-end-dead-count =
{
    $deadCount ->
        [one] [color=red]{$deadCount}[/color] ocalały zginął.
        *[other] [color=red]{$deadCount}[/color] ocalałych zginęło.
}

survivor-round-end-alive-count =
{
    $aliveCount ->
        [one] [color=yellow]{$aliveCount}[/color] ocalały został pozostawiony na stacji.
        *[other] [color=yellow]{$aliveCount}[/color] ocalałych zostało pozostawionych na stacji.
}

survivor-round-end-alive-on-shuttle-count =
{
    $aliveCount ->
        [one] [color=green]{$aliveCount}[/color] ocalały wydostał się żywy.
        *[other] [color=green]{$aliveCount}[/color] ocalałych wydostało się żywych.
}

## Wizard

objective-issuer-swf = [color=turquoise]Federacja Kosmicznych Czarodziejów[/color]

wizard-title = Czarodziej
wizard-description = Na stacji pojawił się Czarodziej! Nigdy nie wiadomo, co może zrobić.

roles-antag-wizard-name = Czarodziej
roles-antag-wizard-objective = Naucz ich lekcji, której nigdy nie zapomną.

wizard-role-greeting =
    JESTEŚ CZARODZIEJEM!
    Pomiędzy Federacją Kosmicznych Czarodziejów a NanoTrasen narosły napięcia.
    Zostałeś wybrany przez Federację Kosmicznych Czarodziejów, aby odwiedzić stację.
    Pokaż im, na co cię stać.
    Co zrobisz, zależy od ciebie, pamiętaj tylko, że Federacja Kosmicznych Czarodziejów chce, abyś wyszedł żywy.

wizard-round-end-name = czarodziej

## TODO: Wizard Apprentice (Coming sometime post-wizard release)
