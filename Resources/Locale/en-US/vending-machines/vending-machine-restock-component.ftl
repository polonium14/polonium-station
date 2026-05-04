# SPDX-FileCopyrightText: 2023 Vordenburg <114301317+Vordenburg@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT

vending-machine-restock-invalid-inventory = { CAPITALIZE(THE($this)) } isn't the right package to restock { THE($target) }.
vending-machine-restock-start-self = You start restocking { $target }.
vending-machine-restock-needs-panel-open = { CAPITALIZE($target) } needs { POSS-ADJ($target) } maintenance panel opened first.
vending-machine-restock-start = { $user } starts restocking { $target }.
vending-machine-restock-start-others = { CAPITALIZE($user) } starts restocking { $target }.
vending-machine-restock-done-self = You finish restocking { $target }.
vending-machine-restock-done-others = { CAPITALIZE($user) } finishes restocking { $target }.
vending-machine-restock-done = { $user } finishes restocking { $target }.
