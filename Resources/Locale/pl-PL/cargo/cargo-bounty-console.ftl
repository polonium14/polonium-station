bounty-console-menu-title = Konsola żądań logistycznych
bounty-console-label-button-text = Wydrukuj żądanie
bounty-console-skip-button-text = Pomiń
bounty-console-time-label = Czas: [color=orange]{ $time }[/color]
bounty-console-reward-label = Nagroda: [color=limegreen]${ $reward }[/color]
bounty-console-manifest-label = Manifest: [color=orange]{ $item }[/color]
bounty-console-manifest-entry =
    { $amount ->
        [1] { $item }
       *[other] { $item } x{ $amount }
    }
bounty-console-manifest-reward = Nagroda: ${ $reward }
bounty-console-manifest-entry-reagent =
    { $amount ->
        [1] { $item }
       *[other] { $item } { $amount }u
    }
bounty-console-description-label = [color=gray]{ $description }[/color]
bounty-console-id-label = ID#{ $id }
bounty-console-flavor-left = Żądania zbierane z lokalnych nieuczciwych handlarzy.
bounty-console-flavor-right = w1.4
bounty-manifest-header = [font size=14][bold]Oficjalne rządanie logistyczne[/bold] (ID#{ $id })[/font]
bounty-manifest-list-start = Zawartość:
bounty-console-tab-available-label = Dastępne
bounty-console-tab-history-label = Historia
bounty-console-history-empty-label = Brak histori żądań
bounty-console-history-notice-completed-label = [color=limegreen]Wypełnione[/color]
bounty-console-history-notice-skipped-label = [color=red]Pominięte[/color] przez { $id }
bounty-console-category-description = { $category } Żądanie: { $id }
