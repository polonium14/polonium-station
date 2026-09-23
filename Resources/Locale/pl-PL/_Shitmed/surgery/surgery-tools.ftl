surgery-tool-turn-on = Najpierw włącz to!
surgery-tool-reload = Najpierw przeładuj!
surgery-tool-match-light = Najpierw zapal!
surgery-tool-match-replace = Weź nową zapałkę!
surgery-tool-examinable-verb-text = Narzędzie chirurgiczne
surgery-tool-examinable-verb-message = Zbadaj zastosowania tego narzędzia w operacjach.
surgery-tool-header = To może być używane w operacjach jako:
surgery-tool-unlimited = - { LOC($tool, przypadek: "mianownik") } z [color={ $color }]{ $speed }x[/color] prędkością
surgery-tool-used = - { LOC($tool, przypadek: "mianownik") } z [color={ $color }]{ $speed }x[/color] prędkością, [color=red]następnie zużywa się[/color]

# $tool is a localization key, not an already translated name. The containing
# message chooses $przypadek through LOC(); keep Polish grammar out of the C# callers.
# mianownik: "jako: piła do kości"; dopelniacz: "potrzebujesz piły do kości".
# biernik: "pojemnik na żel kostny"; narzednik: "natnij skalpelem".
surgery-tool-name-bone-gel = { $przypadek ->
    [dopelniacz] żelu kostnego
    [biernik] żel kostny
   *[mianownik] żel kostny
    }
surgery-tool-name-bone-saw = { $przypadek ->
    [dopelniacz] piły do kości
   *[mianownik] piła do kości
    }
surgery-tool-name-bone-setter = { $przypadek ->
    [dopelniacz] nastawiacza kości
   *[mianownik] nastawiacz kości
    }
surgery-tool-name-cautery = { $przypadek ->
    [dopelniacz] kautera
   *[mianownik] kauter
    }
surgery-tool-name-drill = { $przypadek ->
    [dopelniacz] wiertła
   *[mianownik] wiertło
    }
surgery-tool-name-hemostat = { $przypadek ->
    [dopelniacz] hemostatu
   *[mianownik] hemostat
    }
surgery-tool-name-retractor = { $przypadek ->
    [dopelniacz] rozwórki
   *[mianownik] rozwórka
    }
surgery-tool-name-scalpel = { $przypadek ->
    [dopelniacz] skalpela
    [narzednik] skalpelem
   *[mianownik] skalpel
    }
surgery-tool-name-stitches = { $przypadek ->
    [dopelniacz] nici chirurgicznych
   *[mianownik] nici chirurgiczne
    }
surgery-tool-name-tending = { $przypadek ->
    [dopelniacz] opatrunku
   *[mianownik] opatrunek
    }
surgery-tool-name-tweezers = { $przypadek ->
    [dopelniacz] pęsety
   *[mianownik] pęseta
    }
surgery-tool-name-organ = { $przypadek ->
    [dopelniacz] organu
   *[mianownik] organ
    }
