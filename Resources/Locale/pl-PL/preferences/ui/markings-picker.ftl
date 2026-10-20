markings-search = Szukaj
-markings-selection =
    { $selectable ->
        [0] Nie możesz już wybrać żadnych oznaczeń.
        [one] Możesz wybrać jeszcze jedno oznaczenie.
        [few] Możesz wybrać jeszcze { $selectable } oznaczenia.
       *[other] Możesz wybrać jeszcze { $selectable } oznaczeń.
    }
markings-limits = { $required ->
    [true] { $count ->
        [-1] Select at least one marking.
        [0] You cannot select any markings, but somehow, you have to? This is a bug.
        [one] Select one marking.
       *[other] Select at least one marking and up to {$count} markings. { -markings-selection(selectable: $selectable) }
    }
   *[false] { $count ->
        [-1] Select any number of markings.
        [0] You cannot select any markings.
        [one] Select up to one marking.
       *[other] Select up to {$count} markings. { -markings-selection(selectable: $selectable) }
    }
}
markings-reorder = Zmień kolejność oznaczeń
humanoid-marking-modifier-respect-limits = Przestrzegaj limitów
humanoid-marking-modifier-respect-group-sex = Przestrzegaj ograniczeń grup i płci

# Categories

markings-organ-Torso = Tułów
markings-organ-Head = Głowa
markings-organ-ArmLeft = Lewe ramię
markings-organ-ArmRight = Prawe ramię
markings-organ-HandRight = Prawa dłoń
markings-organ-HandLeft = Lewa dłoń
markings-organ-LegLeft = Lewa noga
markings-organ-LegRight = Prawa noga
markings-organ-FootLeft = Lewa stopa
markings-organ-FootRight = Prawa stopa
markings-organ-Eyes = Oczy
markings-layer-Special = Specjalne
markings-layer-Tail = Ogon
markings-layer-Tail-Moth = Skrzydła
markings-layer-Hair = Włosy
markings-layer-FacialHair = Zarost
markings-layer-UndergarmentTop = Podkoszulek
markings-layer-UndergarmentBottom = Bielizna (dół)
markings-layer-Chest = Klatka piersiowa
markings-layer-Head = Głowa
markings-layer-Snout = Pysk
markings-layer-SnoutCover = Pysk (nakładka)
markings-layer-HeadSide = Głowa (bok)
markings-layer-HeadTop = Głowa (góra)
markings-layer-Eyes = Oczy
markings-layer-RArm = Prawe ramię
markings-layer-LArm = Lewe ramię
markings-layer-RHand = Prawa dłoń
markings-layer-LHand = Lewa dłoń
markings-layer-RLeg = Prawa noga
markings-layer-LLeg = Lewa noga
markings-layer-RFoot = Prawa stopa
markings-layer-LFoot = Lewa stopa
markings-layer-Overlay = Nakładka
markings-layer-TailOverlay = Nakładka
marking-points-remaining = Pozostało Cech: [{ $current }/{ $max }]
marking-used = { $marking-name }
marking-used-forced = { $marking-name } (Wymuszone)
marking-slot-add = Dodaj
marking-slot-remove = Usuń
marking-slot = Miejsce { $number }
marking-forced-colors = Ta cecha ma wymuszone kolory i nie można jej dostosować.
marking-select-to-customize = Wybierz cechę z prawego panelu, aby dostosować jej kolory.
marking-customize-colors = Kolorowanie
