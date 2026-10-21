cmd-align-desc =
    Automatycznie wyrównuje ustawienie wszystkich zakotwiczonych śluz, drzwi, blokad przeciwpożarowych itp.
    do sąsiednich konstrukcji.
    
    Użyj parametru [dry run], aby wykonać sprawdzenie bez obracania czegokolwiek.
cmd-align-help = Użycie: { $command } [MapID] [dry run?]
cmd-align-no-release = Nie możesz użyć tej komendy, jeśli gra działa w konfiguracji RELEASE.
cmd-align-hint-id = MapID
cmd-align-hint-dry = dry run?
cmd-align-feedback-none =
    { $dry ->
        [true] PRÓBA: nie znaleziono
       *[false] Nie znaleziono
    } żadnych encji zgodnych z AlignerSystem!
cmd-align-feedback-good =
    { $dry ->
        [true] PRÓBA: nie znaleziono
       *[false] Nie znaleziono
    } żadnych źle wyrównanych encji.
cmd-align-feedback =
    { $dry ->
        [true] PRÓBA: znaleziono
       *[false] Znaleziono i naprawiono
    } { $fixed ->
        [one] jedną źle wyrównaną encję.
       *[else] źle wyrównane encje: { $fixed }.
    }
