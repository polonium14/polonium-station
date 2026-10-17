execution-verb-name = Zabij
execution-verb-message = Użyj swojej broni aby kogoś zabić.

# All the below localisation strings have access to the following variables
# attacker (the person committing the execution)
# victim (the person being executed)
# weapon (the weapon used for the execution)

execution-popup-melee-initial-internal = Zbliżasz { $weapon } do gardła { $victim }.
execution-popup-melee-initial-external = { CAPITALIZE($attacker) } zbliża { POSS-ADJ($attacker) } { $weapon } do gardła { $victim }.
execution-popup-melee-complete-internal = Podżynasz gardło { $victim }!
execution-popup-melee-complete-external = { CAPITALIZE($attacker) } podżyna gardło { $victim }!
execution-popup-self-initial-internal = Zbliżasz { $weapon } do swojego gardła.
execution-popup-self-initial-external = { CAPITALIZE($attacker) } zbliża { POSS-ADJ($attacker) } { $weapon } do swojego dardła.
execution-popup-self-complete-internal = Podżynasz sobie gardło!
execution-popup-self-complete-external = { CAPITALIZE($attacker) } podżyna swoje gardło!
gun-execution-initial-self = You press { THE($weapon) } against { THE($victim) }'s head.
gun-execution-initial-others = { CAPITALIZE(THE($attacker)) } presses { POSS-ADJ($attacker) } { $weapon } against { THE($victim) }'s head.
gun-execution-complete-self = You pull the trigger and execute { THE($victim) }!
gun-execution-complete-others = { CAPITALIZE(THE($attacker)) } pulls the trigger and executes { THE($victim) }!
gun-execution-suicide-initial-self = You put { THE($weapon) } under your chin.
gun-execution-suicide-initial-others = { CAPITALIZE(THE($attacker)) } puts { POSS-ADJ($attacker) } { $weapon } under their chin.
gun-execution-suicide-complete-self = You pull the trigger!
gun-execution-suicide-complete-others = { CAPITALIZE(THE($attacker)) } pulls the trigger!
gun-execution-empty-self = { THE($weapon) } clicks. It's empty.
gun-execution-empty-others = { CAPITALIZE(THE($attacker)) }'s { $weapon } clicks. It's empty.
