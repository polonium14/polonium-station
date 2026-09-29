materials-replisteel = replistal
stack-replisteel = replistal
replicator-hand-replisteel-empty = Replistal
tiles-replicator-floor = podłoga replikatorów

alerts-replicator-recall-immune-name = Ślad nanitów
alerts-replicator-recall-immune-desc = Po Przywołaniu Roju wciąż oblepiają cię nanity. Replikatory nie mogą cię ponownie przywołać, dopóki ślad nie zniknie.

replicator-construction-fail-no-floor = Nie ma tu podłogi, na której dałoby się pracować.
replicator-construction-fail-already-ours = To już należy do roju.
replicator-construction-fail-wall = Nanity nie są w stanie przejąć tej ściany.
replicator-construction-fail-budget = Rojowi brakuje replistali, to kosztuje { $cost } { $cost ->
        [one] arkusz
        [few] arkusze
        [many] arkuszy
       *[other] arkusza
    }.
replicator-construction-fail-occupied = Coś stoi na przeszkodzie.
replicator-construction-fail-no-nest = Nie masz gniazda, z którego można czerpać replistal.

replicator-fabricator-verb = Złóż korpus ({ $cost } { $cost ->
        [one] arkusz
        [few] arkusze
        [many] arkuszy
       *[other] arkusza
    })
replicator-fabricator-busy = Fabrykator jest już zajęty.
replicator-fabricator-examine-working = Składa korpus, zostało [color=violet]{ $seconds } s[/color].
replicator-fabricator-examine-ready = Gotowy korpus czeka, aż zajmie go umysł.
replicator-fabricator-examine-idle = Stoi bezczynnie.
replicator-fabricator-limit = Rój nie utrzyma teraz więcej niż { $limit } replikatorów.
replicator-fabricator-started = Fabrykator zaczyna składać korpus.

replicator-rebuild-tier1 = Przebuduj do poziomu 1 (za darmo)
replicator-rebuild-tier2 = Przebuduj do poziomu 2 ({ $cost } { $cost ->
        [one] arkusz
        [few] arkusze
        [many] arkuszy
       *[other] arkusza
    })
replicator-rebuild-tier3 = Przebuduj do poziomu 3 ({ $cost } { $cost ->
        [one] arkusz
        [few] arkusze
        [many] arkuszy
       *[other] arkusza
    })
replicator-rebuild-fail-same = Już jesteś na tym poziomie.
replicator-rebuild-fail-level = Gniazdo musi najpierw osiągnąć poziom { $level }.
replicator-rebuild-started = Zanurzasz się w gnieździe, a nanity zaczynają cię rozbierać.
replicator-rebuild-finished = Wypełzasz z gniazda w nowym korpusie.
replicator-rebuild-interrupted = Gniazdo przepadło, a razem z nim replistal wydana na twoją przebudowę.

replicator-hive-unlocked = Gniazdo osiągnęło poziom { $level }. Teraz dostępne: { $classes }.
replicator-hive-examine = Rój ma [color=violet]{ $budget } { $budget ->
        [one] arkusz
        [few] arkusze
        [many] arkuszy
       *[other] arkusza
    }[/color] replistali. Gniazdo jest na poziomie { $level }.

replicator-teleport-prey-fail-immune = Ten cel wciąż oblepiają nanity, nie da się go jeszcze przywołać.
replicator-on-structure-attack-fail = Nie możesz niszczyć tego, co zbudował rój.
