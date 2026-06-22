### UI

chat-manager-max-message-length = Twoja wiadomość przekracza limit { $maxMessageLength } znaków
chat-manager-ooc-chat-enabled-message = Czat OOC został włączony.
chat-manager-ooc-chat-disabled-message = Czat OOC został wyłączony.
chat-manager-looc-chat-enabled-message = Czat LOOC został włączony.
chat-manager-looc-chat-disabled-message = Czat LOOC został wyłączony.
chat-manager-dead-looc-chat-enabled-message = Martwi gracze mogą teraz używać LOOC.
chat-manager-dead-looc-chat-disabled-message = Martwi gracze nie mogą już używać LOOC.
chat-manager-crit-looc-chat-enabled-message = Gracze w stanie krytycznym mogą teraz używać LOOC.
chat-manager-crit-looc-chat-disabled-message = Gracze w stanie krytycznym nie mogą już używać LOOC.
chat-manager-admin-ooc-chat-enabled-message = Adminowy czat OOC został włączony.
chat-manager-admin-ooc-chat-disabled-message = Adminowy czat OOC został wyłączony.
chat-manager-max-message-length-exceeded-message = Twoja wiadomość przekroczyła limit { $limit } znaków
chat-manager-no-headset-on-message = Nie masz na sobie zestawu słuchawkowego!
chat-manager-no-radio-key = Nie podano klawisza radia!
chat-manager-no-such-channel = Nie ma kanału z kluczem '{ $key }'!
chat-manager-whisper-headset-on-message = Nie możesz szeptać przez radio!
chat-manager-server-wrap-message = [bold]{ $message }[/bold]
chat-manager-sender-announcement = Centralne Dowództwo
chat-manager-sender-announcement-wrap-message = [font size=14][bold]{ $sender } – ogłoszenie:[/font][font size=12]
    { $message }[/bold][/font]
chat-manager-entity-say-wrap-message = [BubbleHeader][bold][Name]{ $entityName }[/Name][/bold][/BubbleHeader] { $verb }, [font={ $fontType } size={ $fontSize }]"[BubbleContent]{ $message }[/BubbleContent]"[/font]
chat-manager-entity-say-bold-wrap-message = [BubbleHeader][bold][Name]{ $entityName }[/Name][/bold][/BubbleHeader] { $verb }, [font={ $fontType } size={ $fontSize }]"[BubbleContent][bold]{ $message }[/bold][/BubbleContent]"[/font]
chat-manager-entity-whisper-wrap-message = [font size=11][italic][BubbleHeader][Name]{ $entityName }[/Name][/BubbleHeader] szepcze, "[BubbleContent]{ $message }[/BubbleContent]"[/italic][/font]
chat-manager-entity-whisper-unknown-wrap-message = [font size=11][italic][BubbleHeader]Ktoś[/BubbleHeader] szepcze, "[BubbleContent]{ $message }[/BubbleContent]"[/italic][/font]
# THE() is not used here because the entity and its name can technically be disconnected if a nameOverride is passed...
chat-manager-entity-me-wrap-message = [italic]{ PROPER($entity) ->
       *[false] { $entityName } { $message }[/italic]
        [true] { CAPITALIZE($entityName) } { $message }[/italic]
    }
chat-manager-entity-looc-wrap-message = LOOC: [bold]{ $entityName }:[/bold] { $message }
chat-manager-send-ooc-wrap-message = OOC: [bold]{ $playerName }:[/bold] { $message }
chat-manager-send-ooc-patron-wrap-message = OOC: [bold][color={ $patronColor }]{ $playerName }[/color]:[/bold] { $message }
chat-manager-send-dead-chat-wrap-message = { $deadChannelName }: [bold][BubbleHeader]{ $playerName }[/BubbleHeader]:[/bold] [BubbleContent]{ $message }[/BubbleContent]
chat-manager-send-admin-dead-chat-wrap-message = { $adminChannelName }: [bold]([BubbleHeader]{ $userName }[/BubbleHeader]):[/bold] [BubbleContent]{ $message }[/BubbleContent]
chat-manager-send-admin-chat-wrap-message = { $adminChannelName }: [bold]{ $playerName }:[/bold] { $message }
chat-manager-send-admin-announcement-wrap-message = [bold]{ $adminChannelName }: { $message }[/bold]
chat-manager-send-hook-ooc-wrap-message = OOC: [bold](D){ $senderName }:[/bold] { $message }
chat-manager-send-hook-admin-wrap-message = ADMIN: [bold](D){ $senderName }:[/bold] { $message }
chat-manager-dead-channel-name = MARTWI
chat-manager-admin-channel-name = ADMIN
chat-manager-rate-limited = Wysyłasz wiadomości zbyt szybko!
chat-manager-rate-limit-admin-announcement = Ostrzeżenie o limicie wiadomości: { $player }

