delivery-recipient-examine = Adresat: { $recipient }, { $job }.
delivery-already-opened-examine = Przesyłka została już otwarta.
delivery-earnings-examine = Dostarczenie tej przesyłki przyniesie stacji [color=yellow]{ $spesos }[/color] spesos.
delivery-recipient-no-name = Bez nazwiska
delivery-recipient-no-job = Nieznane
delivery-unlocked-self = Odblokowujesz odciskiem palca: { $delivery }.
delivery-opened-self = Otwierasz: { $delivery }.
delivery-unlocked-others = { CAPITALIZE($recipient) } odblokowuje odciskiem palca: { $delivery }.
delivery-opened-others = { CAPITALIZE($recipient) } otwiera: { $delivery }.
delivery-unlock-verb = Odblokuj
delivery-open-verb = Otwórz
delivery-slice-verb = Rozetnij
delivery-teleporter-amount-examine =
    { $amount ->
        [one] Zawiera [color=yellow]{ $amount }[/color] przesyłkę.
        [few] Zawiera [color=yellow]{ $amount }[/color] przesyłki.
       *[many] Zawiera [color=yellow]{ $amount }[/color] przesyłek.
    }
delivery-teleporter-empty = { CAPITALIZE($entity) } jest pusty.
delivery-teleporter-empty-verb = Zabierz pocztę
# modifiers
delivery-priority-examine = [color=orange]PRIORYTET[/color] - { $type }. Masz jeszcze [color=orange]{ $time }[/color] czasu na dostarczenie, aby otrzymać bonus.
delivery-priority-delivered-examine = [color=orange]PRIORYTET[/color] - { $type }. Dostarczono na czas.
delivery-priority-expired-examine = [color=orange]PRIORYTET[/color] - { $type }. Czas minął.
delivery-fragile-examine = [color=red]OSTROŻNIE[/color] - { $type }. Dostarcz w całości, aby otrzymać bonus.
delivery-fragile-broken-examine = [color=red]OSTROŻNIE[/color] - { $type }. Widoczne są poważne uszkodzenia.
delivery-bomb-examine = [color=purple]BOMBA[/color] - { $type }. O nie.
delivery-bomb-primed-examine = [color=purple]BOMBA[/color] - { $type }. Czytanie tego to strata czasu.
