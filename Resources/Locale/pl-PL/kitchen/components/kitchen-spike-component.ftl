comp-kitchen-spike-deny-collect = { CAPITALIZE(THE($this)) } ma już coś na sobie, dokończ najpierw zbieranie mięsa!
comp-kitchen-spike-deny-butcher = { CAPITALIZE(THE($victim)) } nie może być {GENDER($victim) ->
        [male] rozłożony
        [female] rozłożona
        *[other] rozłożone
    } na { THE($this) }.
comp-kitchen-spike-deny-butcher-knife = { CAPITALIZE(THE($victim)) } nie może być { GENDER($victim) ->
        [male] rozłożony
        [female] rozłożona
        *[other] rozłożone
    } na { THE($this) }, potrzebujesz noża aby {OBJECT($victim)} rozłożyć
comp-kitchen-spike-deny-not-dead = { CAPITALIZE(THE($victim)) } nie może być powieszony na { THE($this) }, {$victim} nie jest {GENDER($victim) ->
        [male] martwy
        [female] martwa
        *[other] martwe
    }.
comp-kitchen-spike-begin-hook-victim = { CAPITALIZE(THE($user)) } zaczyna wieszać cię na { THE($hook) }!
comp-kitchen-spike-begin-hook-self = Zaczynasz ciągnąć się na { THE($hook) }!
comp-kitchen-spike-kill = { CAPITALIZE(THE($user)) } siłą {GENDER($user) ->
        [male] wepchnął
        [female] wepchnęła
        *[other] wepchneło
    } { THE($victim) } na { THE($this) }, zabijając {OBJECT($victim)} natychmiastowo!
comp-kitchen-spike-suicide-other = { CAPITALIZE(THE($victim)) } {GENDER($user) ->
        [male] rzucił
        [female] rzuciła
        *[other] rzuciło
    } się na { THE($this) }!
comp-kitchen-spike-suicide-self = Wieszasz się na { THE($this) }!
comp-kitchen-spike-knife-needed = Potrzebujesz noża do tego.
comp-kitchen-spike-remove-meat = Odkrajasz kawałek mięsa z { THE($victim) }.
comp-kitchen-spike-remove-meat-last = Odkrajasz ostatni kawałek mięsa z { THE($victim) }!
comp-kitchen-spike-meat-name = { $name } ({ $victim })
comp-kitchen-spike-begin-hook-self-other = { CAPITALIZE(THE($victim)) } zaczyna wieszać się na { THE($hook) }!
comp-kitchen-spike-begin-hook-other-self = Zaczynasz wieszać { CAPITALIZE(THE($victim)) } na { THE($hook) }!
comp-kitchen-spike-begin-hook-other = { CAPITALIZE(THE($user)) } zaczyna wieszać { CAPITALIZE(THE($victim)) } na { THE($hook) }!
comp-kitchen-spike-hook-self = Rzucasz się na { THE($hook) }!
comp-kitchen-spike-hook-self-other = { CAPITALIZE(THE($victim)) } { GENDER($victim) ->
        [male] powiesił
        [female] powiesiła
        *[other] powiesiło
    } się na { THE($hook) }!
comp-kitchen-spike-hook-other-self = Wieszasz { CAPITALIZE(THE($victim)) } na { THE($hook) }!
comp-kitchen-spike-hook-other = { CAPITALIZE(THE($user)) } { GENDER($user) ->
        [male] powiesił
        [female] powiesiła
        *[other] powiesiło
    } { CAPITALIZE(THE($victim)) } na { THE($hook) }!
comp-kitchen-spike-begin-unhook-self = Zaczynasz zciągać się z { THE($hook) }!
comp-kitchen-spike-begin-unhook-self-other = { CAPITALIZE(THE($victim)) } zaczyna zciągać się z { THE($hook) }!
comp-kitchen-spike-begin-unhook-other-self = Zaczynasz zciągać { CAPITALIZE(THE($victim)) } z { THE($hook) }!
comp-kitchen-spike-begin-unhook-other = { CAPITALIZE(THE($user)) } zaczyna zciągać { CAPITALIZE(THE($victim)) } z { THE($hook) }!
comp-kitchen-spike-unhook-self = { GENDER($victim) ->
        [male] Zciągnąłeś
        [female] Zciągnełaś
        *[other] Zciągnołoś
    } się z { THE($hook) }!
comp-kitchen-spike-unhook-self-other = { CAPITALIZE(THE($victim)) } {GENDER($victim) ->
        [male] zciągnął
        [female] zciągneła
        *[other] zciągnoło
    } się z { THE($hook) }!
comp-kitchen-spike-unhook-other-self = { GENDER($victim) ->
        [male] Zciągnąłeś
        [female] Zciągnełaś
        *[other] Zciągnąłoś
    } {CAPITALIZE(THE($victim))} z { THE($hook) }!
comp-kitchen-spike-unhook-other = { CAPITALIZE(THE($user)) } { GENDER($victim) ->
        [male] zciągnął
        [female] zciągneła
        *[other] zciągneło
    } {CAPITALIZE(THE($victim))} z { THE($hook) }!
comp-kitchen-spike-begin-butcher-self = Zaczynasz rozkładać { THE($victim) }!
comp-kitchen-spike-begin-butcher = { CAPITALIZE(THE($user)) } zaczyna rozkładać { THE($victim) }!
comp-kitchen-spike-butcher-self = {GENDER($user) ->
        [male] Rozłożyłeś
        [female] Rozłożyłaś
        *[other] Rozłożyłoś
    } { THE($victim) }!
comp-kitchen-spike-butcher = { CAPITALIZE(THE($user)) } {GENDER($user) ->
        [male] rozłożył
        [female] rozłożyła
        *[other] rozłożyło
    } { THE($victim) }!
comp-kitchen-spike-unhook-verb = Zciągnij
comp-kitchen-spike-hooked = [color=red]{ CAPITALIZE(THE($victim)) } jest na tym kolcu![/color]
comp-kitchen-spike-victim-examine = [color=orange]{ CAPITALIZE(SUBJECT($target)) } wygląda chudo.[/color]
