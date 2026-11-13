zzzz-fmt-list-and = { $count ->
    [2] { $first } and { $last }
   *[other] { $first }, and { $last }
}
zzzz-fmt-list-or = { $first } or { $last }
