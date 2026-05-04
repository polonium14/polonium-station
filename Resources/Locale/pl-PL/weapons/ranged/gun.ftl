# SPDX-FileCopyrightText: 2022 Kara <lunarautomaton6@gmail.com>
# SPDX-FileCopyrightText: 2022 PixelTK <85175107+PixelTheKermit@users.noreply.github.com>
# SPDX-FileCopyrightText: 2022 Rane <60792108+Elijahrane@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 Errant <35878406+errant@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 MendaxxDev <153332064+MendaxxDev@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 TaralGit <76408146+TaralGit@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 Vordenburg <114301317+Vordenburg@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 and_a <and_a@DESKTOP-RJENGIR>
# SPDX-FileCopyrightText: 2023 chromiumboy <50505512+chromiumboy@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Errant <35878406+Errant-4@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT

gun-selected-mode-examine = [color={ $color }]{ $mode }[/color] obecnym trybem strzelania.
gun-fire-rate-examine = Szybkostrzelność wynosi [color={ $color }]{ $fireRate }[/color] na sekundę.
gun-selector-verb = Zmień na tryb { $mode }
gun-selected-mode = Wybrano { $mode }
gun-disabled = Nie możesz używać tej broni!
gun-clumsy = Broń wybucha ci w twarz!
gun-set-fire-mode = Ustawiono tryb { $mode }
gun-magazine-whitelist-fail = To się nie zmieści w broni!
# SelectiveFire
gun-SemiAuto = półautomatyczny
gun-Burst = seria
gun-FullAuto = automatyczny
# BallisticAmmoProvider
gun-ballistic-cycle = Przeładuj
gun-ballistic-cycled = Przeładowano
gun-ballistic-cycled-empty = Przeładowano (pusty)
gun-ballistic-transfer-invalid = { CAPITALIZE(THE($ammoEntity)) } won't fit inside { THE($targetEntity) }!
gun-ballistic-transfer-empty = { CAPITALIZE(THE($entity)) } is empty.
gun-ballistic-transfer-target-full = { CAPITALIZE(THE($entity)) } is already fully loaded.
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
