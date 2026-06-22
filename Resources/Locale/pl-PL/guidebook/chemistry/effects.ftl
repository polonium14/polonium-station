-create-3rd-person =
    { $chance ->
        [1] Tworzy
       *[other] tworzyć
    }
-cause-3rd-person =
    { $chance ->
        [1] Powoduje
       *[other] powodować
    }
-satiate-3rd-person =
    { $chance ->
        [1] Zaspokaja
       *[other] zaspokajać
    }
reagent-effect-guidebook-create-entity-reaction-effect =
    { $chance ->
        [1] Tworzy
       *[other] tworzy
    } { $amount ->
        [1] { INDEFINITE($entname) }
       *[other] { $amount } { MAKEPLURAL($entname) }
    }
reagent-effect-guidebook-explosion-reaction-effect =
    { $chance ->
        [1] Wywołuje
       *[other] wywołuje
    } eksplozję
reagent-effect-guidebook-emp-reaction-effect =
    { $chance ->
        [1] Generuje
       *[other] generuje
    } impuls elektromagnetyczny (EMP)
reagent-effect-guidebook-flash-reaction-effect =
    { $chance ->
        [1] Wywołuje
       *[other] wywołuje
    } oślepiający błysk
reagent-effect-guidebook-foam-area-reaction-effect =
    { $chance ->
        [1] Tworzy
       *[other] tworzy
    } duże ilości piany
reagent-effect-guidebook-smoke-area-reaction-effect =
    { $chance ->
        [1] Tworzy
       *[other] tworzy
    } duże ilości dymu
reagent-effect-guidebook-satiate-thirst =
    { $chance ->
        [1] Zaspokaja
       *[other] zaspokaja
    } { $relative ->
        [1] pragnienie w stopniu przeciętnym
       *[other] pragnienie z { NATURALFIXED($relative, 3) }-krotnością przeciętnego tempa
    }
reagent-effect-guidebook-satiate-hunger =
    { $chance ->
        [1] Zaspokaja
       *[other] zaspokaja
    } { $relative ->
        [1] głód w stopniu przeciętnym
       *[other] głód z { NATURALFIXED($relative, 3) }-krotnością przeciętnego tempa
    }
reagent-effect-guidebook-health-change =
    { $chance ->
        [1]
            { $healsordeals ->
                [heals] Leczy
                [deals] Zadaje
               *[both] Modyfikuje zdrowie o
            }
       *[other]
            { $healsordeals ->
                [heals] leczy
                [deals] zadaje
               *[both] modyfikuje zdrowie o
            }
    } { $changes }
reagent-effect-guidebook-even-health-change =
    { $chance ->
        [1]
            { $healsordeals ->
                [heals] Równomiernie leczy
                [deals] Równomiernie zadaje
               *[both] Równomiernie modyfikuje zdrowie o
            }
       *[other]
            { $healsordeals ->
                [heals] równomiernie leczy
                [deals] równomiernie zadaje
               *[both] równomiernie modyfikuje zdrowie o
            }
    } { $changes }
reagent-effect-guidebook-status-effect =
    { $type ->
        [add]
            { $chance ->
                [1] Wywołuje
               *[other] wywołuje
            } { LOC($key) } na co najmniej { NATURALFIXED($time, 3) } { MANY("sekundę", $time) } z kumulacją
       *[set]
            { $chance ->
                [1] Wywołuje
               *[other] wywołuje
            } { LOC($key) } na co najmniej { NATURALFIXED($time, 3) } { MANY("sekundę", $time) } bez kumulacji
        [remove]
            { $chance ->
                [1] Skraca
               *[other] skraca
            } czas trwania { LOC($key) } o { NATURALFIXED($time, 3) } { MANY("sekundę", $time) }
    }
reagent-effect-guidebook-set-solution-temperature-effect =
    { $chance ->
        [1] Ustala
       *[other] ustala
    } temperaturę roztworu na dokładnie { NATURALFIXED($temperature, 2) } K
reagent-effect-guidebook-adjust-solution-temperature-effect =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Podnosi
               *[-1] Obniża
            }
       *[other]
            { $deltasign ->
                [1] podnosi
               *[-1] obniża
            }
    } temperaturę roztworu, aż osiągnie { $deltasign ->
        [1] maksymalnie { NATURALFIXED($maxtemp, 2) } K
       *[-1] co najmniej { NATURALFIXED($mintemp, 2) } K
    }
