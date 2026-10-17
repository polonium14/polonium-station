materials-replisteel = replisteel
stack-replisteel = replisteel
replicator-hand-replisteel-empty = Replisteel
tiles-replicator-floor = replicator floor

alerts-replicator-recall-immune-name = Nanite trace
alerts-replicator-recall-immune-desc = Nanites still cling to you after a Hive Recall. Replicators can't teleport you away again until the trace fades.

replicator-construction-fail-no-floor = There is no floor to work on here.
replicator-construction-fail-already-ours = This already belongs to the hive.
replicator-construction-fail-wall = The nanites can't take over this wall.
replicator-construction-fail-budget = The nest is short on replisteel, this costs { $cost } { $cost ->
        [one] sheet
       *[other] sheets
    }.
replicator-construction-fail-occupied = Something is in the way.
replicator-construction-fail-no-nest = You have no nest to draw replisteel from.

replicator-fabricator-recipe-name = replicator shell
replicator-fabricator-recipe-desc = A deadly machine that will wake up any moment now
replicator-fabricator-examine-working = It is assembling a shell, [color=violet]{ $seconds } s[/color] left.
replicator-fabricator-examine-ready = A finished shell waits for a mind to take it.
replicator-fabricator-examine-idle = It stands idle.
replicator-fabricator-limit = The hive can't sustain more than { $limit } replicators right now.

replicator-hive-unlocked = The nest reached level { $level }. Now available: { $structures }.
replicator-hive-examine = The nest holds [color=violet]{ $budget }[/color] sheets of replisteel, { $reserve } of them are kept in reserve. It is at level { $level }.

replicator-teleport-prey-fail-immune = Nanites still cling to that target, it can't be recalled yet.
replicator-on-structure-attack-fail = You cannot harm what the hive has built.

name-identifier-format-replicator = RL-{ $number }
name-identifier-format-replicator-colossus = KL-{ $number }

replicator-colossus-pulse-target = A wave of bluespace tears you away!

replicator-colossus-announcement = Central Command has detected an anomalous surge of bluespace energy. Location: { $location }. Destroy the threat at all costs!
replicator-colossus-defeat-announcement = The bluespace signature is gone. Clear out what remains of the threat!
replicator-colossus-foundry-warning = Central Command is picking up rising bluespace readings. Location: { $location }. The replicators are assembling something enormous. Find it and tear it down before it comes online.

replicator-colossus-foundry-fail-built = The hive has already raised its colossus foundry this shift.
replicator-colossus-foundry-fail-level = The nest has to reach level { $level } first.
replicator-colossus-foundry-fail-floor = The foundry needs solid floor { $size } tiles across, with no gaps into space.
replicator-colossus-foundry-fail-blocked = Something solid is in the way. The foundry needs a clear { $size }x{ $size } patch of floor.
replicator-colossus-foundry-fail-hive-floor = The foundry needs a { $size }x{ $size } patch of replicator floor under it.
replicator-colossus-foundry-fail-mob = Someone is standing where the foundry would rise.
replicator-colossus-foundry-part-not-replicator = You have no idea where this goes.
replicator-colossus-foundry-part-complete = The colossus is already assembled.
replicator-colossus-foundry-part-wrong = The foundry needs the { $part } next.
replicator-colossus-foundry-complete = The colossus is assembled. Initialize it at the foundry.
replicator-colossus-foundry-verb-initialize = Initialize
replicator-colossus-foundry-awaiting = The colossus is already waiting for a mind.
replicator-colossus-foundry-incomplete = Not every part is in place yet.
replicator-colossus-foundry-initializing = The colossus core wakes up and reaches out for a mind.
replicator-colossus-foundry-examine-stage = Assembly: [color=violet]{ $stage }/{ $total }[/color] parts in place.
replicator-colossus-foundry-examine-next = Next part: [color=violet]{ $part }[/color].
replicator-colossus-foundry-examine-awaiting = The core pulses in the dark, waiting for a mind.

