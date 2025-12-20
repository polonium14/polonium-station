-entity-heater-setting-name =
    { $setting ->
        [off] wył.
        [low] niski
        [medium] średni
        [high] wysoki
       *[other] nieznane
    }
-entity-heater-setting-color =
    { $setting ->
        [off] gray
        [low] yellow
        [medium] orange
        [high] red
       *[other] purple
    }
entity-heater-examined = Obecny tryb [color={ -entity-heater-setting-color(setting: "{$setting}") }]{ -entity-heater-setting-name(setting: "{$setting}") }[/color].
entity-heater-switch-setting = Przełącz na { -entity-heater-setting-name(setting: $setting) }
entity-heater-switched-setting = Przełączono na { -entity-heater-setting-name(setting: $setting) }.
