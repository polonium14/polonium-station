# Examine text after when they're holding something (in-hand)
comp-hands-examine = { CAPITALIZE(SUBJECT($user)) } { CONJUGATE-BE($user) } trzyma { $items }.
comp-hands-examine-empty = { CAPITALIZE(SUBJECT($user)) } nic nie trzyma.
comp-hands-examine-wrapper = { INDEFINITE($item) } [color=paleturquoise]{$item}[/color]

hands-system-blocked-by = Zablokowane pzez