reagent-effect-guidebook-adjust-reagent-reagent =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Dodaje
               *[-1] Usuwa
            }
       *[other]
            { $deltasign ->
                [1] dodaje
               *[-1] usuwa
            }
    } { NATURALFIXED($amount, 2) } u substancji { $reagent } { $deltasign ->
        [1] do
       *[-1] z
    } roztworu
reagent-effect-guidebook-adjust-reagent-group =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Dodaje
               *[-1] Usuwa
            }
       *[other]
            { $deltasign ->
                [1] dodaje
               *[-1] usuwa
            }
    } { NATURALFIXED($amount, 2) } u odczynników z grupy { $group } { $deltasign ->
        [1] do
       *[-1] z
    } roztworu
reagent-effect-guidebook-adjust-temperature =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Przekazuje
               *[-1] Odprowadza
            }
       *[other]
            { $deltasign ->
                [1] przekazuje
               *[-1] odprowadza
            }
    } { POWERJOULES($amount) } energii cieplnej { $deltasign ->
        [1] do
       *[-1] z
    } organizmu, w którym się znajduje
reagent-effect-guidebook-chem-cause-disease =
    { $chance ->
        [1] Wywołuje
       *[other] wywołuje
    } chorobę: { $disease }
reagent-effect-guidebook-chem-cause-random-disease =
    { $chance ->
        [1] Wywołuje
       *[other] wywołuje
    } choroby: { $diseases }
reagent-effect-guidebook-jittering =
    { $chance ->
        [1] Powoduje
       *[other] powoduje
    } drgawki
reagent-effect-guidebook-chem-clean-bloodstream =
    { $chance ->
        [1] Oczyszcza
       *[other] oczyszcza
    } krwiobieg z innych chemikaliów
reagent-effect-guidebook-cure-disease =
    { $chance ->
        [1] Leczy
       *[other] leczy
    } choroby
reagent-effect-guidebook-cure-eye-damage =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Powoduje
               *[-1] Leczy
            }
       *[other]
            { $deltasign ->
                [1] powoduje
               *[-1] leczy
            }
    } uszkodzenia wzroku
reagent-effect-guidebook-chem-vomit =
    { $chance ->
        [1] Wywołuje
       *[other] wywołuje
    } wymioty
reagent-effect-guidebook-create-gas =
    { $chance ->
        [1] Tworzy
       *[other] tworzy
    } { $moles } { $moles ->
        [1] mol
       *[other] moli
    } gazu { $gas }
reagent-effect-guidebook-drunk =
    { $chance ->
        [1] Powoduje
       *[other] powoduje
    } upojenie alkoholowe
reagent-effect-guidebook-electrocute =
    { $chance ->
        [1] Poraża prądem
       *[other] poraża prądem
    } cel na { NATURALFIXED($time, 3) } { MANY("sekundę", $time) }
reagent-effect-guidebook-emote =
    { $chance ->
        [1] Zmusza
       *[other] zmusza
    } cel do wykonania gestu/emotki: [bold][color=white]{ $emote }[/color][/bold]
reagent-effect-guidebook-extinguish-reaction =
    { $chance ->
        [1] Gasi
       *[other] gasi
    } ogień
reagent-effect-guidebook-flammable-reaction =
    { $chance ->
        [1] Zwiększa
       *[other] zwiększa
    } łatwopalność
reagent-effect-guidebook-ignite =
    { $chance ->
        [1] Podpala
       *[other] podpala
    } cel
reagent-effect-guidebook-make-sentient =
    { $chance ->
        [1] Nadaje
       *[other] nadaje
    } celowi samoświadomość
reagent-effect-guidebook-make-polymorph =
    { $chance ->
        [1] Polimorfuje
       *[other] polimorfuje
    } cel w { $entityname }
reagent-effect-guidebook-modify-bleed-amount =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Wywołuje
               *[-1] Zmniejsza
            }
       *[other]
            { $deltasign ->
                [1] wywołuje
               *[-1] zmniejsza
            }
    } krwawienie
reagent-effect-guidebook-modify-blood-level =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Zwiększa
               *[-1] Zmniejsza
            }
       *[other]
            { $deltasign ->
                [1] zwiększa
               *[-1] zmniejsza
            }
    } poziom krwi
reagent-effect-guidebook-paralyze =
    { $chance ->
        [1] Paraliżuje
       *[other] paraliżuje
    } cel na co najmniej { NATURALFIXED($time, 3) } { MANY("sekundę", $time) }
