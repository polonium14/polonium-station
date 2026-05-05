# SPDX-FileCopyrightText: 2022 EmoGarbage404 <98561806+EmoGarbage404@users.noreply.github.com>
# SPDX-FileCopyrightText: 2022 Marat Gadzhiev <15rinkashikachi15@gmail.com>
# SPDX-FileCopyrightText: 2023 Kara <lunarautomaton6@gmail.com>
# SPDX-FileCopyrightText: 2024 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 icekot8 <93311212+icekot8@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 lzk <124214523+lzk228@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 Tay <td12233a@gmail.com>
# SPDX-FileCopyrightText: 2025 jackel234 <52829582+jackel234@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 pa.pecherskij <pa.pecherskij@interfax.ru>
# SPDX-FileCopyrightText: 2025 pathetic meowmeow <uhhadd@gmail.com>
# SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
#
# SPDX-License-Identifier: MIT


## UI

cargo-console-menu-title = Konsola zamówień logistycznych
cargo-console-menu-account-name-label = Konto:{ " " }
cargo-console-menu-account-name-none-text = Żadne
cargo-console-menu-account-name-format = [bold][color={ $color }]{ $name }[/color][/bold] [font="Monospace"]\[{ $code }\][/font]
cargo-console-menu-shuttle-name-label = Nazwa wahadłowca:{ " " }
cargo-console-menu-shuttle-name-none-text = Żadna
cargo-console-menu-points-label = Balans:{ " " }
cargo-console-menu-points-amount = ${ $amount }
cargo-console-menu-shuttle-status-label = Stan wahadłowca:{ " " }
cargo-console-menu-shuttle-status-away-text = Oddalony
cargo-console-menu-order-capacity-label = Pojemność zamówień:{ " " }
cargo-console-menu-call-shuttle-button = Aktywuj platformę teleportującą
cargo-console-menu-permissions-button = Uprawnienia
cargo-console-menu-categories-label = Kategorie:{ " " }
cargo-console-menu-search-bar-placeholder = Szukaj
cargo-console-menu-requests-label = Prośby
cargo-console-menu-orders-label = Zamówienia
cargo-console-menu-order-reason-description = Powód: { $reason }
cargo-console-menu-populate-categories-all-text = Wszystko
cargo-console-menu-populate-orders-cargo-order-row-product-name-text = { $productName } (x{ $orderAmount }) przez { $orderRequester } z [color={ $accountColor }]{ $account }[/color]
cargo-console-menu-cargo-order-row-approve-button = Akceptuj
cargo-console-menu-cargo-order-row-cancel-button = Anuluj
cargo-console-menu-tab-title-orders = Zamówienia
cargo-console-menu-tab-title-funds = Transfery
cargo-console-menu-account-action-transfer-limit = [bold]Limit transferu:[/bold] ${ $limit }
cargo-console-menu-account-action-transfer-limit-unlimited-notifier = [color=gold](Nielimitowany)[/color]
cargo-console-menu-account-action-select = [bold]Operacja konta:[/bold]
cargo-console-menu-account-action-amount = [bold]Ilość:[/bold] $
cargo-console-menu-account-action-button = Transferuj
cargo-console-menu-toggle-account-lock-button = Przełącz limit transferu
cargo-console-menu-account-action-option-withdraw = Wypłać
cargo-console-menu-account-action-option-transfer = Transfer środków do { $code }
# Orders
cargo-console-order-not-allowed = Brak uprawnień
cargo-console-station-not-found = Stacja niedostępna
cargo-console-invalid-product = Niewłaściwy identyfikator produktu
cargo-console-too-many = Zbyt dużo zaakceptowanych zamówień
cargo-console-snip-snip = Zamówienie zmniejszone do pojemności
cargo-console-insufficient-funds = Brak środków (wymaga { $cost })
cargo-console-unfulfilled = Brak miejsca do realizacji zamówienia
cargo-console-trade-station = Wysłane do { $destination }
cargo-console-unlock-approved-order-broadcast = [bold]{ $productName } x{ $orderAmount }[/bold], kosztujące [bold]{ $cost }[/bold], zostało zaakceptowane przez [bold]{ $approver }[/bold]
cargo-console-fund-withdraw-broadcast = [bold]{ $name } withdrew { $amount } spesos from { $name1 } \[{ $code1 }\]
cargo-console-fund-transfer-broadcast = [bold]{ $name } transferred { $amount } spesos from { $name1 } \[{ $code1 }\] to { $name2 } \[{ $code2 }\][/bold]
cargo-console-fund-transfer-user-unknown = Nieznany
cargo-console-paper-reason-default = Brak
cargo-console-paper-approver-default = Własne
cargo-console-paper-print-name = Zamówienie #{ $orderNumber }
cargo-console-paper-print-text = [head=2]Zamówienie #{ $orderNumber }[/head]
    { "[bold]Przedmiot:[/bold]" } { $itemName } (x{ $orderQuantity })
    { "[bold]Zamówiony przez:[/bold]" } { $requester }
    
    { "[head=3]Informacja o zamówieniu[/head]" }
    { "[bold]Płatnik[/bold]:" } { $account } [font="Monospace"]\[{ $accountcode }\][/font]
    { "[bold]Akceptowane przez:[/bold]" } { $approver }
    { "[bold]Powód:[/bold]" } { $reason }
