cmd-bql_select-desc = Show results of a BQL query in a client-side window
cmd-bql_select-help =
    Usage: bql_select <bql query>
    The opened window allows you to teleport to or view variables the resulting entities.
cmd-bql_select-err-server-shell = Cannot be executed from server shell
cmd-bql_select-err-rest = Warning: unused part after BQL query: "{ $rest }"
ui-bql-results-title = BQL results
ui-bql-results-vv = VV
ui-bql-results-tp = TP
ui-bql-results-vv-tooltip = Wyświetl zmienne encji
ui-bql-results-tp-tooltip = Teleportuj do encji
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