reagent-effect-guidebook-movespeed-modifier =
    { $chance ->
        [1] Modyfikuje
       *[other] modyfikuje
    } prędkość poruszania się o { NATURALFIXED($walkspeed, 3) }x na co najmniej { NATURALFIXED($time, 3) } { MANY("sekundę", $time) }
reagent-effect-guidebook-reset-narcolepsy =
    { $chance ->
        [1] Tymczasowo powstrzymuje
       *[other] tymczasowo powstrzymuje
    } ataki narkolepsji
reagent-effect-guidebook-wash-cream-pie-reaction =
    { $chance ->
        [1] Zmywa
       *[other] zmywa
    } resztki tary z kremem z twarzy
reagent-effect-guidebook-cure-zombie-infection =
    { $chance ->
        [1] Leczy
       *[other] leczy
    } aktywną infekcję zombie
reagent-effect-guidebook-cause-zombie-infection =
    { $chance ->
        [1] Zaraża
       *[other] zaraża
    } cel infekcją zombie
reagent-effect-guidebook-innoculate-zombie-infection =
    { $chance ->
        [1] Leczy
       *[other] leczy
    } aktywną infekcję zombie oraz zapewnia odporność na przyszłe zakażenia
reagent-effect-guidebook-reduce-rotting =
    { $chance ->
        [1] Cofa gnicie o
       *[other] cofa gnicie o
    } { NATURALFIXED($time, 3) } { MANY("sekundę", $time) }
reagent-effect-guidebook-area-reaction =
    { $chance ->
        [1] Wywołuje
       *[other] wywołuje
    } reakcję dymną lub pianową na { NATURALFIXED($duration, 3) } { MANY("sekundę", $duration) }
reagent-effect-guidebook-add-to-solution-reaction =
    { $chance ->
        [1] Powoduje, że
       *[other] powoduje, że
    } chemikalia nałożone na obiekt trafiają bezpośrednio do jego wewnętrznego zbiornika na roztwory
reagent-effect-guidebook-artifact-unlock =
    { $chance ->
        [1] Pomaga
       *[other] pomaga
    } odblokować obcy artefakt.
reagent-effect-guidebook-plant-attribute =
    { $chance ->
        [1] Modyfikuje
       *[other] modyfikuje
    } { $attribute } o [color={ $colorName }]{ $amount }[/color]
reagent-effect-guidebook-plant-cryoxadone =
    { $chance ->
        [1] Odmładza
       *[other] odmładza
    } roślinę, w zależności od jej obecnego wieku i czasu wzrostu
reagent-effect-guidebook-plant-phalanximine =
    { $chance ->
        [1] Przywraca
       *[other] przywraca
    } żywotność roślinie, która stała się bezpłodna w wyniku mutacji
reagent-effect-guidebook-plant-diethylamine =
    { $chance ->
        [1] Zwiększa
       *[other] zwiększa
    } długość życia i/lub bazowe zdrowie rośliny (10% szansy na każdy efekt)
reagent-effect-guidebook-plant-robust-harvest =
    { $chance ->
        [1] Zwiększa
       *[other] zwiększa
    } potencjał rośliny o { $increase } (maksymalnie do { $limit }). Powoduje, że roślina traci nasiona po osiągnięciu progu { $seedlesstreshold }. Próba zwiększenia potencjału powyżej { $limit } wiąże się z 10% szansą na zmniejszenie plonów
reagent-effect-guidebook-plant-seeds-add =
    { $chance ->
        [1] Przywraca
       *[other] przywraca
    } nasiona rośliny
reagent-effect-guidebook-add-to-chemicals =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Dodaje
               *[-1] Usuwa
            }
       *[other]
            { $deltasign ->
                [1] dodaje
               *[-1] usuwa
            }
    } { NATURALFIXED($amount, 2) } u substancji { $reagent } { $deltasign ->
        [1] do
       *[-1] z
    } roztworu
reagent-effect-guidebook-adjust-ling-chemicals =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Dodaje
               *[-1] Usuwa
            }
       *[other]
            { $deltasign ->
                [1] dodaje
               *[-1] usuwa
            }
    } { NATURALFIXED($amount, 2) } u chemikaliów gwiezdnego wampira (changelinga) { $deltasign ->
        [1] do genomu
       *[-1] z genomu
    } celu
reagent-effect-guidebook-plant-seeds-remove =
    { $chance ->
        [1] Usuwa
       *[other] usuwa
    } nasiona rośliny