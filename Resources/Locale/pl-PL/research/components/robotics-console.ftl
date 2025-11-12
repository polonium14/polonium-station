robotics-console-window-title = Konsola robotyki
robotics-console-no-cyborgs = Brak robotów!
robotics-console-select-cyborg = Wybierz powyżej robota.
robotics-console-model = [color=gray]Model:[/color] { $name }
# name is not formatted to prevent players trolling
robotics-console-designation = [color=gray]Przystosowanie:[/color]
robotics-console-battery = [color=gray]Stan baterii:[/color] [color={ $color }]{ $charge }[/color]%
robotics-console-modules = [color=gray]Zamontowane moduły:[/color] { $count }
robotics-console-brain = [color=gray]Zamontowany mózg:[/color] [color={ $brain ->
        [true] green]tak
       *[false] red]nie
    }[/color]
robotics-console-locked-message = Sterowanie zablokowane, zbliż ID.
robotics-console-disable = Wyłącz
robotics-console-destroy = Zniszcz
robotics-console-cyborg-destroying = { CAPITALIZE(THE($name)) } jest zdalnie detonowany!
robotics-console-hp = [color=gray]Stan obudowy:[/color] [color={ $color }]{ $hp }[/color]%
