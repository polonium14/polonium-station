### Locale for wielding items; i.e. two-handing them

wieldable-verb-text-wield = Chwyć oburącz
wieldable-verb-text-unwield = Puść
wieldable-component-successful-wield = You wield { THE($item) }.
wieldable-component-failed-wield = You unwield { THE($item) }.
wieldable-component-successful-wield-other = { CAPITALIZE(THE($user)) } wields { THE($item) }.
wieldable-component-blocked-wield = { CAPITALIZE($blocker) } blokuje cię przed chwyceniem { $item } oburącz.
wieldable-component-failed-wield-other = { CAPITALIZE(THE($user)) } unwields { THE($item) }.
wieldable-component-no-hands = Nie masz wystarczająco rąk!
wieldable-component-not-enough-free-hands =
    { $number ->
        [one] You need a free hand to wield { THE($item) }.
       *[other] You need { $number } free hands to wield { THE($item) }.
    }
wieldable-component-not-in-hands = { CAPITALIZE(THE($item)) } isn't in your hands!
wieldable-component-requires = { CAPITALIZE(THE($item)) } must be wielded!
gunwieldbonus-component-examine = Ta broń ma lepszą celność, gdy jest trzymana oburącz.
gunrequireswield-component-examine = Tę broń można wystrzelić tylko, gdy jest trzymana oburącz.
