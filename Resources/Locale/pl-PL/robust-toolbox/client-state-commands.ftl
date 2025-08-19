# Loc strings for various entity state & client-side PVS related commands

cmd-reset-ent-help = Użycie: resetent <Entity UID>
cmd-reset-ent-desc = Resetuje jednostkę do ostatniego stanu otrzymanego z serwera. To również zresetuje jednostki, które zostały odłączone do "null-space".

cmd-reset-all-ents-help = Użycie: resetallents
cmd-reset-all-ents-desc = Resetuje wszystkie jednostki do ostatniego stanu otrzymanego z serwera. Dotyczy tylko jednostek, które nie zostały odłączone do "null-space".

cmd-detach-ent-help = Użycie: detachent <Entity UID>
cmd-detach-ent-desc = Odłącza jednostkę do "null-space", tak jakby opuściła zasięg PVS.

cmd-local-delete-help = Użycie: localdelete <Entity UID>
cmd-local-delete-desc = Usuwa jednostkę po stronie klienta. W przeciwieństwie do standardowej komendy delete, działa tylko po stronie klienta. Jeśli jednostka nie jest jednostką klienta, może powodować błędy.

cmd-full-state-reset-help = Użycie: fullstatereset
cmd-full-state-reset-desc = Usuwa wszystkie informacje o stanie jednostek i żąda pełnego stanu od serwera.