alert-level-delta-colossus-announcement = A Colossal Replicator is loose aboard the station. Crewmembers are to keep out of its path and follow the orders of the heads of staff.

ghost-role-information-replicator-colossus-name = Colossal Replicator
ghost-role-information-replicator-colossus-desc = The hive's crowning work. Stride across the station, remake it for the hive and crush whoever stands in the way.

replicator-construction-fail-reinforce-foreign = That is not a hive wall.
replicator-construction-fail-reinforce-done = That wall is already reinforced.
replicator-construction-fail-no-wall = There is no wall to take over there.
replicator-construction-fail-not-hive-floor = The hive builds only on replicator floor.
replicator-build-selected = Selected: { $structure }. Point at replicator floor with the replisteel in hand.
replicator-build-fail-range = That is too far away.
replicator-build-fail-level = The nest has to reach level { $level } first.
replicator-build-fail-sheets = You need { $cost } { $cost ->
        [one] sheet
       *[other] sheets
    } of replisteel for this. The sheets you hold and the nest's stock together are not enough.
replicator-build-menu-entry = { CAPITALIZE($structure) } ({ $cost } { $cost ->
        [one] sheet
       *[other] sheets
    })
replicator-build-menu-locked = { CAPITALIZE($structure) }: the nest has to reach level { $level }
replicator-hive-withdraw-reserve = The nest keeps { $reserve } sheets in reserve for the hive's abilities.
replicator-duplicator-recipe-name = duplicate replisteel
replicator-duplicator-recipe-desc = One sheet goes in, two come out.

replicator-evolution-title = Evolution chamber
replicator-evolution-stock = Nest stock: { $budget } sheets
replicator-evolution-level = Nest level: { $level }
replicator-evolution-grown = Stages grown: { $grown } of { $limit }
replicator-evolution-grow = Grow ({ $cost } { $cost ->
        [one] sheet
       *[other] sheets
    })
