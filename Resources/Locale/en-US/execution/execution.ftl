execution-verb-name = Execute
execution-verb-message = Use your weapon to execute someone.

# All the below localisation strings have access to the following variables
# attacker (the person committing the execution)
# victim (the person being executed)
# weapon (the weapon used for the execution)

execution-popup-melee-initial-internal = You ready {THE($weapon)} against {THE($victim)}'s throat.
execution-popup-melee-initial-external = { CAPITALIZE(THE($attacker)) } readies {POSS-ADJ($attacker)} {$weapon} against the throat of {THE($victim)}.
execution-popup-melee-complete-internal = You slit the throat of {THE($victim)}!
execution-popup-melee-complete-external = { CAPITALIZE(THE($attacker)) } slits the throat of {THE($victim)}!

execution-popup-self-initial-internal = You ready {THE($weapon)} against your own throat.
execution-popup-self-initial-external = { CAPITALIZE(THE($attacker)) } readies {POSS-ADJ($attacker)} {$weapon} against their own throat.
execution-popup-self-complete-internal = You slit your own throat!
execution-popup-self-complete-external = { CAPITALIZE(THE($attacker)) } slits their own throat!

gun-execution-initial-self = You press {THE($weapon)} against {THE($victim)}'s head.
gun-execution-initial-others = { CAPITALIZE(THE($attacker)) } presses {POSS-ADJ($attacker)} {$weapon} against {THE($victim)}'s head.
gun-execution-complete-self = You pull the trigger and execute {THE($victim)}!
gun-execution-complete-others = { CAPITALIZE(THE($attacker)) } pulls the trigger and executes {THE($victim)}!
gun-execution-suicide-initial-self = You put {THE($weapon)} under your chin.
gun-execution-suicide-initial-others = { CAPITALIZE(THE($attacker)) } puts {POSS-ADJ($attacker)} {$weapon} under their chin.
gun-execution-suicide-complete-self = You pull the trigger!
gun-execution-suicide-complete-others = { CAPITALIZE(THE($attacker)) } pulls the trigger!
gun-execution-empty-self = {THE($weapon)} clicks. It's empty.
gun-execution-empty-others = { CAPITALIZE(THE($attacker)) }'s {$weapon} clicks. It's empty.
