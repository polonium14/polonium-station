ore-silo-ui-title = Silos na materiały
ore-silo-ui-label-clients = Maszyny
ore-silo-ui-label-mats = Materiały
ore-silo-ui-itemlist-entry =
    { $linked ->
        [true] { "[Powiązany] " }
       *[False] { "" }
    } { $name } ({ $beacon }) { $inRange ->
        [true] { "" }
       *[false] (Poza zasięgiem)
    }
