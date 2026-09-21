# interaction
comp-crew-medal-inspection-text = Przyznano { $recipient } za { $reason}.
comp-crew-medal-award-text = { $recipient } otrzymał { $medal}.

# round end screen

comp-crew-medal-round-end-result =
    { $count ->
        [one] Otrzymano jeden medal:
       *[other] Otrzymano { $count } medalów:
    }
comp-crew-medal-round-end-list =
    - [color=white]{ $recipient }[/color] otrzymał [color=white]{ $medal }[/color] za
    { " }{ $reason }

# UI

crew-medal-ui-header = Ustawienia medalu
crew-medal-ui-reason = Powód przyznania nagrody:
crew-medal-ui-character-limit = { $number }/{ $max }
crew-medal-ui-info = Nie można tego już zmienić, gdy ktoś otrzyma ten medal.
crew-medal-ui-save = Zapisz
