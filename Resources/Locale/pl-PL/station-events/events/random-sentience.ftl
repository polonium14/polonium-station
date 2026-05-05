# SPDX-FileCopyrightText: 2022 Moony <moonheart08@users.noreply.github.com>
# SPDX-FileCopyrightText: 2022 Morb <14136326+Morb0@users.noreply.github.com>
# SPDX-FileCopyrightText: 2022 moonheart08 <moonheart08@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 Nim <128169402+Nimfar11@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Psychpsyo <60073468+Psychpsyo@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Tadeo <td12233a@gmail.com>
# SPDX-FileCopyrightText: 2024 deathride58 <deathride58@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT


## Phrases used for where central command got this information.

random-sentience-event-data-1 = skanów z naszych czujników dalekiego zasięgu
random-sentience-event-data-2 = naszych zaawansowanych modeli probabilistycznych
random-sentience-event-data-3 = naszej wszechmocy
random-sentience-event-data-4 = ruchu komunikacyjnego na waszej stacji
random-sentience-event-data-5 = wykrytych emisji energii
random-sentience-event-data-6 = [ZREDAGOWANO]

## Phrases used to describe the level of intelligence, though it doesn't actually affect anything.

random-sentience-event-strength-1 = ludzkim
random-sentience-event-strength-2 = naczelnym
random-sentience-event-strength-3 = umiarkowanym
random-sentience-event-strength-4 = ochrony
random-sentience-event-strength-5 = dowódczym
random-sentience-event-strength-6 = klaunim
random-sentience-event-strength-7 = niskim
random-sentience-event-strength-8 = SI

## Announcement text

station-event-random-sentience-announcement =
    Na podstawie { $data }, wierzymy, że niektóre z { $amount ->
        [1] { $kind1 }
        [2] { $kind1 } i { $kind2 }
        [3] { $kind1 }, { $kind2 } i { $kind3 }
       *[other] { $kind1 }, { $kind2 }, { $kind3 }, itp.
    } istot na stacji rozwinęły inteligencję na poziomie { $strength } oraz zdolność komunikacji.

## Ghost role description

station-event-random-sentience-role-description = Jesteś świadomym { $name }, ożywionym przez kosmiczną magię.
# Flavors
station-event-random-sentience-flavor-mechanical = mechaniczny
station-event-random-sentience-flavor-organic = organiczny
station-event-random-sentience-flavor-corgi = corgi
station-event-random-sentience-flavor-primate = naczelny
station-event-random-sentience-flavor-kobold = kobold
station-event-random-sentience-flavor-slime = szlam
station-event-random-sentience-flavor-inanimate = nieożywiony
