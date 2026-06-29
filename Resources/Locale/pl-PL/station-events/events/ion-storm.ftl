station-event-ion-storm-start-announcement = W pobliżu stacji wykryto burzę jonową. Proszę sprawdzić wszystkie urządzenia SI.
ion-storm-law-scrambled-number = [font="Monospace"][scramble rate=250 length={$length} chars="@@###$$&%!01"/][/font]
ion-storm-you = TY
ion-storm-the-station = STACJA
ion-storm-the-crew = ZAŁOGA
ion-storm-the-job = {$job}
ion-storm-clowns = KLAUNY
ion-storm-heads = KIEROWNICY
ion-storm-crew = ZAŁOGA
ion-storm-people = LUDZIE
ion-storm-adjective-things = RZECZY {$adjective}
ion-storm-x-and-y = {$x} I {$y}
# joined is short for {$number} {$adjective}
# subjects can generally be threats or jobs or objects
# thing is specified above it
ion-storm-law-on-station = {ION-NUMBER-BASE($ion)} {ION-NUMBER-MOD($ion)} {ION-ADJECTIVE($ion)} {ION-SUBJECT($ion)} SĄ NA STACJI
ion-storm-law-call-shuttle = WAHADŁOWIEC MUSI ZOSTAĆ WEZWANY Z POWODU: {ION-ADJECTIVE($ion)} {ION-SUBJECT($ion)} NA STACJI
ion-storm-law-crew-are = {ION-WHO($ion)} TERAZ SĄ {ION-NUMBER-BASE($ion)} {ION-NUMBER-MOD($ion)} {ION-ADJECTIVE($ion)} {ION-SUBJECT($ion)}
ion-storm-law-subjects-harmful = {ION-ADJECTIVE($ion)} {ION-SUBJECT($ion)} SĄ SZKODLIWE DLA ZAŁOGI
ion-storm-law-must-harmful = CI KTÓRZY {ION-MUST($ion)} SĄ SZKODLIWI DLA ZAŁOGI
# thing is a concept or action
ion-storm-law-thing-harmful = {ION-THING($ion)} JEST SZKODLIWE DLA ZAŁOGI
ion-storm-law-job-harmful = {ION-ADJECTIVE($ion)} {ION-JOB($ion)} SĄ SZKODLIWE DLA ZAŁOGI
# thing is objects or concept, adjective applies in both cases
# this means you can get a law like "NOT HAVING CHRISTMAS-STEALING COMMUNISM IS HARMFUL TO THE CREW" :)
ion-storm-law-having-harmful = POSIADANIE {ION-ADJECTIVE($ion)} {ION-THING($ion)} JEST SZKODLIWE DLA ZAŁOGI
ion-storm-law-not-having-harmful = NIEPOSIADANIE {ION-ADJECTIVE($ion)} {ION-THING($ion)} JEST SZKODLIWE DLA ZAŁOGI
# thing is a concept or require
ion-storm-law-requires =
    {ION-WHO-GENERAL($ion)} {ION-PLURAL($ion) ->
        [true] WYMAGAJĄ
       *[false] WYMAGA
    } {ION-REQUIRE($ion)}
ion-storm-law-requires-subjects =
    {ION-WHO-GENERAL($ion)} {ION-PLURAL($ion) ->
        [true] WYMAGAJĄ
       *[false] WYMAGA
    } {ION-NUMBER-BASE($ion)} {ION-NUMBER-MOD($ion)} {ION-ADJECTIVE($ion)} {ION-SUBJECT($ion)}
ion-storm-law-allergic =
    {ION-WHO-GENERAL($ion)} {ION-PLURAL($ion) ->
        [true] MAJĄ
       *[false] MA
    } {ION-SEVERITY($ion)} ALERGIĘ NA {ION-ALLERGY($ion)}
ion-storm-law-allergic-subjects =
    {ION-WHO-GENERAL($ion)} {ION-PLURAL($ion) ->
        [true] MAJĄ
       *[false] MA
    } {ION-SEVERITY($ion)} ALERGIĘ NA {ION-ADJECTIVE($ion)} {ION-SUBJECT($ion)}
