comp-kitchen-spike-deny-collect = { CAPITALIZE($this) } ma już coś na sobie, dokończ najpierw zbieranie mięsa!
comp-kitchen-spike-deny-butcher =
    { CAPITALIZE($victim) } nie może być { GENDER($victim) ->
        [male] rozłożony
        [female] rozłożona
        [epicene] rozłożonu
       *[neuter] rozłożone
    } na { $this }.
comp-kitchen-spike-deny-butcher-knife =
    { CAPITALIZE($victim) } nie może być { GENDER($victim) ->
        [male] rozłożony
        [female] rozłożona
        [epicene] rozłożonu
       *[neuter] rozłożone
    } na { $this }, potrzebujesz noża aby { OBJECT($victim) } rozłożyć
comp-kitchen-spike-deny-changeling = { CAPITALIZE($victim) } resists being put on { $this }.
comp-kitchen-spike-deny-absorbed = { CAPITALIZE($victim) } has nothing left to butcher.
comp-kitchen-spike-deny-not-dead =
    { CAPITALIZE($victim) } nie może być powieszony na { $this }, { $victim } nie jest { GENDER($victim) ->
        [male] martwy
        [female] martwa
        [epicene] martwu
       *[neuter] martwe
    }.
comp-kitchen-spike-begin-hook-victim = { CAPITALIZE($user) } zaczyna wieszać cię na { $hook }!
comp-kitchen-spike-begin-hook-self = Zaczynasz ciągnąć się na { $hook }!
comp-kitchen-spike-kill =
    { CAPITALIZE($user) } siłą { GENDER($user) ->
        [male] wepchnął
        [female] wepchnęła
        [epicene] wepchnęłu
       *[neuter] wepchneło
    } { $victim } na { $this }, zabijając { OBJECT($victim) } natychmiastowo!
comp-kitchen-spike-suicide-other =
    { CAPITALIZE($victim) } { GENDER($user) ->
        [male] rzucił
        [female] rzuciła
        [epicene] rzuciłu
       *[neuter] rzuciło
    } się na { $this }!
comp-kitchen-spike-suicide-self = Wieszasz się na { $this }!
comp-kitchen-spike-knife-needed = Potrzebujesz noża do tego.
comp-kitchen-spike-remove-meat = Odkrajasz kawałek mięsa z { $victim }.
comp-kitchen-spike-remove-meat-last = Odkrajasz ostatni kawałek mięsa z { $victim }!
comp-kitchen-spike-meat-name = { $name } ({ $victim })
comp-kitchen-spike-begin-hook-self-other = { CAPITALIZE($victim) } zaczyna wieszać się na { $hook }!
comp-kitchen-spike-begin-hook-other-self = Zaczynasz wieszać { CAPITALIZE($victim) } na { $hook }!
comp-kitchen-spike-begin-hook-other = { CAPITALIZE($user) } zaczyna wieszać { CAPITALIZE($victim) } na { $hook }!
comp-kitchen-spike-hook-self = Rzucasz się na { $hook }!
comp-kitchen-spike-hook-self-other =
    { CAPITALIZE($victim) } { GENDER($victim) ->
        [male] powiesił
        [female] powiesiła
        [epicene] powiesiłu
       *[neuter] powiesiło
    } się na { $hook }!
comp-kitchen-spike-hook-other-self = Wieszasz { CAPITALIZE($victim) } na { $hook }!
comp-kitchen-spike-hook-other =
    { CAPITALIZE($user) } { GENDER($user) ->
        [male] powiesił
        [female] powiesiła
        [epicene] powiesiłu
       *[neuter] powiesiło
    } { CAPITALIZE($victim) } na { $hook }!
comp-kitchen-spike-begin-unhook-self = Zaczynasz zciągać się z { $hook }!
comp-kitchen-spike-begin-unhook-self-other = { CAPITALIZE($victim) } zaczyna zciągać się z { $hook }!
comp-kitchen-spike-begin-unhook-other-self = Zaczynasz zciągać { CAPITALIZE($victim) } z { $hook }!
comp-kitchen-spike-begin-unhook-other = { CAPITALIZE($user) } zaczyna zciągać { CAPITALIZE($victim) } z { $hook }!
comp-kitchen-spike-unhook-self =
    { GENDER($victim) ->
        [male] Zciągnąłeś
        [female] Zciągnełaś
        [epicene] Zciągnełuś
       *[neuter] Zciągnołoś
    } się z { $hook }!
comp-kitchen-spike-unhook-self-other =
    { CAPITALIZE($victim) } { GENDER($victim) ->
        [male] zciągnął
        [female] zciągneła
        [epicene] zciągnełu
       *[neuter] zciągnoło
    } się z { $hook }!
comp-kitchen-spike-unhook-other-self =
    { GENDER($victim) ->
        [male] Zciągnąłeś
        [female] Zciągnełaś
        [epicene] Zciągnełuś
       *[neuter] Zciągnąłoś
    } { CAPITALIZE($victim) } z { $hook }!
comp-kitchen-spike-unhook-other =
    { CAPITALIZE($user) } { GENDER($victim) ->
        [male] zciągnął
        [female] zciągneła
        [epicene] zciągnełu
       *[neuter] zciągneło
    } { CAPITALIZE($victim) } z { $hook }!
comp-kitchen-spike-begin-butcher-self = Zaczynasz rozkładać { $victim }!
comp-kitchen-spike-begin-butcher = { CAPITALIZE($user) } zaczyna rozkładać { $victim }!
comp-kitchen-spike-butcher-self =
    { GENDER($user) ->
        [male] Rozłożyłeś
        [female] Rozłożyłaś
        [epicene] Rozłożyłuś
       *[neuter] Rozłożyłoś
    } { $victim }!
comp-kitchen-spike-butcher =
    { CAPITALIZE($user) } { GENDER($user) ->
        [male] rozłożył
        [female] rozłożyła
        [epicene] rozłożyłu
       *[neuter] rozłożyło
    } { $victim }!
comp-kitchen-spike-unhook-verb = Zciągnij
comp-kitchen-spike-hooked = [color=red]{ CAPITALIZE($victim) } jest na tym kolcu![/color]
comp-kitchen-spike-victim-examine = [color=orange]{ CAPITALIZE(SUBJECT($target)) } wygląda chudo.[/color]
