cmd-bql_select-desc = Pokazuje wyniki zapytania BQL w oknie po stronie klienta
cmd-bql_select-help =
    Użycie: bql_select <zapytanie bql>
    Otwarte okno pozwala teleportować się do wynikowych encji lub przeglądać ich zmienne.
cmd-bql_select-err-server-shell = Nie można wykonać z powłoki serwera
cmd-bql_select-err-rest = Ostrzeżenie: nieużywana część po zapytaniu BQL: "{ $rest }"
ui-bql-results-title = Wyniki BQL
ui-bql-results-vv = VV
ui-bql-results-status =
    { $count } { $count ->
        [one] encja
        [few] encje
       *[many] encji
    }
ui-bql-results-status-more =
    { $count } { $count ->
        [one] encja (więcej dostępnych)
        [few] encje (więcej dostępnych)
       *[other] encji (więcej dostępnych)
    }
ui-bql-results-status-total =
    { $loaded } / { $total } { $total ->
        [one] encja
        [few] encje
       *[many] encji
    }
ui-bql-results-col-id = ID
ui-bql-results-col-name = Nazwa
ui-bql-results-col-proto = Prototyp
ui-bql-results-col-actions = Akcje
ui-bql-results-actions = Akcje
ui-bql-results-follow = Śledź
ui-bql-results-copy = Kopiuj ID
ui-bql-results-delete = Usuń
ui-bql-results-delete-confirm = Potwierdź?
ui-bql-results-pin = Przypnij
ui-bql-results-unpin = Odepnij
