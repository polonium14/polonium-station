
comp-kitchen-spike-deny-collect = { CAPITALIZE(THE($this)) } already has something on it, finish collecting its meat first!
comp-kitchen-spike-deny-butcher = { CAPITALIZE(THE($victim)) } can't be butchered on { THE($this) }.
comp-kitchen-spike-deny-changeling = { CAPITALIZE(THE($victim)) } resists being put on { THE($this) }.
comp-kitchen-spike-deny-absorbed = { CAPITALIZE(THE($victim)) } has nothing left to butcher.
comp-kitchen-spike-deny-butcher-knife = { CAPITALIZE(THE($victim)) } can't be butchered on { THE($this) }, you need to butcher it using a knife.
comp-kitchen-spike-deny-not-dead = { CAPITALIZE(THE($victim)) } can't be butchered. { CAPITALIZE(SUBJECT($victim)) } { CONJUGATE-BE($victim) } is not dead!
comp-kitchen-spike-begin-hook-victim = { CAPITALIZE(THE($user)) } begins dragging you onto { THE($this) }!
comp-kitchen-spike-begin-hook-self = You begin dragging yourself onto { THE($this) }!
comp-kitchen-spike-kill = { CAPITALIZE(THE($user)) } has forced { THE($victim) } onto the spike, killing them instantly!
comp-kitchen-spike-suicide-other = { CAPITALIZE(THE($victim)) } has thrown themselves on a meat spike!
comp-kitchen-spike-suicide-self = You throw yourself on a meat spike!
comp-kitchen-spike-knife-needed = You need a knife to do this.
comp-kitchen-spike-remove-meat = You remove some meat from { THE($victim) }.
comp-kitchen-spike-remove-meat-last = You remove the last piece of meat from { THE($victim) }!
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
comp-kitchen-spike-hooked =  [color=red]{ CAPITALIZE($victim) } jest na tym kolcu![/color]
comp-kitchen-spike-victim-examine =  [color=orange]{ CAPITALIZE(SUBJECT($target)) } wygląda chudo.[/color]
comp-kitchen-spike-meat-name = { $name } ({ $victim })
