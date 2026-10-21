entity-effect-guidebook-spawn-entity =
    { $chance ->
        [1] Tworzy
       *[other] stworzyć
    } { $amount ->
        [1] { $entname }
       *[other] { $amount }× { $entname }
    }
entity-effect-guidebook-destroy =
    { $chance ->
        [1] Niszczy
       *[other] zniszczyć
    } obiekt
entity-effect-guidebook-break =
    { $chance ->
        [1] Łamie
       *[other] złamać
    } obiekt
entity-effect-guidebook-explosion =
    { $chance ->
        [1] Wywołuje
       *[other] wywołać
    } eksplozję
entity-effect-guidebook-emp =
    { $chance ->
        [1] Wywołuje
       *[other] wywołać
    } impuls elektromagnetyczny
entity-effect-guidebook-flash =
    { $chance ->
        [1] Wywołuje
       *[other] wywołać
    } oślepiający błysk
entity-effect-guidebook-satiate =
    { $chance ->
        [1] Syci
       *[other] nasycić
    } { $relative ->
        [1] { $type } w przeciętnym tempie
       *[other] { $type } w tempie { NATURALFIXED($relative, 3) }x średniej
    }
entity-effect-guidebook-health-change =
    { $chance ->
        [1]
            { $healsordeals ->
                [heals] Leczy
                [deals] Zadaje
               *[both] Zmienia zdrowie o
            }
       *[other]
            { $healsordeals ->
                [heals] leczyć
                [deals] zadać
               *[both] zmienić zdrowie o
            }
    } { $changes }
entity-effect-guidebook-even-health-change =
    { $chance ->
        [1]
            { $healsordeals ->
                [heals] Równomiernie leczy
                [deals] Równomiernie zadaje
               *[both] Równomiernie zmienia zdrowie o
            }
       *[other]
            { $healsordeals ->
                [heals] równomiernie leczyć
                [deals] równomiernie zadać
               *[both] równomiernie zmienić zdrowie o
            }
    } { $changes }
entity-effect-guidebook-status-effect-old =
    { $type ->
        [update]
            { $chance ->
                [1] Wywołuje
               *[other] wywołać
            } { LOC($key) } na co najmniej { NATURALFIXED($time, 3) } { MANY("second", $time) } bez kumulacji
        [add]
            { $chance ->
                [1] Wywołuje
               *[other] wywołać
            } { LOC($key) } na co najmniej { NATURALFIXED($time, 3) } { MANY("second", $time) } z kumulacją
        [set]
            { $chance ->
                [1] Wywołuje
               *[other] wywołać
            } { LOC($key) } na { NATURALFIXED($time, 3) } { MANY("second", $time) } bez kumulacji
       *[remove]
            { $chance ->
                [1] Usuwa
               *[other] usunąć
            } { NATURALFIXED($time, 3) } { MANY("second", $time) } efektu: { LOC($key) }
    }
entity-effect-guidebook-status-effect =
    { $type ->
        [update]
            { $chance ->
                [1] Wywołuje
               *[other] wywołać
            } { $key } na co najmniej { NATURALFIXED($time, 3) } { MANY("second", $time) } bez kumulacji
        [add]
            { $chance ->
                [1] Wywołuje
               *[other] wywołać
            } { $key } na co najmniej { NATURALFIXED($time, 3) } { MANY("second", $time) } z kumulacją
        [set]
            { $chance ->
                [1] Wywołuje
               *[other] wywołać
            } { $key } na co najmniej { NATURALFIXED($time, 3) } { MANY("second", $time) } bez kumulacji
       *[remove]
            { $chance ->
                [1] Usuwa
               *[other] usunąć
            } { NATURALFIXED($time, 3) } { MANY("second", $time) } efektu: { $key }
    } { $delay ->
        [0] natychmiast
       *[other] po opóźnieniu wynoszącym { NATURALFIXED($delay, 3) } s
    }
entity-effect-guidebook-status-effect-indef =
    { $type ->
        [update]
            { $chance ->
                [1] Wywołuje
               *[other] wywołać
            } trwały efekt: { $key }
        [add]
            { $chance ->
                [1] Wywołuje
               *[other] wywołać
            } trwały efekt: { $key }
        [set]
            { $chance ->
                [1] Wywołuje
               *[other] wywołać
            } trwały efekt: { $key }
       *[remove]
            { $chance ->
                [1] Usuwa
               *[other] usunąć
            } efekt: { $key }
    } { $delay ->
        [0] natychmiast
       *[other] po opóźnieniu wynoszącym { NATURALFIXED($delay, 3) } s
    }
entity-effect-guidebook-knockdown =
    { $type ->
        [update]
            { $chance ->
                [1] Wywołuje
               *[other] wywołać
            } { LOC($key) } na co najmniej { NATURALFIXED($time, 3) } { MANY("second", $time) } bez kumulacji
        [add]
            { $chance ->
                [1] Wywołuje
               *[other] wywołać
            } powalenie na co najmniej { NATURALFIXED($time, 3) } { MANY("second", $time) } z kumulacją
       *[set]
            { $chance ->
                [1] Wywołuje
               *[other] wywołać
            } powalenie na co najmniej { NATURALFIXED($time, 3) } { MANY("second", $time) } bez kumulacji
        [remove]
            { $chance ->
                [1] Usuwa
               *[other] usunąć
            } { NATURALFIXED($time, 3) } { MANY("second", $time) } powalenia
    }
