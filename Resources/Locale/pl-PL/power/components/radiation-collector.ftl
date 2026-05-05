# SPDX-FileCopyrightText: 2023 chromiumboy <50505512+chromiumboy@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Aiden <aiden@djkraz.com>
# SPDX-FileCopyrightText: 2024 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT

power-radiation-collector-gas-tank-missing = Komora [color=darkred]nie zawiera[/color] zbiornika plazmy.
power-radiation-collector-gas-tank-present =
    Komora [color=darkgreen]zawiera[/color] [color={ $fullness ->
       *[0] red]pusty
        [1] red]prawie pusty
        [2] yellow]pół pełny
        [3] lime]pełny
    }[/color] zbiornik plazmy.
power-radiation-collector-enabled =
    Jest [color={ $state ->
        [true] darkgreen]włączony
       *[false] darkred]wyłączony
    }[/color].
