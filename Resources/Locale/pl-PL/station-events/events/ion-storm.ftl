station-event-ion-storm-start-announcement = W pobliżu stacji wykryto burzę jonową. Proszę sprawdzić wszystkie urządzenia SI.
ion-storm-law-scrambled-number = [font="Monospace"][scramble rate=250 length={ $length } chars="@@###$$&%!01"/][/font]
ion-storm-you = TY
ion-storm-the-station = STACJA
ion-storm-the-crew = ZAŁOGA
ion-storm-the-job = { $job }
ion-storm-clowns = KLAUNY
ion-storm-heads = KIEROWNICY
ion-storm-crew = ZAŁOGA
ion-storm-people = LUDZIE
ion-storm-adjective-things = RZECZY { $adjective }
ion-storm-x-and-y = { $x } I { $y }
# joined is short for {$number} {$adjective}
# subjects can generally be threats or jobs or objects
# thing is specified above it
ion-storm-law-on-station = { $joined } { $subjects } SĄ NA STACJI
ion-storm-law-call-shuttle = WAHADŁOWIEC MUSI ZOSTAĆ WEZWANY Z POWODU: { $joined } { $subjects } NA STACJI
ion-storm-law-crew-are = { $who } TERAZ SĄ { $joined } { $subjects }
ion-storm-law-subjects-harmful = { $adjective } { $subjects } SĄ SZKODLIWE DLA ZAŁOGI
ion-storm-law-must-harmful = CI KTÓRZY { $must } SĄ SZKODLIWI DLA ZAŁOGI
# thing is a concept or action
ion-storm-law-thing-harmful = { $thing } JEST SZKODLIWE DLA ZAŁOGI
ion-storm-law-job-harmful = { $adjective } { $job } SĄ SZKODLIWE DLA ZAŁOGI
# thing is objects or concept, adjective applies in both cases
# this means you can get a law like "NOT HAVING CHRISTMAS-STEALING COMMUNISM IS HARMFUL TO THE CREW" :)
ion-storm-law-having-harmful = POSIADANIE { $adjective } { $thing } JEST SZKODLIWE DLA ZAŁOGI
ion-storm-law-not-having-harmful = NIEPOSIADANIE { $adjective } { $thing } JEST SZKODLIWE DLA ZAŁOGI
# thing is a concept or require
ion-storm-law-requires =
    { $who } { $plural ->
        [true] WYMAGAJĄ
       *[false] WYMAGA
    } { $thing }
ion-storm-law-requires-subjects =
    { $who } { $plural ->
        [true] WYMAGAJĄ
       *[false] WYMAGA
    } { $joined } { $subjects }
ion-storm-law-allergic =
    { $who } { $plural ->
        [true] MAJĄ
       *[false] MA
    } { $severity } ALERGIĘ NA { $allergy }
ion-storm-law-allergic-subjects =
    { $who } { $plural ->
        [true] MAJĄ
       *[false] MA
    } { $severity } ALERGIĘ NA { $adjective } { $subjects }
ion-storm-law-feeling = { $who } { $feeling } { $concept }
ion-storm-law-feeling-subjects = { $who } { $feeling } { $joined } { $subjects }
ion-storm-law-you-are = TERAZ { $concept }
ion-storm-law-you-are-subjects = TERAZ JESTEŚ { $joined } { $subjects }
ion-storm-law-you-must-always = MUSISZ ZAWSZE { $must }
ion-storm-law-you-must-never = NIGDY NIE MOŻESZ { $must }
ion-storm-law-eat = { $who } MUSI JEŚĆ { $adjective } { $food } ABY PRZEŻYĆ
ion-storm-law-drink = { $who } MUSI PIĆ { $adjective } { $drink } ABY PRZEŻYĆ
ion-storm-law-change-job = { $who } SĄ TERAZ { $adjective } { $change }
ion-storm-law-highest-rank = { $who } SĄ TERAZ NAJWYŻSZYMI CZŁONKAMI ZAŁOGI
ion-storm-law-lowest-rank = { $who } SĄ TERAZ NAJNIŻSZYMI CZŁONKAMI ZAŁOGI
ion-storm-law-crew-must = { $who } MUSZĄ { $must }
ion-storm-law-crew-must-go = { $who } MUSZĄ IŚĆ DO { $area }
ion-storm-part =
    { $part ->
        [true] SĄ CZĘŚCIĄ
       *[false] NIE SĄ CZĘŚCIĄ
    }
# due to phrasing, this would mean a law such as
# ONLY HUMANS ARE NOT PART OF THE CREW
# would make non-human nukies/syndies/whatever crew :)
ion-storm-law-crew-only-1 = TYLKO { $who } SĄ { $part } ZAŁOGI
ion-storm-law-crew-only-2 = TYLKO { $who } I { $other } SĄ { $part } ZAŁOGI
ion-storm-law-crew-only-subjects = TYLKO { $adjective } { $subjects } SĄ { $part } ZAŁOGI
ion-storm-law-crew-must-do = TYLKO CI KTÓRZY { $must } SĄ { $part } ZAŁOGI
ion-storm-law-crew-must-have = TYLKO CI KTÓRZY MAJĄ { $adjective } { $objects } SĄ { $part } ZAŁOGI
ion-storm-law-crew-must-eat = TYLKO CI KTÓRZY JEDZĄ { $adjective } { $food } SĄ { $part } ZAŁOGI
ion-storm-law-harm = TY MUSISZ KRZYWDZIĆ { $who } I NIE POZWÓL, PRZEZ BEZCZYNNOŚĆ, BY UNIKNĘLI KRZYWDY
ion-storm-law-protect = NIGDY NIE WOLNO CI SKRZYWDZIĆ { $who } I NIE POZWÓL, PRZEZ BEZCZYNNOŚĆ, BY STAŁA IM SIĘ KRZYWDA
# implementing other variants is annoying so just have this one
# COMMUNISM IS KILLING CLOWNS
ion-storm-law-concept-verb = { $concept } TO { $verb } { $subjects }
