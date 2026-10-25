### Interaction Messages


# System


## When trying to ingest without the required utensil... but you gotta hold it

ingestion-you-need-to-hold-utensil = Aby to zjeść, musisz trzymać: { $utensil }!
ingestion-try-use-is-empty = { CAPITALIZE(THE($entity)) } — pusto!
ingestion-try-use-wrong-utensil = Nie możesz { $verb } przedmiotu { THE($food) } za pomocą: { $utensil }.
ingestion-remove-mask = Najpierw musisz zdjąć: { $entity }.

## Failed Ingestion

ingestion-you-cannot-ingest-any-more = Nie możesz już { $verb }!
ingestion-other-cannot-ingest-any-more = { CAPITALIZE(SUBJECT($target)) } nie może już { $verb }!
ingestion-cant-digest = Nie możesz strawić: { THE($entity) }!
ingestion-cant-digest-other = { CAPITALIZE(SUBJECT($target)) } nie może strawić: { THE($entity) }!

## Action Verbs, not to be confused with Verbs

ingestion-verb-food = Zjedz
ingestion-verb-drink = Wypij

# Edible Component

-edible-satiated =
    { $satiated ->
        [true]
            { " " }{ $verb ->
                [eat] Nie masz już ochoty jeść.
                [drink] Nie masz już ochoty pić.
                [swallow] Nie masz już ochoty połykać.
               *[other] Nie masz już ochoty na więcej.
            }
       *[false] { "" }
    }
edible-nom = Nom. {$flavors}{ -edible-satiated(satiated: $satiated, verb: "eat") }
edible-nom-other = Mniam.
edible-slurp = Slurp. {$flavors}{ -edible-satiated(satiated: $satiated, verb: "drink") }
edible-slurp-other = Siorb.
edible-swallow = You swallow { THE($food) }.{ -edible-satiated(satiated: $satiated, verb: "swallow") }
edible-gulp-other = Łyk.
edible-has-used-storage = Nie możesz { $verb } przedmiotu { THE($food) }, gdy w środku jest schowany przedmiot.

## Nouns

edible-noun-edible = jadalne
edible-noun-food = jedzenie
edible-noun-drink = napój
edible-noun-pill = pigułka

## Verbs

edible-verb-edible = spożyć
edible-verb-food = zjeść
edible-verb-drink = wypić
edible-verb-pill = połknąć

## Force feeding

edible-force-feed = { CAPITALIZE(THE($user)) } próbuje zmusić cię do czynności: { $verb }!
edible-force-feed-success = {CAPITALIZE(THE($user))} forced you to {$verb} something! {$flavors}{ -edible-satiated(satiated: $satiated, verb: $verb) }
edible-force-feed-success-user = Pomyślnie nakarmiłeś: { THE($target) }
