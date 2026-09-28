## Scenariusze lotu ewakuacyjnego

evac-scenario-sender-shuttle = Komputer pokładowy wahadłowca

## Katastrofa na planecie

evac-scenario-planet-crash-asteroids = Uwaga! Wahadłowiec wchodzi w pas asteroid. Osłony przeciwmeteorytowe nie odpowiadają, dokonano automatyczną zmianę kursu.
evac-scenario-planet-crash-breach = Wykryto rozszczelnienie kadłuba w wielu sekcjach. Załóżcie maski i podłączcie butle z tlenem.
evac-scenario-planet-crash-brace = AWARIA NAPĘDU FTL. Wyjście z nadprzestrzeni za 15 sekund. PRZYGOTOWAĆ SIĘ NA UDERZENIE!
evac-scenario-planet-crash-lost = Utraciliśmy kontakt z wahadłowcem ratunkowym. Ostatni sygnał nadał znad niezbadanej planety w waszym sektorze. Ekipa ratunkowa jest w drodze.
evac-scenario-crash-round-end = Wahadłowiec ratunkowy rozbił się na planecie { $planet }.

evac-scenario-planet-lava = Tartar IV
evac-scenario-planet-snow = Niflheim
evac-scenario-planet-jungle = Amazonia-W-VII
evac-scenario-planet-desert = Kadesz

## Komenda

cmd-evacscenario-desc = Pokazuje scenariusze lotu ewakuacyjnego, wymusza scenariusz dla tej rundy albo od razu wysyła wahadłowiec, żeby go przetestować.
cmd-evacscenario-help =
    Użycie:
      { $command } list – scenariusze i ich szansa w tej rundzie
      { $command } force <scenariusz|none> – wymusza scenariusz (albo lot bez scenariusza) przy ewakuacji w tej rundzie
      { $command } clear – scenariusz znów zostanie wylosowany przy starcie wahadłowca
      { $command } test <scenariusz> – przywołuje wahadłowiec, od razu wysyła go z tym scenariuszem i przenosi cię na pokład. UWAGA: runda się po nim skończy!
cmd-evacscenario-arg-action = <list|force|clear|test>
cmd-evacscenario-arg-scenario = <scenariusz>
cmd-evacscenario-list-entry = { $id }: { $chance }% w tej rundzie
cmd-evacscenario-list-empty = Nie ma żadnych scenariuszy lotu ewakuacyjnego.
cmd-evacscenario-state-random = Scenariusz zostanie wylosowany przy starcie wahadłowca.
cmd-evacscenario-state-disabled = Losowanie scenariuszy jest wyłączone (evac.scenarios_enabled), zadziała tylko wymuszony.
cmd-evacscenario-state-forced = Wymuszony scenariusz: { $id }.
cmd-evacscenario-state-forced-none = Wymuszony lot bez scenariusza.
cmd-evacscenario-forced = Ewakuacja w tej rundzie przebiegnie według scenariusza { $id }.
cmd-evacscenario-forced-none = Ewakuacja w tej rundzie odbędzie się bez scenariusza.
cmd-evacscenario-cleared = Scenariusz zostanie wylosowany przy starcie wahadłowca.
cmd-evacscenario-unknown = { $id } nie jest scenariuszem lotu ewakuacyjnego.
cmd-evacscenario-not-in-round = To działa tylko w trakcie rundy.
cmd-evacscenario-already-left = Wahadłowiec ratunkowy już odleciał w tej rundzie.
cmd-evacscenario-evac-enabled = Wahadłowiec ratunkowy był wyłączony na tym serwerze (shuttle.emergency), więc go włączono.
cmd-evacscenario-no-shuttle = Nie znaleziono wahadłowca ratunkowego.
cmd-evacscenario-test-started = Wahadłowiec ratunkowy odlatuje za kilka sekund ze scenariuszem { $id }.
cmd-evacscenario-test-boarded = Przeniesiono cię na pokład wahadłowca.
