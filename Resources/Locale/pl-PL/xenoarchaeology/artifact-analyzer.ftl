analysis-console-menu-title = Konsola analityczna szerokopasmowa Mark 3
analysis-console-server-list-button = Serwer
analysis-console-extract-button = Wyodrębnij punkty
analysis-console-info-no-scanner = Brak podłączonego analizatora! Podłącz go multitoolem.
analysis-console-info-no-artifact = Brak artefaktu! Połóż go na platformie, aby wyświetlić informacje o węzłach.
analysis-console-info-ready = Systemy sprawne. Gotowe do skanowania.
analysis-console-no-node = Wybierz węzeł, aby wyświetlić
analysis-console-info-class = [font="Monospace" size=11]Klasa:[/font]
analysis-console-info-locked-value = [font="Monospace" size=11][color={ $state ->
        [0] red]Zablokowany
        [1] lime]Odblokowany
       *[2] plum]Aktywny
    }[/color][/font]
analysis-console-info-durability = [font="Monospace" size=11]Wytrzymałość:[/font]
analysis-console-info-effect = [font="Monospace" size=11]Efekt:[/font]
analysis-console-info-effect-value = [font="Monospace" size=11][color=gray]{ $state ->
        [true] { $info }
       *[false] Odblokuj węzły, aby uzyskać informacje
    }[/color][/font]
analysis-console-info-trigger = [font="Monospace" size=11]Wyzwalacze:[/font]
analysis-console-info-scanner = Skanowanie...
analysis-console-info-scanner-paused = Wstrzymano.
analysis-console-progress-text =
    { $seconds ->
        [one] T-{ $seconds } sekunda
       *[other] T-{ $seconds } sekund
    }
analysis-console-extract-value = [font="Monospace" size=11][color=orange]Węzeł { $id } (+{ $value })[/color][/font]
analysis-console-extract-none = [font="Monospace" size=11][color=orange] Brak odblokowanych węzłów z punktami do wyodrębnienia [/color][/font]
analysis-console-extract-sum = [font="Monospace" size=11][color=orange]Łączne badania: { $value }[/color][/font]
analyzer-artifact-extract-popup = Energia migocze na powierzchni artefaktu!
