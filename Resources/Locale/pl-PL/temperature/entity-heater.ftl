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
entity-heater-examined = Obecny tryb { $setting ->
    [off] [color=gray]{ -entity-heater-setting-name(setting: "off") }[/color]
    [low] [color=yellow]{ -entity-heater-setting-name(setting: "low") }[/color]
    [medium] [color=orange]{ -entity-heater-setting-name(setting: "medium") }[/color]
    [high] [color=red]{ -entity-heater-setting-name(setting: "high") }[/color]
   *[other] [color=purple]{ -entity-heater-setting-name(setting: "other") }[/color]
}.
entity-heater-switch-setting = Przełącz na { -entity-heater-setting-name(setting: $setting) }
entity-heater-switched-setting = Przełączono na { -entity-heater-setting-name(setting: $setting) }.
