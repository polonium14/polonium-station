### UI

# Shown when a stack is examined in details range
comp-stack-examine-detail-count =
    { $count ->
        [one] W stosie znajduje się [color={ $markupCountColor }]{ $count }[/color] przedmiot.
        [few] W stosie znajdują się [color={ $markupCountColor }]{ $count }[/color] przedmioty.
       *[many] W stosie znajduje się [color={ $markupCountColor }]{ $count }[/color] przedmiotów.
    }
# Stack status control
comp-stack-status = Licznik: [color=white] { $count }[/color]

### Interaction Messages

# Shown when attempting to add to a stack that is full
comp-stack-already-full = Stos jest już pełny.
# Shown when a stack becomes full
comp-stack-becomes-full = Stos jest teraz pełny.
# Text related to splitting a stack
comp-stack-split = Podzieliłeś stos.
comp-stack-split-halve = Połowa
comp-stack-split-too-small = Stos jest zbyt mały, aby go rozdzielić.
# Goobstation - Custom stack splitting dialog
comp-stack-split-custom = Podziel ilość...
# Goobstation - Custom stack splitting dialog
comp-stack-split-size = Maks: { $size }
ui-custom-stack-split-title = Podziel ilość
ui-custom-stack-split-line-edit-placeholder = Ilość
ui-custom-stack-split-apply = Podziel