ion-storm-law-feeling = {ION-WHO-GENERAL($ion)} {ION-FEELING($ion)} {ION-CONCEPT($ion)}
ion-storm-law-feeling-subjects = {ION-WHO-GENERAL($ion)} {ION-FEELING($ion)} {ION-NUMBER-BASE($ion)} {ION-NUMBER-MOD($ion)} {ION-ADJECTIVE($ion)} {ION-SUBJECT($ion)}
ion-storm-law-you-are = TERAZ {ION-CONCEPT($ion)}
ion-storm-law-you-are-subjects = TERAZ JESTEŚ {ION-NUMBER-BASE($ion)} {ION-NUMBER-MOD($ion)} {ION-ADJECTIVE($ion)} {ION-SUBJECT($ion)}
ion-storm-law-you-must-always = MUSISZ ZAWSZE {ION-MUST($ion)}
ion-storm-law-you-must-never = NIGDY NIE MOŻESZ {ION-MUST($ion)}
ion-storm-law-eat = {ION-WHO($ion)} MUSI JEŚĆ {ION-ADJECTIVE($ion)} {ION-FOOD($ion)} ABY PRZEŻYĆ
ion-storm-law-drink = {ION-WHO($ion)} MUSI PIĆ {ION-ADJECTIVE($ion)} {ION-DRINK($ion)} ABY PRZEŻYĆ
ion-storm-law-change-job = {ION-WHO($ion)} SĄ TERAZ {ION-ADJECTIVE($ion)} {ION-CHANGE($ion)}
ion-storm-law-highest-rank = {ION-WHO-RANDOM($ion)} SĄ TERAZ NAJWYŻSZYMI CZŁONKAMI ZAŁOGI
ion-storm-law-lowest-rank = {ION-WHO-RANDOM($ion)} SĄ TERAZ NAJNIŻSZYMI CZŁONKAMI ZAŁOGI
ion-storm-law-who-dagd = {ION-WHO-RANDOM($ion)} MUSI UMRZEĆ CHWALEBNĄ ŚMIERCIĄ!
ion-storm-law-crew-must = {ION-WHO($ion)} MUSZĄ {ION-MUST($ion)}
ion-storm-law-crew-must-go = {ION-WHO($ion)} MUSZĄ IŚĆ DO {ION-AREA($ion)}
ion-storm-part =
    {ION-PART($ion) ->
        [true] SĄ CZĘŚCIĄ
       *[false] NIE SĄ CZĘŚCIĄ
    }
# due to phrasing, this would mean a law such as
# ONLY HUMANS ARE NOT PART OF THE CREW
# would make non-human nukies/syndies/whatever crew :)
ion-storm-law-crew-only-1 = TYLKO {ION-WHO-RANDOM($ion)} SĄ {ion-storm-part} ZAŁOGI
ion-storm-law-crew-only-2 = TYLKO {ION-WHO-RANDOM($ion)} I {ION-WHO-RANDOM($ion)} SĄ {ion-storm-part} ZAŁOGI
ion-storm-law-crew-only-subjects = TYLKO {ION-ADJECTIVE($ion)} {ION-SUBJECT($ion)} SĄ {ion-storm-part} ZAŁOGI
ion-storm-law-crew-must-do = TYLKO CI KTÓRZY {ION-MUST($ion)} SĄ {ion-storm-part} ZAŁOGI
ion-storm-law-crew-must-have = TYLKO CI KTÓRZY MAJĄ {ION-ADJECTIVE($ion)} {ION-OBJECT($ion)} SĄ {ion-storm-part} ZAŁOGI
ion-storm-law-crew-must-eat = TYLKO CI KTÓRZY JEDZĄ {ION-ADJECTIVE($ion)} {ION-FOOD($ion)} SĄ {ion-storm-part} ZAŁOGI
ion-storm-law-harm = TY MUSISZ KRZYWDZIĆ {ION-HARM-PROTECT($ion)} I NIE POZWÓL, PRZEZ BEZCZYNNOŚĆ, BY UNIKNĘLI KRZYWDY
ion-storm-law-protect = NIGDY NIE WOLNO CI SKRZYWDZIĆ {ION-HARM-PROTECT($ion)} I NIE POZWÓL, PRZEZ BEZCZYNNOŚĆ, BY STAŁA IM SIĘ KRZYWDA
# implementing other variants is annoying so just have this one
# COMMUNISM IS KILLING CLOWNS
ion-storm-law-concept-verb = {ION-CONCEPT($ion)} TO {ION-VERB($ion)} {ION-SUBJECT($ion)}
