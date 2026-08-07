cmd-bql_select-desc = Show results of a BQL query in a client-side window
cmd-bql_select-help =
    Usage: bql_select <bql query>
    The opened window allows you to teleport to or view variables the resulting entities.
cmd-bql_select-err-server-shell = Cannot be executed from server shell
cmd-bql_select-err-rest = Warning: unused part after BQL query: "{ $rest }"
ui-bql-results-title = BQL results
ui-bql-results-vv = VV
ui-bql-results-status-more = { $count } { $count ->
    [one] encja (więcej dostępnych)
    [few] encje (więcej dostępnych)
   *[other] encji (więcej dostępnych)
}

ui-bql-results-status = { $count } { $count ->
    [one] encja
    [few] encje
   *[many] encji
}

ui-bql-results-status-total = { $loaded } / { $total }

ui-bql-results-col-id = ID
ui-bql-results-col-name = Nazwa
ui-bql-results-col-proto = Prototyp
ui-bql-results-col-actions = Akcje

ui-bql-results-actions = Akcje
ui-bql-results-follow = Śledź
ui-bql-results-copy = Kopiuj ID
ui-bql-results-delete = Usuń
ui-bql-results-delete-confirm = Potwierdź?
