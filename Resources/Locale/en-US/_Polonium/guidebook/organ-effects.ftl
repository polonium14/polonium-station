entity-effect-guidebook-heal-organs =
    { $chance ->
        [1] Heals
        *[other] heal
    } {NATURALFIXED($amount, 2)} integrity of { $organs }
entity-effect-guidebook-damage-organs =
    { $chance ->
        [1] Damages
        *[other] damage
    } { $organs } by {NATURALFIXED($amount, 2)} integrity
entity-effect-guidebook-organs-all = every organ
entity-effect-guidebook-organs-all-except = every organ except { $organs }
entity-effect-guidebook-organ-Heart = the heart
entity-effect-guidebook-organ-Lungs = the lungs
entity-effect-guidebook-organ-Liver = the liver
entity-effect-guidebook-organ-Kidneys = the kidneys
entity-effect-guidebook-organ-Stomach = the stomach
entity-effect-guidebook-organ-Eyes = the eyes
entity-effect-guidebook-organ-Tongue = the tongue
entity-effect-guidebook-organ-Ears = the ears
entity-effect-guidebook-organ-Appendix = the appendix
entity-effect-guidebook-organ-Brain = the brain