## Speech verbs for chat

chat-speech-verb-suffix-exclamation = !
chat-speech-verb-suffix-exclamation-strong = !!
chat-speech-verb-suffix-question = ?
chat-speech-verb-suffix-stutter = -
chat-speech-verb-suffix-mumble = ..
chat-speech-verb-name-none = Brak
chat-speech-verb-name-default = Domyślny
chat-speech-verb-default = mówi
chat-speech-verb-name-exclamation = Wykrzykiwanie
chat-speech-verb-exclamation = wykrzykuje
chat-speech-verb-name-exclamation-strong = Krzyk
chat-speech-verb-exclamation-strong = krzyczy
chat-speech-verb-name-question = Pytanie
chat-speech-verb-question = pyta
chat-speech-verb-name-stutter = Jąkanie
chat-speech-verb-stutter = jąka się
chat-speech-verb-name-mumble = Mamrotanie
chat-speech-verb-mumble = mamrocze
chat-speech-verb-name-arachnid = Pajęczak
chat-speech-verb-insect-1 = terkocze
chat-speech-verb-insect-2 = ćwierka
chat-speech-verb-insect-3 = klika
chat-speech-verb-name-moth = Ćma
chat-speech-verb-winged-1 = trzepocze
chat-speech-verb-winged-2 = macha skrzydłami
chat-speech-verb-winged-3 = bzyczy
chat-speech-verb-name-slime = Szlam
chat-speech-verb-slime-1 = chlupocze
chat-speech-verb-slime-2 = bulgocze
chat-speech-verb-slime-3 = sączy się
chat-speech-verb-name-plant = Diona
chat-speech-verb-plant-1 = szeleści
chat-speech-verb-plant-2 = kołysze się
chat-speech-verb-plant-3 = skrzypi
chat-speech-verb-name-robotic = Robotyczny
chat-speech-verb-robotic-1 = oznajmia
chat-speech-verb-robotic-2 = piszczy
chat-speech-verb-robotic-3 = syntezuje
chat-speech-verb-name-reptilian = Gad
chat-speech-verb-reptilian-1 = syczy
chat-speech-verb-reptilian-2 = prycha
chat-speech-verb-reptilian-3 = sapie
chat-speech-verb-name-skeleton = Szkielet
chat-speech-verb-skeleton-1 = grzechocze
chat-speech-verb-skeleton-2 = klekocze
chat-speech-verb-skeleton-3 = zgrzyta
chat-speech-verb-name-vox = Voks
chat-speech-verb-vox-1 = skrzeczy
chat-speech-verb-vox-2 = wrzeszczy
chat-speech-verb-vox-3 = kracze
chat-speech-verb-name-canine = Psowaty
chat-speech-verb-canine-1 = szczeka
chat-speech-verb-canine-2 = hauka
chat-speech-verb-canine-3 = wyje
chat-speech-verb-name-goat = Koza
chat-speech-verb-goat-1 = beczy
chat-speech-verb-goat-2 = mruczy
chat-speech-verb-goat-3 = ryczy
chat-speech-verb-name-small-mob = Mysz
chat-speech-verb-small-mob-1 = piszczy
chat-speech-verb-small-mob-2 = popiskuje
chat-speech-verb-name-large-mob = Karp
chat-speech-verb-large-mob-1 = ryczy
chat-speech-verb-large-mob-2 = warczy
chat-speech-verb-name-monkey = Małpa
chat-speech-verb-monkey-1 = popiskuje
chat-speech-verb-monkey-2 = wrzeszczy
chat-speech-verb-name-cluwne = Cluwne
chat-speech-verb-name-parrot = Papuga
chat-speech-verb-parrot-1 = skrzeczy
chat-speech-verb-parrot-2 = ćwierka
chat-speech-verb-parrot-3 = świergocze
chat-speech-verb-cluwne-1 = chichocze
chat-speech-verb-cluwne-2 = rechocze
chat-speech-verb-cluwne-3 = śmieje się
chat-speech-verb-name-ghost = Duch
chat-speech-verb-ghost-1 = narzeka
chat-speech-verb-ghost-2 = dyszy
chat-speech-verb-ghost-3 = nuci
chat-speech-verb-ghost-4 = mamrocze
chat-speech-verb-name-electricity = Elektryczność
chat-speech-verb-electricity-1 = trzaska
chat-speech-verb-electricity-2 = bzyczy
chat-speech-verb-electricity-3 = piszczy
