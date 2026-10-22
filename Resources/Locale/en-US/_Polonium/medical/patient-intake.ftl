## Machine

patient-intake-verb-on = Switch on
patient-intake-verb-off = Switch off
patient-intake-verb-missing = Mark patient as missing

patient-intake-popup-on = Bluespace NFZ hums to life.
patient-intake-popup-off = Bluespace NFZ powers down.
patient-intake-popup-access = Access denied! Switching it on needs medical access.
patient-intake-popup-missing-access = Access denied! Only the Chief Medical Officer or the Captain can do that.
patient-intake-popup-mind = The tray won't close! A crew member is lying on it.
patient-intake-popup-no-patient = The tray won't close! Only the issued patient's body may go in.
patient-intake-popup-locked = The tray stays open for another {$seconds} { $seconds ->
    [one] second
   *[other] seconds
}.

patient-intake-examine-running = It is [color=green]accepting cases[/color].
patient-intake-examine-stopped = It is [color=red]not accepting cases[/color].
patient-intake-examine-case = Active case: {$case}.
patient-intake-examine-time = About {$minutes} min left on the clock.
patient-intake-examine-overdue = [color=red]The time limit has passed.[/color]
patient-intake-examine-clock-waiting = The patient is in stasis. The clock starts when the tray is opened.

patient-intake-examine-fit = [color=green]Fit for discharge.[/color]
patient-intake-examine-unfit = [color=red]Not fit for discharge:[/color]
patient-intake-examine-line = - {$line}

patient-intake-radio-arrived = Bluespace NFZ: {$name} has arrived ({$case}{ $total ->
    [1] {""}
   *[other] , patient {$patient} of {$total}
}).
patient-intake-radio-finished = Bluespace NFZ: case ({$case}) closed! {$amount} spesos transferred to the medical budget.
patient-intake-radio-finished-debit = Bluespace NFZ: case ({$case}) closed, {$amount} spesos charged to the medical budget.

patient-intake-round-end-header = [bold]Bluespace NFZ[/bold]
patient-intake-round-end-line = - Bluespace NFZ™ cases closed: {$cases}! Earned {$amount} spesos
patient-intake-round-end-line-loss = - Bluespace NFZ™ cases closed: {$cases}. The medical budget lost {$amount} spesos

## Case kinds

patient-intake-case-easy = Routine
patient-intake-case-medium = Standard
patient-intake-case-hard = Complicated
patient-intake-case-timed = Urgent order
patient-intake-case-super-hard = Critical order
patient-intake-case-mass-casualty = Mass casualty
patient-intake-case-request = Patient request
patient-intake-case-carp-attack = Space carp attack
patient-intake-case-burn-ward = Burn ward
patient-intake-case-workplace-accident = Workplace accident

## Case file

patient-intake-file-name = patient file: {$name}
patient-intake-doc-header = { "[color=#50afbf][mono]█▄ █ ▀█▀[/mono]    [head=2]" }{$title}{ "[/head][/color]" }
    { "[color=#50afbf][mono]█ ▀█  █[/mono]     [head=3]Sector: " }{$station} | NFZ-MED{ "[/head][/color]" }
    ==============================================
    { "[bold]Date:[/bold] " }{$date}
    { "[bold]Sender:[/bold] " }Bluespace NFZ
    { "[bold]Case type:[/bold] " }{$case}
    ==============================================
patient-intake-doc-footer = { "[mono]▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄[/mono]" }
    { "[mono]▄▄  ▄▄   ▄▄▄▄     ▄▄▄▄▄▄         ▄▄[/mono]" }
    { "[italic]Place for stamp:[/italic]" }
patient-intake-doc-item = {$index}. {$line}

patient-intake-file-title = Treatment referral
patient-intake-file-identity = - [bold]Patient:[/bold] {$name}
patient-intake-file-patient = - [bold]Species:[/bold] {$species}
patient-intake-file-deadline = - [bold]Time limit:[/bold] {$minutes} min
patient-intake-file-deadline-hard = - [bold]Time limit:[/bold] {$minutes} min [color=orange](delays will be charged to the medical budget!)[/color]
patient-intake-file-payout = - [bold]Payout:[/bold] up to {$amount} spesos
patient-intake-file-mass = This case has {$count} patients. They arrive one at a time: take the patient out and close the empty tray to receive the next one. Return each one separately.
patient-intake-file-issues = - [bold]Findings:[/bold]
patient-intake-file-symptoms = - [bold]Reported symptoms:[/bold]
patient-intake-file-gear = - [bold]Personal effects:[/bold]

## Receipt

patient-intake-receipt-name = Bluespace NFZ receipt
patient-intake-receipt-title = Case settlement
patient-intake-receipt-pass = {$name}: [color=green]passed[/color], {$amount} spesos.
patient-intake-receipt-pass-late = {$name}: [color=green]passed[/color] but [color=yellow]after the time limit[/color], {$amount} spesos.
patient-intake-receipt-fail = {$name}: [color=red]not healthy[/color], no pay.
patient-intake-receipt-dead = {$name}: [color=red]returned dead[/color], {$amount} spesos charged.
patient-intake-receipt-late = {$name}: [color=red]returned late[/color], {$amount} spesos charged.
patient-intake-receipt-gibbed = {$name}: [color=red]body destroyed[/color], {$amount} spesos charged.
patient-intake-receipt-destroyed = {$name}: [color=red]body lost[/color], {$amount} spesos charged.
patient-intake-receipt-mind = {$name}: [color=red]body taken over[/color], {$amount} spesos charged.
patient-intake-receipt-missing = Case marked as missing, {$amount} spesos charged.
patient-intake-receipt-gear = - Missing personal effects ({$count}): -{$amount} spesos ({$percent}%)
patient-intake-receipt-time = - Time: {$time} (limit {$limit} min)
patient-intake-receipt-time-unopened = - Time: the tray was never opened
patient-intake-receipt-base = - Base pay: {$amount} spesos
patient-intake-receipt-time-cut = - Time penalty: -{$amount} spesos
patient-intake-receipt-total = [bold]Total: {$amount} spesos.[/bold]

