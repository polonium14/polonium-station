# antag selection
roles-antag-changeling-name = Zmiennokształtny
roles-antag-changeling-objective = Inteligentny drapieżnik, który przejmuje tożsamości swoich ofiar.
# devour
changeling-devour-attempt-failed-cannot-devour = Nie możemy tego pożreć!
changeling-devour-attempt-failed-already-devoured = Już strawiliśmy to ciało!
changeling-devour-attempt-failed-devoured-recently = To ciało jest zbyt okaleczone, by je pożreć!
changeling-devour-attempt-failed-not-dead = To ciało wciąż żyje! Nie możemy pożreć żywego!
changeling-devour-attempt-failed-rotting = Ten trup składa się wyłącznie ze zgnitej biomasy.
changeling-devour-attempt-failed-protected = Biomasa tej ofiary jest chroniona przez pancerz!
changeling-devour-attempt-failed-no-space = Przekroczyliśmy maksymalną liczbę przebrań!
changeling-devour-begin-windup-self = Nasze nienaturalne usta ukazują się z kosmicznym głodem.
changeling-devour-begin-windup-others = { CAPITALIZE(POSS-ADJ($user)) } nienaturalne usta ukazują się z kosmicznym głodem.
changeling-devour-begin-consume-self = Nienaturalne usta wgryzają się głęboko w ofiarę.
changeling-devour-begin-consume-others = { CAPITALIZE(POSS-ADJ($user)) } nienaturalne usta wgryzają się głęboko w { POSS-ADJ($user) } ofiarę.
changeling-devour-consume-complete-self = Nasze nienaturalne usta cofają się, biomasa została strawiona.
changeling-devour-consume-complete-others = { CAPITALIZE(POSS-ADJ($user)) } nienaturalne usta cofają się.
changeling-devour-doafter-windup = [color=red]{ CAPITALIZE(THE($user)) } rozwiera twarz, szykując się do uczty![/color]
changeling-devour-doafter = [color=red]{ CAPITALIZE(THE($user)) } rozszarpuje ciało ofiary: { $target }![/color]
# transformation
changeling-transform-attempt-self = Nasze kości pękają, mięśnie rwą się, jedno ciało staje się innym.
changeling-transform-attempt-others = { CAPITALIZE(POSS-ADJ($user)) } kości pękają, mięśnie rwą się, ciało przekształca się w inne.
changeling-transform-doafter = [color=red]Powierzchnia { POSS-ADJ($user) } skóry topi się, przyjmując inną postać.[/color]
# transformation BUI
changeling-transform-bui-select-entity = { $entity }
changeling-transform-bui-drop-identity-menu = Usuń pożartą tożsamość z pamięci.
changeling-transform-bui-drop-identity-entity = Usuń { $entity }
changeling-transform-bui-drop-identity-entity-popup = Usunęliśmy { $entity } z naszej pamięci.
changeling-transform-bui-drop-identity-cannot-drop = Nie możemy usunąć naszej obecnej tożsamości.
# voice mimicry
changeling-voice-mimic-window-title = Naśladowanie głosu
# flesh clothing
changeling-flesh-clothing-removed-popop = { CAPITALIZE(THE($item)) } rozpada się w cielesne szczątki!
changeling-flesh-clothing-examine-wearer = [color=crimson]Ten przedmiot to zamaskowana część twojego ciała. Zniknie, jeśli go zdejmiesz![/color]
changeling-flesh-clothing-alert-name = Zdolność odzieży z ciała
changeling-flesh-clothing-alert-desc = Czy transformacja odzieży jest włączona. Kliknij, aby przełączyć.
# biodegrade
changeling-biodegrade-used-popup = { CAPITALIZE(THE($user)) } wymiotuje kwasem na { POSS-ADJ($user) } { $restraint }!
changeling-biodegrade-used-popup-self = Wymiotujemy kwasem na nasze { $restraint }!
# last resort
changeling-takeover-not-dead = To ciało wciąż żyje! Nie możemy go infekować, gdy żyje.
changeling-takeover-is-changeling = To ciało odrzuca naszą próbę przejęcia!
changeling-takeover-start-others = { CAPITALIZE(THE($user)) } zaczyna wpełzać do trupa!
changeling-takeover-doafter = [color=red]{ CAPITALIZE(SUBJECT($user)) } { CONJUGATE-BE($user) } crawling inside of { $target }![/color]
# stings
changeling-sting-success = Cicho ukąsiliśmy { THE($target) }!
changeling-sting-hemotoxin = Czujesz ostry ból w klatce piersiowej!
# mindshield
changeling-fake-mindshield-enabled = Emiutjemy fałszywą osłonę umysłu.
changeling-fake-mindshield-disabled = Przestajemy emitować fałszywe fale osłony umysłu.
# chemicals
changeling-not-enough-chemicals = Nie mamy wystarczającej ilości chemikaliów.
# other
changeling-action-fail-generic = Nie możemy tego teraz zrobić.
changeling-paused-map-name = Mapa przechowywania tożsamości changelinga
changeling-cocoon-success = Zamykamy { THE($target) } w mięsistym grobie.
changeling-cocoon-doafter = [color=red]{ CAPITALIZE(SUBJECT($user)) } { CONJUGATE-BE($user) } covering { THE($target) } in a fleshy mass![/color]
# horror transformation
changeling-horror-alert-time-name = Pozostały czas
changeling-horror-alert-time-desc = Kiedy ten licznik osiągnie wartość 0, wyczerpią się nasze siły.
changeling-horror-force-transform-self = Nasze siły się wyczerpały, nie jesteśmy już w stanie utrzymać tej postaci.
changeling-horror-force-transform-others = { CAPITALIZE(POSS-ADJ($user)) } ciało szybko usycha i obumiera.
changeling-horror-transform-fail = Nie dysponujemy wystarczającą ilością zgromadzonego materiału DNA, by ujawnić swoją tożsamość.
