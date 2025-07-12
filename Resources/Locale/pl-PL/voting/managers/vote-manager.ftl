# Displayed as initiator of vote when no user creates the vote
ui-vote-initiator-server = Serwer

## Default.Votes

ui-vote-restart-title = Restart rundy
ui-vote-restart-succeeded = Głosowanie na restart powiodło się.
ui-vote-restart-failed = Głosowanie za restartem nie powiodło się (wymagane { TOSTRING($ratio, "P0") }).
ui-vote-restart-fail-not-enough-ghost-players = Głosowanie za restartem nie powiodło się: wymagane jest co najmniej { $ghostPlayerRequirement }% martwych graczy, aby rozpocząć głosowanie. Obecnie jest ich za mało.
ui-vote-restart-yes = Tak
ui-vote-restart-no = Nie
ui-vote-restart-abstain = Wtrzymuję się

ui-vote-gamemode-title = Następny tryb
ui-vote-gamemode-tie = Remis w głosowaniu za trybem gry! Wybieramy... { $picked }
ui-vote-gamemode-win = { $winner } wygrał głosowanie za trybem gry!

ui-vote-map-title = Następna mapa
ui-vote-map-tie = Remis w głosowaniu na mapę! Wybieramy... { $picked }
ui-vote-map-win = { $winner } wygrała głosowanie na mapę!
ui-vote-map-notlobby = Głosowanie za mapą jest ważne tylko w lobby przedrundowym!
ui-vote-map-notlobby-time = Głosowanie za mapą jest ważne tylko w lobby przed rundą, w której pozostało { $time }!


# Votekick votes
ui-vote-votekick-unknown-initiator = Gracz
ui-vote-votekick-unknown-target = Nieznany Gracz
ui-vote-votekick-title = { $initiator } rozpoczął votekick dla użytkownika: { $targetEntity }. Powód: { $powód }
ui-vote-votekick-yes = Tak
ui-vote-votekick-no = Nie
ui-vote-votekick-abstain = Wstrzymuję się
ui-vote-votekick-success = Votekick gracza { $target } powiódł się. Powód votekick'a: { $reason }
ui-vote-votekick-failure = Votekick gracza { $target } nie powiódł się. Powód votekick'a: { $reason }
ui-vote-votekick-not-enough-eligible = Niewystarczająco upoważnionych aktywnych graczy, aby rozpocząć głosowanie: { $voters }/{ $requirement }
ui-vote-votekick-server-cancelled = Votekick gracza { $target } został anulowane przez serwer.
