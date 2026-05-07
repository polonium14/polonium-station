-entity-heater-setting-name =
    { $setting ->
        [off] wył.
        [low] niski
        [medium] średni
        [high] wysoki
       *[other] nieznane
    }
entity-heater-switch-setting = Switch to { -entity-heater-setting-name(setting: $setting) }
entity-heater-switched-setting = Switched to { -entity-heater-setting-name(setting: $setting) }.
