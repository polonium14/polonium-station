## SuspicionGui.xaml.cs

# Shown when clicking your Role Button in Suspicion
suspicion-ally-count-display =
    { $allyCount ->
       *[zero] Nie masz sojuszników
        [one] Twoim sojusznikiem jest { $allyNames }
        [other] Twoi sojusznicy to { $allyNames }
    }