# Cargo shuttle console
cargo-shuttle-console-menu-title = Konsola wahadłowca logistycznego
cargo-shuttle-console-station-unknown = Nieznana
cargo-shuttle-console-shuttle-not-found = Nie znaleziono
cargo-shuttle-console-organics = Wykryto organiczne formy życia na wahadłowcu
cargo-no-shuttle = Nie znaleziono wahadłowca!
# Funding allocation console
cargo-funding-alloc-console-menu-title = Konsola rozdziału funduszy
cargo-funding-alloc-console-label-account = [bold]Konto[/bold]
cargo-funding-alloc-console-label-code = [bold] Kod [/bold]
cargo-funding-alloc-console-label-balance = [bold] Balans [/bold]
cargo-funding-alloc-console-label-primary-cut = Udział logistyki w zyskach ze źródeł otwartych (%):
cargo-funding-alloc-console-label-cut = [bold] Podział zysku (%) [/bold]
cargo-funding-alloc-console-label-lockbox-cut = Udział logistyki w zyskach ze sprzedaży skrzyń zamkniętych (%):
cargo-funding-alloc-console-label-help-non-adjustible = Logistyka otrzymuje { NATURALPERCENT($percent) } zysków ze sprzedaży skrzyń otwartych. Reszta jest rozdzielona jak niżej:
cargo-funding-alloc-console-label-help-adjustible = Pozostałe zyski ze źródeł otwartych są rozdzielane jak niżej:
cargo-funding-alloc-console-label-help = Logistyka otrzymuje { $percent }% wszystkich zysków. Reszta jest rozdzielana jak wskazano poniżej:
cargo-funding-alloc-console-button-save = Zapisz zmiany
cargo-funding-alloc-console-label-save-fail = [bold]Revenue Divisions Invalid![/bold] [color=red]({ $pos ->
        [1] +
       *[-1] -
    }{ $val }%)[/color]
# Slip template
cargo-acquisition-slip-body = [head=3]Szczegóły mienia[/head]
    { "[bold]Produkt:[/bold]" } { $product }
    { "[bold]Opis:[/bold]" } { $description }
    { "[bold]Koszt jednostkowy:[/bold" }] ${ $unit }
    { "[bold]Liczba:[/bold]" } { $amount }
    { "[bold]Koszt:[/bold]" } ${ $cost }
    
    { "[head=3]Szczegóły zakupu[/head]" }
    { "[bold]Zamawiający:[/bold]" } { $orderer }
    { "[bold]Powód:[/bold]" } { $reason }