## Failures

patient-intake-failure-dead = Patient is dead
patient-intake-failure-damage = Body still damaged
patient-intake-failure-limbdamage = {$part}: damaged
patient-intake-failure-organdamage = {$part}: organ damage
patient-intake-failure-trauma = {$part}: trauma
patient-intake-failure-bleeding = Bleeding
patient-intake-failure-missingpart = Missing: {$part}
patient-intake-failure-unfinishedsurgery = {$part}: unfinished surgery
patient-intake-failure-lowblood = Blood volume too low
patient-intake-failure-harmfulreagent = Harmful substance in the blood: {$part}
patient-intake-failure-blind = Eye damage
patient-intake-failure-implant = Implant not removed: {$part}
patient-intake-failure-requestnotdone = Organ not replaced: {$part}

patient-intake-part-torso = torso
patient-intake-part-head = head
patient-intake-part-armleft = left arm
patient-intake-part-armright = right arm
patient-intake-part-handleft = left hand
patient-intake-part-handright = right hand
patient-intake-part-legleft = left leg
patient-intake-part-legright = right leg
patient-intake-part-footleft = left foot
patient-intake-part-footright = right foot
patient-intake-part-brain = brain
patient-intake-part-eyes = eyes
patient-intake-part-tongue = tongue
patient-intake-part-appendix = appendix
patient-intake-part-ears = ears
patient-intake-part-lungs = lungs
patient-intake-part-heart = heart
patient-intake-part-stomach = stomach
patient-intake-part-liver = liver
patient-intake-part-kidneys = kidneys
patient-intake-part-beak = beak

## Issues

patient-intake-issue-bruising = Blunt trauma to a limb
patient-intake-symptom-bruising = Bruised and sore
patient-intake-issue-crush = Severe crush injury to a limb
patient-intake-symptom-crush = Was caught under something heavy
patient-intake-issue-laceration = Deep laceration
patient-intake-symptom-laceration = Cut themselves badly
patient-intake-issue-puncture = Puncture wound
patient-intake-symptom-puncture = Stepped on or fell onto something sharp
patient-intake-issue-burn = Burns
patient-intake-symptom-burn = Touched something hot
patient-intake-issue-severe-burn = Severe burns
patient-intake-symptom-severe-burn = Caught in a fire
patient-intake-issue-frostbite = Frostbite
patient-intake-symptom-frostbite = Locked in a freezer for a while
patient-intake-issue-fracture = Fractured bone
patient-intake-symptom-fracture = Fell down some stairs, can't move a limb properly
patient-intake-issue-organ = Internal organ damage
patient-intake-symptom-organ = Abdominal pain after an accident
patient-intake-issue-eye-trauma = Eye trauma
patient-intake-symptom-eye-trauma = Took a hit to the head
patient-intake-issue-poisoning = Toxin poisoning
patient-intake-symptom-poisoning = Nauseous, ate something odd
patient-intake-issue-carp-poisoning = Carpotoxin poisoning
patient-intake-symptom-carp-poisoning = Ate badly prepared fish
patient-intake-issue-blood-loss = Blood loss from a wound
patient-intake-symptom-blood-loss = Pale, dizzy, lost a lot of blood
patient-intake-issue-blind = Eye damage
patient-intake-symptom-blind = Complains of blurry vision
patient-intake-issue-implant = Unauthorized tracking implant (extract it)
patient-intake-symptom-implant = Feels something under the skin
patient-intake-issue-missing-organ = Missing internal organ: {$part}
patient-intake-symptom-missing-organ = Survived an organ theft
patient-intake-issue-missing-extremity = Missing hand or foot: {$part}
patient-intake-symptom-missing-extremity = Lost an extremity in an accident
patient-intake-issue-missing-limb = Missing arm or leg: {$part}
patient-intake-symptom-missing-limb = Lost a limb in an accident
patient-intake-issue-replace-eyes = Requests new eyes (transplant)
patient-intake-issue-replace-liver = Requests a liver transplant
patient-intake-issue-replace-lungs = Requests a lung transplant
patient-intake-issue-prosthesis = Wants a prosthesis: {$part}

## Device linking

patient-intake-fax-sender = Bluespace NFZ

signal-port-name-patient-intake-fax = Fax
signal-port-description-patient-intake-fax = Sends case files and receipts to a fax machine.
signal-port-name-patient-intake-fax-receiver = Bluespace NFZ
signal-port-description-patient-intake-fax-receiver = Prints case files and receipts from Bluespace NFZ.

## Admin command

cmd-patientintake_start-desc = Starts a Bluespace NFZ case right away, skipping the timer and the staffing check.
cmd-patientintake_start-help = Usage: patientintake_start <machine uid> [case id]
    Without a case id, any case kind can roll.
cmd-patientintake_start-arg-machine = <machine uid>
cmd-patientintake_start-arg-case = [case id]
cmd-patientintake_start-no-machine = {$uid} is not a Bluespace NFZ machine.
cmd-patientintake_start-busy = That machine already has an active case.
cmd-patientintake_start-open = Close the tray first.
cmd-patientintake_start-no-case = No Bluespace NFZ case called {$case}.
cmd-patientintake_start-started = Started case {$case}.