replicator-evolution-complete = Fully grown
replicator-module-fail-complete = This branch is fully grown.
replicator-module-fail-full = Your shell holds no more than { $limit } upgrade stages.
replicator-module-started = The chamber's arms close in around you.
replicator-module-installed = { $module } grown to stage { $stage }.
replicator-evolution-no-class = Shell: universal
replicator-evolution-class = Class: { $class }
replicator-evolution-choose = Choose a class. The chamber rebuilds your whole shell for one role, and that role decides which branches you can grow. [color=#ffff91]The choice is final.[/color]
replicator-evolution-branches = Branches: { $branches }
replicator-evolution-specialize = Rebuild shell
replicator-evolution-specialize-cost = Rebuild shell ({ $cost } { $cost ->
        [one] sheet
       *[other] sheets
    })
replicator-evolution-form = { $stages ->
        [0] Form { $number }: right after the rebuild
       *[other] Form { $number }: after { $stages } grown stages
    }
replicator-class-fail-chosen = Your shell has already been rebuilt for one role.
replicator-module-fail-class = Your class does not grow this branch.
replicator-class-installed = The chamber rebuilds your shell: { $class }.
replicator-class-fighter-name = Fighter
replicator-class-fighter-desc = A wedge head between two box cannons topped with horn fins, and a serrated ram blade under its chin. Breaks structures and holds its ground in a fight.
replicator-class-defender-name = Defender
replicator-class-defender-desc = A heavy armoured hull on thrusters, with massive shoulders and a single sensor eye under a light bar. Soaks up damage and, fully grown, mends the rest of the hive. Moves slower than the other shells.
replicator-class-scout-name = Scout
replicator-class-scout-desc = A tall narrow spire between two broad swept-back blades with running lights, and a single slit eye across the front. The fastest shell in the hive, and it can drop out of sight.
replicator-class-builder-name = Builder
replicator-class-builder-desc = A low, wide barge on thrusters with an engine drum on each side and a strapped stack of plates on its back. Builds and converts faster and hauls replisteel for the nest.
replicator-class-hunter-name = Hunter
replicator-class-hunter-desc = A wide wedge with lit teeth along its edges, two emitters on its brow and a single slit eye. Recalls prey faster and tracks the crew for the whole hive.
replicator-module-thrusters-name = Thrusters
replicator-module-thrusters-1 = You move 10% faster.
replicator-module-thrusters-2 = You move 20% faster.
replicator-module-thrusters-3 = You move 30% faster and gain a thruster burst: a lunge of about three tiles every 15 seconds.
replicator-module-cloak-name = Cloak
replicator-module-cloak-1 = You turn translucent while you hold still. Moving or attacking gives you away.
replicator-module-cloak-2 = You vanish completely while you hold still.
replicator-module-cloak-3 = You stay hidden while moving slowly. Sprinting or attacking still gives you away.
replicator-module-plating-name = Plating
replicator-module-plating-1 = You take 15% less damage.
replicator-module-plating-2 = You take 30% less damage.
replicator-module-plating-3 = You take 40% less damage, and stun weapons wear you down half as fast.
replicator-module-emitter-name = Emitter
replicator-module-emitter-1 = Your stun ray holds 8 charges instead of 6 and fires 20% faster.
replicator-module-emitter-2 = Your stun ray holds 10 charges, regains them faster and fires 40% faster.
replicator-module-emitter-3 = Your stun ray holds 13 charges, regains them faster still and fires 50% faster, and every 20 seconds you can loose three rays at once in a fan.
replicator-module-ram-name = Ram
replicator-module-ram-1 = Your bare strike hits structures twice as hard.
replicator-module-ram-2 = Your bare strike also drains half again as much stamina.
replicator-module-ram-3 = You grow a heavy arm: wide swings that tear through structures and pry doors open.
replicator-module-toolkit-name = Toolkit
replicator-module-toolkit-1 = You build and convert 20% faster.
replicator-module-toolkit-2 = You build and convert 40% faster and grow an omnitool.
replicator-module-toolkit-3 = You grow a welder, converting the floor under you also takes the four tiles next to it, and you can reinforce the hive's walls.
replicator-module-regeneration-name = Regeneration
replicator-module-regeneration-1 = You mend yourself twice as fast.
replicator-module-regeneration-2 = You mend yourself three times as fast.
replicator-module-regeneration-3 = You can mend other replicators and hive structures: 25 damage for every six seconds of holding still next to them.
replicator-module-capture-name = Recall array
replicator-module-capture-1 = Hive Recall channels 25% faster.
replicator-module-capture-2 = A recalled target stays paralysed two seconds longer.
replicator-module-capture-3 = A recalled target can be recalled again after 15 seconds instead of 25.
replicator-module-cargo-name = Cargo bay
replicator-module-cargo-1 = You grow a second hand for replisteel.
replicator-module-cargo-2 = Whatever you drag barely slows you down.
replicator-module-cargo-3 = Once a minute you can send the replisteel you hold straight into the nest's stock.
replicator-module-sensors-name = Sensors
replicator-module-sensors-1 = An arrow points you to the nearest living crew member.
replicator-module-sensors-2 = You can mark a living target: for 45 seconds the whole hive sees it through walls.
replicator-module-sensors-3 = Once a minute a scan pulse marks every living crew member within twelve tiles for 20 seconds.
replicator-repair-fail-self = You cannot mend yourself this way.
replicator-repair-fail-target = You can mend only replicators and hive structures.
replicator-repair-fail-intact = There is nothing to mend there.
replicator-cargo-fail-empty = You hold nothing the nest could use.
replicator-cargo-sent = Sent to the nest: { $amount } sheets' worth.
replicator-mark-fail-target = Only a living target can be marked.
replicator-mark-applied = Target marked for { $seconds } seconds.
replicator-scan-result = { $count ->
        [0] The pulse found nobody.
       *[other] The pulse marked crew members: { $count }.
    }

replicator-teleport-prey-fail-warded = Something on that target scatters the lock. It can't be recalled.

replicator-colossus-beacon-fail-exists = Your beacon already stands.
replicator-colossus-beacon-announcement = Central Command has located a redspace beacon aboard your station. Location: { $location }. Its energy is building and will peak in { $minutes } minutes.
replicator-colossus-beacon-warning = The beacon's signal ({ $location }) is still growing. It will peak in { $minutes } { $minutes ->
        [one] minute
       *[other] minutes
    }.
