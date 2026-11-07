breathalyzer-busy = { CAPITALIZE($device) } wciąż analizuje próbkę.
breathalyzer-cannot-blow = Nie możesz dmuchnąć w { $device }.
breathalyzer-blow-self = Dmuchasz w ustnik.
breathalyzer-blow-others = { CAPITALIZE($user) } dmucha w { $device }.
breathalyzer-force-user = Przykładasz ustnik do ust i każesz dmuchać: { $target }.
breathalyzer-force-others = { CAPITALIZE($user) } każe dmuchać w { $device }: { $target }.

breathalyzer-examine-idle = Wyświetlacz jest gotowy do pomiaru.
breathalyzer-examine-busy = Na wyświetlaczu trwa pomiar.
breathalyzer-examine-result = Wyświetlacz pokazuje [color={ $color }]{ $result }‰ ({ $level })[/color].

breathalyzer-verb-open-display = Spójrz na wyświetlacz

breathalyzer-level-sober = Trzeźwy
breathalyzer-level-after-drinking = Po użyciu alkoholu
breathalyzer-level-intoxicated = Nietrzeźwy
breathalyzer-level-poisoning = Zatrucie alkoholowe

breathalyzer-ui-header = ZGONOMETR 4000
breathalyzer-ui-state-idle = CZUWANIE
breathalyzer-ui-state-sampling = PRÓBKA
breathalyzer-ui-state-analyzing = ANALIZA
breathalyzer-ui-state-result = WYNIK
breathalyzer-ui-status-ready = GOTOWY
breathalyzer-ui-status-blow = DMUCHAJ
breathalyzer-ui-status-analyze = ANALIZUJĘ
breathalyzer-ui-footer-idle = Użyj alkomatu w ręce, żeby dmuchnąć.
breathalyzer-ui-footer-blow = Dmuchaj równo aż do sygnału.
breathalyzer-ui-footer-analyze = Chwila, próbka jest analizowana.
breathalyzer-ui-footer-dirty = Umyj ustnik mydłem.
breathalyzer-ui-status-dirty = BRUDNY USTNIK

breathalyzer-dirty = Ustnik jest zarzygany. Najpierw umyj go mydłem.
breathalyzer-vomit = Ustnik jest cały w wymiocinach!
breathalyzer-cleaned = Ustnik jest znowu czysty.
breathalyzer-examine-dirty = [color=yellowgreen]Ustnik jest ubrudzony wymiocinami.[/color]

breathalyzer-report = [head=2]Wydruk z alkomatu[/head]
    Badany: { $subject }
    Wynik: { $result }‰
    Ocena: { $level }
    Czas pomiaru: { $time }
breathalyzer-report-unknown = nieznany
