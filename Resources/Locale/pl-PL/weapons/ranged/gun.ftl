gun-selected-mode-examine = [color={ $color }]{ $mode }[/color] obecnym trybem strzelania.
gun-fire-rate-examine = Szybkostrzelność wynosi [color={ $color }]{ $fireRate }[/color] na sekundę.
gun-selector-verb = Zmień na tryb { $mode }
gun-selected-mode = Wybrano { $mode }
gun-disabled = Nie możesz używać tej broni!
gun-clumsy = Broń wybucha ci w twarz!
gun-set-fire-mode = Ustawiono tryb { $mode }
gun-magazine-whitelist-fail = To się nie zmieści w broni!
gun-magazine-fired-empty = Nie ma amunicji!
# SelectiveFire
gun-SemiAuto = półautomatyczny
gun-Burst = seria
gun-FullAuto = automatyczny
# BallisticAmmoProvider
gun-ballistic-cycle = Przeładuj
gun-ballistic-cycled = Przeładowano
gun-ballistic-cycled-empty = Przeładowano (pusty)
gun-ballistic-transfer-invalid = { CAPITALIZE($ammoEntity) } nie mieści się w { $targetEntity }!
gun-ballistic-transfer-empty =
    { CAPITALIZE($entity) } jest { GENDER($entity) ->
       *[male] pusty
        [female] pusta
        [other] puste
    }.
gun-ballistic-transfer-target-full =
    { CAPITALIZE($entity) } już jest { GENDER($entity) ->
       *[male] załadowany
        [female] załadowana
        [other] załadowane
    }.
# CartridgeAmmo
gun-cartridge-spent = [color=red]Został[/color] wystrzelony.
gun-cartridge-unspent = [color=lime]Nie został[/color] wystrzelony.
# BatteryAmmoProvider
gun-battery-examine =
    Ma wystarczające napięcie do [color={ $color }]{ $count }[/color] { $count ->
        [one] strzału
       *[other] strzałów
    }.
# CartridgeAmmoProvider
gun-chamber-bolt-ammo = Komora nie zamknięta
gun-chamber-bolt = Komora jest [color={ $color }]{ $bolt }[/color].
gun-chamber-bolt-closed = zamknięta
gun-chamber-bolt-opened = otwarta
gun-chamber-bolt-close = Zamknij komorę
gun-chamber-bolt-open = Otwórz komorę
gun-chamber-bolt-closed-state = Otwórz
gun-chamber-bolt-open-state = Zamknij
gun-chamber-rack = Pompuj
# MagazineAmmoProvider
gun-magazine-examine = Ma [color={ $color }]{ $count }[/color] pozostałych nabojów.
# RevolverAmmoProvider
gun-revolver-empty = Opróżnij rewolwer
gun-revolver-full = Rewolwer jest pełny
gun-revolver-insert = Włożono
gun-revolver-spin = Zakręć bębnem
gun-revolver-spun = Przekręć bembenek
gun-speedloader-empty = Ładownik pusty
# GunSpreadModifier
examine-gun-spread-modifier-reduction = Rozrzut zmniejszony o [color=yellow]{ NATURALPERCENT($percentage) }[/color].
examine-gun-spread-modifier-increase = Rozrzut zwiększony o [color=yellow]{ NATURALPERCENT($percentage) }[/color].
