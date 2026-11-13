breathalyzer-busy = { CAPITALIZE(THE($device)) } is still analysing a sample.
breathalyzer-cannot-blow = You can't blow into { THE($device) }.
breathalyzer-blow-self = You blow into the mouthpiece.
breathalyzer-blow-others = { CAPITALIZE(THE($user)) } blows into { THE($device) }.
breathalyzer-force-user = You hold { THE($device) } to { THE($target) }'s mouth and make them blow.
breathalyzer-force-others = { CAPITALIZE(THE($user)) } holds { THE($device) } to { THE($target) }'s mouth.

breathalyzer-examine-idle = The display is ready for a test.
breathalyzer-examine-busy = The display shows a test in progress.
breathalyzer-examine-result = The display shows [color={ $color }]{ $result }‰ ({ $level })[/color].

breathalyzer-verb-open-display = Look at the display

breathalyzer-level-sober = Sober
breathalyzer-level-after-drinking = After drinking
breathalyzer-level-intoxicated = Intoxicated
breathalyzer-level-poisoning = Alcohol poisoning

breathalyzer-ui-header = BREATHALYZER 4000
breathalyzer-ui-state-idle = STANDBY
breathalyzer-ui-state-sampling = SAMPLING
breathalyzer-ui-state-analyzing = ANALYSIS
breathalyzer-ui-state-result = RESULT
breathalyzer-ui-state-previous = PREVIOUS
breathalyzer-ui-status-ready = READY
breathalyzer-ui-status-blow = BLOW
breathalyzer-ui-status-analyze = ANALYSING
breathalyzer-ui-footer-idle = Use it in hand to blow into it.
breathalyzer-ui-footer-blow = Keep blowing evenly until the signal.
breathalyzer-ui-footer-analyze = Hold on, the sample is being analysed.
breathalyzer-ui-footer-dirty = Clean the mouthpiece with soap.
breathalyzer-ui-status-dirty = DIRTY MOUTHPIECE

breathalyzer-dirty = The mouthpiece is covered in vomit. Clean it with soap first.
breathalyzer-vomit = The mouthpiece is covered in vomit!
breathalyzer-cleaned = The mouthpiece is clean again.
breathalyzer-examine-dirty = [color=yellowgreen]The mouthpiece is smeared with vomit.[/color]

breathalyzer-report = [head=2]Breathalyzer printout[/head]
    Subject: { $subject }
    Reading: { $result }‰
    Assessment: { $level }
    Time of test: { $time }
breathalyzer-report-unknown = unknown
