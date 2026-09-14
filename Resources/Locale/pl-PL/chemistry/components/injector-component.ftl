## UI

injector-volume-transfer-label =
    Objętość: [color=white]{ $currentVolume }/{ $totalVolume }u[/color]
    Tryb: [color=white]{ $modeString }[/color] ([color=white]{ $transferVolume }u[/color])
injector-volume-label =
    Objętość: [color=white]{ $currentVolume }/{ $totalVolume }[/color]
    Tryb: [color=white]{ $modeString }[/color]
injector-toggle-verb-text = Przełącz tryb wtrysku

## Entity

injector-component-inject-mode-name = wstrzyknij
injector-component-draw-mode-name = pobierz
injector-component-dynamic-mode-name = dynamiczny
injector-component-mode-changed-text = Obecnie { $mode }
injector-component-transfer-success-message = Przenosisz { $amount }u do { $target }.
injector-component-transfer-success-message-self = Przenosisz { $amount }u do siebie.
injector-component-inject-success-message = Wstrzykujesz { $amount }u do { $target }!
injector-component-inject-success-message-self = Wstrzykujesz { $amount }u w siebie!
injector-component-draw-success-message = Pobierasz { $amount }u z { $target }.
injector-component-draw-success-message-self = Pobierasz { $amount }u z siebie.

## Fail Messages

injector-component-target-already-full-message = { CAPITALIZE($target) } jest już pełny!
injector-component-target-already-full-message-self = Jesteś już pełny!
injector-component-target-is-empty-message = { CAPITALIZE($target) } jest pusty!
injector-component-target-is-empty-message-self = Jesteś pusty!
injector-component-cannot-toggle-draw-message = Za pełno, by pobierać!
injector-component-cannot-toggle-inject-message = Nic do wstrzyknięcia!
injector-component-cannot-toggle-dynamic-message = Nie można przełączyć na tryb dynamiczny!
injector-component-empty-message = { CAPITALIZE(THE($injector)) } jest pusty!
injector-component-blocked-user = Ekwipunek ochronny zablokował twoje wstrzyknięcie!
injector-component-blocked-other = Zbroja { CAPITALIZE(THE(POSS-ADJ($target))) } zablokowała wstrzyknięcie od { THE($user) }!
injector-component-cannot-transfer-message = Nie możesz przenieść do { $target }!
injector-component-cannot-transfer-message-self = Nie możesz przenieść do siebie!
injector-component-cannot-inject-message = Nie możesz wstrzyknąć do { $target }!
injector-component-cannot-inject-message-self = Nie możesz wstrzyknąć w siebie!
injector-component-cannot-draw-message = Nie możesz pobrać z { $target }!
injector-component-cannot-draw-message-self = Nie możesz pobrać z siebie!
injector-component-ignore-mobs = Ten wstrzykiwacz może wchodzić w interakcje tylko z pojemnikami!

## mob-inject doafter messages

injector-component-needle-injecting-user = Zaczynasz wykonywać zastrzyk.
injector-component-needle-injecting-target = { CAPITALIZE(THE($user)) } próbuje wykonać na tobie zastrzyk!
injector-component-needle-drawing-user = Zaczynasz wykonywać pobranie.
injector-component-needle-drawing-target = { CAPITALIZE(THE($user)) } próbuje pobrać z ciebie!
injector-component-spray-injecting-user = Zaczynasz przygotowywać dyszę aerozolową.
injector-component-spray-injecting-target = { CAPITALIZE(THE($user)) } próbuje umieścić dyszę aerozolową na tobie!

## Target Popup Success messages

injector-component-feel-prick-message = Czujesz drobne ukłucie!
injector-draw-text = Pobierz
injector-inject-text = Wstrzyknij
injector-invalid-injector-toggle-mode = Nieprawidłowy
injector-component-drawing-text = Pobieranie
injector-component-injecting-text = Wstrzykiwanie
injector-component-drawing-user = Zaczynasz pobierać igłą.
injector-component-injecting-user = Zaczynasz wstrzykiwać igłą.
injector-component-drawing-target = { CAPITALIZE($user) } próbuje pobrać krew igłą z ciebie!
injector-component-injecting-target = { CAPITALIZE($user) } próbuje wstrzyknąć ci coś igłą!
injector-component-deny-user = Egzoszkielet jest zbyt gruby!
