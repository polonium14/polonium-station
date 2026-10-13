ghost-role-information-morph-name = Morph
ghost-role-information-morph-description = Jesteś morphem, odrażającym tworem nauki, powstałym głównie z komórek zmiennokształtnego. Możesz przybrać kształt pobliskiego przedmiotu, co jednak zauważą osoby w pobliżu. W przebraniu poruszasz się szybciej, ale zadajesz mniej obrażeń, a kto przyjrzy ci się z bliska, wyczuje, że coś jest nie tak. Pożeraj przedmioty oraz martwe i nieprzytomne istoty. Trawienie istot przywraca ci zdrowie. Kto dotknie cię w przebraniu, zostanie ugryziony i zatruty. Możesz też przybrać postać pożartego załoganta, póki jego ciało jest w twoim brzuchu, ale nie potrafisz mówić.

roles-antag-morph-name = Morph
roles-antag-morph-objective = Pożeraj załogę i przetrwaj do końca zmiany.
role-subtype-morph = Morph
morph-round-end-agent-name = morph
objective-issuer-morph = [color=#3cff00]Głód[/color]
objective-morph-devour-title =
    Pożryj { $count } { $count ->
        [one] załoganta
       *[few] załogantów
        [many] załogantów
    }.
objective-morph-devour-description = Połknij { $count } { $count ->
        [one] członka
       *[few] członków
        [many] członków
    } załogi. Liczy się każda osoba tylko raz.

morph-devour-disguised = Nie możesz niczego zjeść w przebraniu!
morph-disguise-invalid = Nie możesz przybrać tego kształtu.
morph-disguise-cooldown = Twoja kameleonowa skóra wciąż się regeneruje!
morph-disguise-self = Wykręcasz swoje ciało i przybierasz kształt: {$target}.
morph-disguise-others = {CAPITALIZE($morph)} nagle się wykręca i zmienia kształt, stając się kopią: {$target}!
morph-disguise-examine = [color=red]Coś tu jest nie tak...[/color]
morph-reveal-already = Już masz swoją prawdziwą postać!
morph-reveal-self = Wracasz do swojej prawdziwej postaci.
morph-reveal-others = {CAPITALIZE($morph)} nagle zapada się w sobie i rozpływa w kupę zielonego mięsa!
morph-death-disguised = {CAPITALIZE($morph)} wykręca się i rozpływa w kupę zielonego mięsa!
morph-ambush-victim = {CAPITALIZE($morph)} cię gryzie!
morph-ambush-others = {CAPITALIZE($morph)} brutalnie gryzie: {$victim}!

morph-spit-nothing = Nie masz nic przygotowanego do wyplucia.
morph-spit-throw = {CAPITALIZE($morph)} pluje: {$item}!
morph-spit-drop = {CAPITALIZE($morph)} wypluwa: {$item}!
morph-throw-prepared = Przygotowujesz do wyplucia: {$item}.
morph-digest-start = Zaczynasz trawić: {$target}.
morph-digest-fail = Tego nie da się strawić: {$target}.
morph-digest-item = Trawisz: {$target}.
morph-digest-mob = Trawisz: {$target}. Odzyskujesz trochę zdrowia.

morph-stomach-entity = {CAPITALIZE($entity)}
morph-stomach-entity-prepared = {CAPITALIZE($entity)} (do wyplucia)
morph-stomach-drop = Wypluj
morph-stomach-throw = Przygotuj do wyplucia
morph-stomach-digest = Traw
morph-stomach-assume = Przybierz postać

reagent-name-morph-venom = Jad morpha
reagent-desc-morph-venom = Zabójczy jad zmiennokształtnego stworzenia.