entity-effect-guidebook-set-solution-temperature-effect =
    { $chance ->
        [1] Ustawia
       *[other] ustawić
    } temperaturę roztworu dokładnie na { NATURALFIXED($temperature, 2) }k
entity-effect-guidebook-adjust-solution-temperature-effect =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Dodaje
               *[-1] Odbiera
            }
       *[other]
            { $deltasign ->
                [1] dodać
               *[-1] odebrać
            }
    } ciepło roztworowi, aż osiągnie { $deltasign ->
        [1] co najwyżej { NATURALFIXED($maxtemp, 2) }k
       *[-1] co najmniej { NATURALFIXED($mintemp, 2) }k
    }
entity-effect-guidebook-adjust-reagent-reagent =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Dodaje
               *[-1] Usuwa
            }
       *[other]
            { $deltasign ->
                [1] dodać
               *[-1] usunąć
            }
    } { NATURALFIXED($amount, 2) }u reagentu { $reagent } { $deltasign ->
        [1] do
       *[-1] z
    } roztworu
entity-effect-guidebook-adjust-temperature =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Dodaje
               *[-1] Odbiera
            }
       *[other]
            { $deltasign ->
                [1] dodać
               *[-1] odebrać
            }
    } { POWERJOULES($amount) } ciepła { $deltasign ->
        [1] do
       *[-1] z
    } ciała, w którym się znajduje
entity-effect-guidebook-jittering =
    { $chance ->
        [1] Wywołuje
       *[other] wywołać
    } drgawki
entity-effect-guidebook-clean-bloodstream =
    { $chance ->
        [1] Oczyszcza
       *[other] oczyścić
    } krwiobieg z innych substancji chemicznych
entity-effect-guidebook-eye-damage =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Zadaje
               *[-1] Leczy
            }
       *[other]
            { $deltasign ->
                [1] zadać
               *[-1] leczyć
            }
    } uszkodzenia oczu
entity-effect-guidebook-vomit =
    { $chance ->
        [1] Wywołuje
       *[other] wywołać
    } wymioty
entity-effect-guidebook-create-gas =
    { $chance ->
        [1] Tworzy
       *[other] stworzyć
    } { $moles } mol gazu: { $gas }
entity-effect-guidebook-drunk =
    { $chance ->
        [1] Wywołuje
       *[other] wywołać
    } upojenie alkoholowe
entity-effect-guidebook-electrocute =
    { $chance ->
        [1]
            { $stuns ->
                [true] Poraża prądem
               *[false] Razi prądem
            }
       *[other]
            { $stuns ->
                [true] porazić prądem
               *[false] razić prądem
            }
    } metabolizującego przez { NATURALFIXED($time, 3) } { MANY("second", $time) }
entity-effect-guidebook-emote =
    { $chance ->
        [1] Wymusi
       *[other] wymusić
    } na metabolizującym emocję [bold][color=white]{ $emote }[/color][/bold]
entity-effect-guidebook-extinguish-reaction =
    { $chance ->
        [1] Gasi
       *[other] ugasić
    } ogień
entity-effect-guidebook-flammable-reaction =
    { $chance ->
        [1] Zwiększa
       *[other] zwiększyć
    } łatwopalność
entity-effect-guidebook-ignite =
    { $chance ->
        [1] Podpala
       *[other] podpalić
    } metabolizującego
entity-effect-guidebook-make-sentient =
    { $chance ->
        [1] Czyni
       *[other] uczynić
    } metabolizującego świadomym
entity-effect-guidebook-make-polymorph =
    { $chance ->
        [1] Przemienia
       *[other] przemienić
    } metabolizującego w: { $entityname }
entity-effect-guidebook-modify-bleed-amount =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Wywołuje
               *[-1] Zmniejsza
            }
       *[other]
            { $deltasign ->
                [1] wywołać
               *[-1] zmniejszyć
            }
    } krwawienie
entity-effect-guidebook-modify-blood-level =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Zwiększa
               *[-1] Zmniejsza
            }
       *[other]
            { $deltasign ->
                [1] zwiększyć
               *[-1] zmniejszyć
            }
    } poziom krwi
entity-effect-guidebook-paralyze =
    { $chance ->
        [1] Paraliżuje
       *[other] sparaliżować
    } metabolizującego na co najmniej { NATURALFIXED($time, 3) } { MANY("second", $time) }
entity-effect-guidebook-movespeed-modifier =
    { $chance ->
        [1] Zmienia
       *[other] zmienić
    } prędkość ruchu o współczynnik { NATURALFIXED($sprintspeed, 3) }x na co najmniej { NATURALFIXED($time, 3) } { MANY("second", $time) }
