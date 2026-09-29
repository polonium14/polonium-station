materials-replisteel = replisteel
stack-replisteel = replisteel
replicator-hand-replisteel-empty = Replisteel
tiles-replicator-floor = replicator floor

alerts-replicator-recall-immune-name = Nanite trace
alerts-replicator-recall-immune-desc = Nanites still cling to you after a Hive Recall. Replicators can't recall you again until the trace fades.

replicator-construction-fail-no-floor = There is no floor to work on here.
replicator-construction-fail-already-ours = This already belongs to the hive.
replicator-construction-fail-wall = The nanites can't take over this wall.
replicator-construction-fail-budget = The hive is short on replisteel, this costs { $cost } { $cost ->
        [one] sheet
       *[other] sheets
    }.
replicator-construction-fail-occupied = Something is in the way.
replicator-construction-fail-no-nest = You have no nest to draw replisteel from.

replicator-fabricator-verb = Assemble a shell ({ $cost } { $cost ->
        [one] sheet
       *[other] sheets
    })
replicator-fabricator-busy = The fabricator is already busy.
replicator-fabricator-examine-working = It is assembling a shell, [color=violet]{ $seconds } s[/color] left.
replicator-fabricator-examine-ready = A finished shell waits for a mind to take it.
replicator-fabricator-examine-idle = It stands idle.
replicator-fabricator-limit = The hive can't sustain more than { $limit } replicators right now.
replicator-fabricator-started = The fabricator starts assembling a shell.

replicator-rebuild-tier1 = Rebuild into tier 1 (free)
replicator-rebuild-tier2 = Rebuild into tier 2 ({ $cost } { $cost ->
        [one] sheet
       *[other] sheets
    })
replicator-rebuild-tier3 = Rebuild into tier 3 ({ $cost } { $cost ->
        [one] sheet
       *[other] sheets
    })
replicator-rebuild-fail-same = You already are this tier.
replicator-rebuild-fail-level = The nest has to reach level { $level } first.
replicator-rebuild-started = You sink into the nest and the nanites start taking you apart.
replicator-rebuild-finished = You crawl out of the nest in a new shell.
replicator-rebuild-interrupted = The nest is gone, and the replisteel spent on your rebuild went with it.

replicator-hive-unlocked = The nest reached level { $level }. Now available: { $classes }.
replicator-hive-examine = The hive holds [color=violet]{ $budget } { $budget ->
        [one] sheet
       *[other] sheets
    }[/color] of replisteel. The nest is at level { $level }.

replicator-teleport-prey-fail-immune = Nanites still cling to that target, it can't be recalled yet.
replicator-on-structure-attack-fail = You cannot harm what the hive has built.
