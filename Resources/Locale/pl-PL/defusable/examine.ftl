defusable-examine-defused = { CAPITALIZE($name) } is [color=lime]defused[/color].
defusable-examine-live = { CAPITALIZE($name) } is [color=red]ticking[/color] and has [color=red]{ $time }[/color] seconds remaining.
defusable-examine-live-display-off = { CAPITALIZE($name) } is [color=red]ticking[/color], and the timer appears to be off.
defusable-examine-inactive = { CAPITALIZE($name) } is [color=lime]inactive[/color], but can still be armed.
defusable-examine-bolts =
    The bolts are { $down ->
        [true] [color=red]down[/color]
       *[false] [color=green]up[/color]
    }.