entity-effect-guidebook-reset-narcolepsy =
    { $chance ->
        [1] Tymczasowo powstrzymuje
       *[other] tymczasowo powstrzymać
    } narkolepsję
entity-effect-guidebook-wash-cream-pie-reaction =
    { $chance ->
        [1] Zmywa
       *[other] zmyć
    } tort śmietankowy z twarzy
entity-effect-guidebook-cure-zombie-infection =
    { $chance ->
        [1] Leczy
       *[other] wyleczyć
    } trwającą infekcję zombie
entity-effect-guidebook-cause-zombie-infection =
    { $chance ->
        [1] Zaraża
       *[other] zarazić
    } osobnika infekcją zombie
entity-effect-guidebook-innoculate-zombie-infection =
    { $chance ->
        [1] Leczy
       *[other] wyleczyć
    } trwającą infekcję zombie i zapewnia odporność na przyszłe infekcje
entity-effect-guidebook-heal-bones =
    { $chance ->
        [1] Leczy
       *[other] wyleczyć
    } wszystkie złamane i pęknięte kości w ciele
entity-effect-guidebook-reduce-rotting =
    { $chance ->
        [1] Cofa
       *[other] cofnąć
    } gnicie o { NATURALFIXED($time, 3) } { MANY("second", $time) }
entity-effect-guidebook-area-reaction =
    { $chance ->
        [1] Wywołuje
       *[other] wywołać
    } reakcję dymu lub piany na { NATURALFIXED($duration, 3) } { MANY("second", $duration) }
entity-effect-guidebook-add-to-solution-reaction =
    { $chance ->
        [1] Powoduje
       *[other] spowodować
    }, że { $reagent } zostaje dodany do jego wewnętrznego pojemnika na roztwór
entity-effect-guidebook-artifact-unlock =
    { $chance ->
        [1] Pomaga
       *[other] pomóc
    } odblokować obcy artefakt.
entity-effect-guidebook-artifact-durability-restore = Przywraca { $restored } wytrzymałości w aktywnych węzłach obcego artefaktu.
entity-effect-guidebook-plant-attribute =
    { $chance ->
        [1] Adjusts
       *[other] adjust
    } { $attribute } by { $positive ->
        [true] [color=red]{ $amount }[/color]
       *[false] [color=green]{ $amount }[/color]
    }
entity-effect-guidebook-plant-cryoxadone =
    { $chance ->
        [1] Odmładza
       *[other] odmłodzić
    } roślinę, zależnie od jej wieku i czasu wzrostu
entity-effect-guidebook-plant-phalanximine =
    { $chance ->
        [1] Przywraca
       *[other] przywrócić
    } żywotność roślinie, która utraciła ją przez mutację
entity-effect-guidebook-plant-diethylamine =
    { $chance ->
        [1] Zwiększa
       *[other] zwiększyć
    } długość życia rośliny i/lub jej bazowe zdrowie, z 10% szansą dla każdego z nich
entity-effect-guidebook-plant-robust-harvest =
    { $chance ->
        [1] Zwiększa
       *[other] zwiększyć
    } siłę działania rośliny o { $increase } do maksymalnie { $limit }. Powoduje utratę nasion przez roślinę, gdy siła działania osiągnie { $seedlesstreshold }. Próba zwiększenia siły działania powyżej { $limit } może z 10% szansą obniżyć plon
entity-effect-guidebook-plant-seeds-add =
    { $chance ->
        [1] Przywraca
       *[other] przywrócić
    } nasiona rośliny
entity-effect-guidebook-plant-seeds-remove =
    { $chance ->
        [1] Usuwa
       *[other] usunąć
    } nasiona rośliny
entity-effect-guidebook-plant-mutate-exude-gasses =
    { $chance ->
        [1] Mutuje
       *[other] zmutować
    } roślinę tak, by wydzielała gazy w ilości od { $minValue } do { $maxValue } mol
entity-effect-guidebook-plant-mutate-consume-gasses =
    { $chance ->
        [1] Mutuje
       *[other] zmutować
    } roślinę tak, by pochłaniała gazy w ilości od { $minValue } do { $maxValue } mol
entity-effect-guidebook-plant-mutate-chemicals =
    { $chance ->
        [1] Mutuje
       *[other] zmutować
    } roślinę tak, by wytwarzała: { $name }
entity-effect-guidebook-add-reagent-to-bloodstream =
    { $chance ->
        [1] Wstrzykuje
       *[other] wstrzyknąć
    } { $quantity } reagentu { $reagent } bezpośrednio do krwiobiegu
entity-effect-disarm =
    { $chance ->
        [1] Disarms
       *[other] disarms
    } the entity
entity-effect-guidebook-bleed-unholy-blood =
    { $chance ->
        [1] Przemienia
       *[other] przemienić
    } wypływającą krew w nieświętą krew
entity-effect-guidebook-de-cultify =
    { $chance ->
        [1] Zmniejsza
       *[other] zmniejszyć
    } zepsucie kultem o { $amount }
