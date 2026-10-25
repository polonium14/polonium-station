---
applyTo: "Resources/Prototypes/**"
---

# Prototypy YAML w Polonium Station

- Prototypy Polonium są w `Resources/Prototypes/_Polonium/`. Prototyp z upstreamu rozszerza się własnym (przez `parent`). Niezbędna zmiana w pliku upstreamu ma komentarz `# Polonium:` z powodem.
- Kolejność pól: `type`, `abstract`, `parent`, `id`, `categories`, `name`, `suffix`, `description`, `components`. ID w PascalCase, pola w camelCase, bez ID w stylu `prefiks.Nazwa`.
- `name`, `description` i `suffix` encji są po angielsku, a polski tekst jest w `Resources/Locale/pl-PL/prototypes/generated/`. Inne teksty dla gracza są kluczami FTL.

Zgłaszaj:

- usunięty lub przemianowany prototyp encji bez wpisu w `Resources/migration.yml`,
- pola, których nie czyta żaden system,
- nowe tagi tam, gdzie wystarczy rozpoznać encję po komponencie, i tagi bez prototypu `Tag`,
- tekstury w prototypach abstrakcyjnych,
- logikę budowaną w YAML z warunków i efektów zamiast systemu w C#,
- zawartość pojemników, która się w nich nie mieści,
- ceny w cargo i nagrody za zlecenia, które pozwalają zarabiać na arbitrażu,
- zmiany wartości (obrażenia, pancerz, prędkość, ceny, cooldowny) bez uzasadnienia liczbami w opisie PR,
- nową regułę gry, której nie ma w tabelach trybów (np. `Resources/Prototypes/_Polonium/GameRules/secretpolonium.yml`),
- nowego antagonistę, rolę albo maszynę bez opisu w podręczniku (`Resources/ServerInfo/_Polonium/Guidebook/`) i odczynnik z grupą (`group`), której podręcznik nie wyświetla.
