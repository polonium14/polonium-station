lathe-menu-title = Menu Tokarki
# a może obrabiarka, brzmi bardziej uniwersalnie tbh
lathe-menu-queue = Kolejka
lathe-menu-server-list = Lista serwerów
lathe-menu-sync = Synchronizuj
lathe-menu-search-designs = Znajdź przepis
lathe-menu-category-all = Wszystkie
lathe-menu-search-filter = Filtr:
lathe-menu-amount = Ilość:
lathe-menu-recipe-count = { $count ->
    [1] {$count} Receptura
    *[other] {$count} Receptur
}
lathe-menu-reagent-slot-examine = Posiada wejście na zlewkę u boku.
lathe-reagent-dispense-no-container = Płyn wylewa się z {$name} na podłogę!
lathe-menu-result-reagent-display = {$reagent} ({$amount}u)
lathe-menu-material-display = {$material} ({$amount})
lathe-menu-tooltip-display = {$amount} sztuk {$material}
lathe-menu-description-display = [italic]{$description}[/italic]
lathe-menu-material-amount = { $amount ->
    [1] {NATURALFIXED($amount, 2)} {$unit}
    *[other] {NATURALFIXED($amount, 2)} {MAKEPLURAL($unit)}
}
lathe-menu-material-amount-missing = { $amount ->
    [1] {NATURALFIXED($amount, 2)} {$unit} sztuk {$material} ([color=red]Brakuje {NATURALFIXED($missingAmount, 2)} {$unit}[/color])
    *[other] {NATURALFIXED($amount, 2)} {MAKEPLURAL($unit)} sztuk {$material} ([color=red]Brakuje {NATURALFIXED($missingAmount, 2)} {MAKEPLURAL($unit)}[/color])
}
lathe-menu-no-materials-message = Brak materiałów.
lathe-menu-silo-linked-message = Silos połączony
lathe-menu-fabricating-message = Wytwarzanie...
lathe-menu-materials-title = Materiały
lathe-menu-queue-title = Kolejka budowania
