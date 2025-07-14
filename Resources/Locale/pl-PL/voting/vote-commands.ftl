### Voting system related console commands

## 'createvote' command

cmd-createvote-desc = Tworzy głosowanie
cmd-createvote-help = Użycie: createvote <'restart'|'preset'|'map'>
cmd-createvote-cannot-call-vote-now = Nie możesz teraz rozpocząć głosowania!
cmd-createvote-invalid-vote-type = Nieprawidłowy typ głosowania
cmd-createvote-arg-vote-type = <typ głosowania>

## 'customvote' command

cmd-customvote-desc = Tworzy niestandardowe głosowanie
cmd-customvote-help = Użycie: customvote <tytuł> <opcja1> <opcja2> [opcja3...]
cmd-customvote-on-finished-tie = Remis pomiędzy: {$ties}!
cmd-customvote-on-finished-win = Wygrywa {$winner}!
cmd-customvote-arg-title = <tytuł>
cmd-customvote-arg-option-n = <opcja{ $n }>

## 'vote' command

cmd-vote-desc = Oddaje głos w aktywnym głosowaniu
cmd-vote-help = Użycie: vote <ID głosowania> <opcja>
cmd-vote-cannot-call-vote-now = Nie możesz teraz rozpocząć głosowania!
cmd-vote-on-execute-error-must-be-player = Musisz być graczem
cmd-vote-on-execute-error-invalid-vote-id = Nieprawidłowe ID głosowania
cmd-vote-on-execute-error-invalid-vote-options = Nieprawidłowe opcje głosowania
cmd-vote-on-execute-error-invalid-vote = Nieprawidłowe głosowanie
cmd-vote-on-execute-error-invalid-option = Nieprawidłowa opcja

## 'listvotes' command

cmd-listvotes-desc = Wyświetla aktualnie aktywne głosowania
cmd-listvotes-help = Użycie: listvotes

## 'cancelvote' command

cmd-cancelvote-desc = Anuluje aktywne głosowanie
cmd-cancelvote-help = Użycie: cancelvote <id>
                      ID można uzyskać za pomocą komendy listvotes.
cmd-cancelvote-error-invalid-vote-id = Nieprawidłowe ID głosowania
cmd-cancelvote-error-missing-vote-id = Brak ID
cmd-cancelvote-arg-id = <id>
