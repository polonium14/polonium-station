comp-kitchen-spike-begin-hook-self = You begin dragging yourself onto { THE($hook) }!
comp-kitchen-spike-begin-hook-self-other = { CAPITALIZE(THE($victim)) } begins dragging { REFLEXIVE($victim) } onto { THE($hook) }!

comp-kitchen-spike-begin-hook-other-self = You begin dragging { CAPITALIZE(THE($victim)) } onto { THE($hook) }!
comp-kitchen-spike-begin-hook-other = { CAPITALIZE(THE($user)) } begins dragging { CAPITALIZE(THE($victim)) } onto { THE($hook) }!

comp-kitchen-spike-hook-self = You threw yourself on { THE($hook) }!
comp-kitchen-spike-hook-self-other = { CAPITALIZE(THE($victim)) } threw { REFLEXIVE($victim) } on { THE($hook) }!

comp-kitchen-spike-hook-other-self = You threw { CAPITALIZE(THE($victim)) } on { THE($hook) }!
comp-kitchen-spike-hook-other = { CAPITALIZE(THE($user)) } threw { CAPITALIZE(THE($victim)) } on { THE($hook) }!

comp-kitchen-spike-begin-unhook-self = You begin dragging yourself off { THE($hook) }!
comp-kitchen-spike-begin-unhook-self-other = { CAPITALIZE(THE($victim)) } begins dragging { REFLEXIVE($victim) } off { THE($hook) }!

comp-kitchen-spike-begin-unhook-other-self = You begin dragging { CAPITALIZE(THE($victim)) } off { THE($hook) }!
comp-kitchen-spike-begin-unhook-other = { CAPITALIZE(THE($user)) } begins dragging { CAPITALIZE(THE($victim)) } off { THE($hook) }!

comp-kitchen-spike-unhook-self = You got yourself off { THE($hook) }!
comp-kitchen-spike-unhook-self-other = { CAPITALIZE(THE($victim)) } got { REFLEXIVE($victim) } off { THE($hook) }!

comp-kitchen-spike-unhook-other-self = You got { CAPITALIZE(THE($victim)) } off { THE($hook) }!
comp-kitchen-spike-unhook-other = { CAPITALIZE(THE($user)) } got { CAPITALIZE(THE($victim)) } off { THE($hook) }!

comp-kitchen-spike-begin-butcher-self = You begin butchering { THE($victim) }!
comp-kitchen-spike-begin-butcher = { CAPITALIZE(THE($user)) } begins to butcher { THE($victim) }!

comp-kitchen-spike-butcher-self = You butchered { THE($victim) }!
comp-kitchen-spike-butcher = { CAPITALIZE(THE($user)) } butchered { THE($victim) }!

comp-kitchen-spike-need-tool-quality = { $quality } tool required to butcher { THE($target) }.

comp-kitchen-spike-unhook-verb = Unhook

comp-kitchen-spike-hooked = [color=red]{ CAPITALIZE(THE($victim)) } is on this spike![/color]

comp-kitchen-spike-meat-name = { $name } ({ $victim })

comp-kitchen-spike-victim-examine = [color=orange]{ CAPITALIZE(SUBJECT($target)) } looks quite lean.[/color]

comp-kitchen-spike-deconstruct-occupied = Next, [color=red]unhook the body[/color].

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