replicator-colossus-beacon-ready-announcement = The beacon's signal ({ $location }) has peaked. Whatever was meant to hear it is listening now.
replicator-colossus-beacon-ready = The beacon is charged. Come close and it will latch onto your core.
replicator-colossus-beacon-destroyed-announcement = The beacon has been destroyed and its signal has gone quiet. The Colossal Replicator can raise another one, but the signal will have to build up again.
replicator-colossus-beacon-lost = Your beacon has been destroyed!
replicator-colossus-beacon-examine-charging = It will be charged in [color=orange]{ $time }[/color].
replicator-colossus-beacon-examine-ready = [color=red]It is fully charged.[/color]
replicator-colossus-link-start = The beacon latches onto your core. You cannot move or fire until it is done.
replicator-colossus-link-broken = The link is broken! Your weapons are restarting.
replicator-colossus-rearmed = Your weapons are back online.
replicator-colossus-disarmed = Your systems are not responding.
replicator-colossus-assimilate-announcement = The beacon ({ $location }) has latched onto the Colossal Replicator and is drawing on its core. By our calculations, in { $seconds } seconds everything aboard will be destroyed. The colossus cannot move or fire while it feeds the beacon. Hit the beacon with everything you have to break the link.
replicator-colossus-assimilate-interrupted-announcement = The link has been broken and the beacon ({ $location }) has lost part of its charge. Its signal will peak again in { $minutes } { $minutes ->
        [one] minute
       *[other] minutes
    }. The colossus is disarmed for a moment.
replicator-colossus-assimilate-released-announcement = The assimilation has been released.
replicator-colossus-assimilated = The wave takes you apart and puts you back together. You are a replicator now, and you serve the hive.

replicator-colossus-wreck-fail-replicator = The hive will not let you cut the colossus apart.
replicator-colossus-core-verb-study = Study
replicator-colossus-core-examine = Someone who took the time to study it could learn how to keep the nanites off.
replicator-colossus-core-examine-studied = It has already been studied.
replicator-colossus-core-announcement = The crew has studied the core of the Colossal Replicator. Nanite ward vests can now be printed at any lathe on the station. They stop replicators from teleporting the wearer and take most of the force out of their blows and stun rays.

chat-radio-replicator-hive = Hive
replicator-emp-stunned = The pulse scrambles your lattice!

replicator-round-end-assimilated = [color=#d70aa0]The Colossal Replicator assimilated the station. The hive won.[/color]
replicator-round-end-colossus-destroyed = [color=green]The Colossal Replicator was destroyed. The hive lost.[/color]

replicator-queen-crowned = The hive has lost its queen. { $queen } is the new queen.
replicator-queen-crowned-self = The hive has lost its queen. You are the new queen.
replicator-queen-crowned-nestless = The hive has lost its queen and has no nest. { $queen } is the new queen, and only the queen can manufacture a new nest.
replicator-queen-crowned-nestless-self = The hive has lost its queen and has no nest. You are the new queen, so manufacture a new nest with the Manufacture Nest ability.
replicator-queen-nest-lost = The nest has been destroyed. Only the queen, { $queen }, can manufacture a new one.
replicator-queen-nest-lost-self = The nest has been destroyed. You are the queen, so manufacture a new one with the Manufacture Nest ability.
