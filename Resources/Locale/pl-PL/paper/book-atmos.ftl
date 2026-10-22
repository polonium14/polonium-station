book-text-atmos-distro =
    The distribution network, or "distro" for short, is the station's lifeline. It's responsible for transporting air from atmospherics throughout the station.
    
    Relevant pipes are often painted Popping Subdued Blue, but a surefire way to identify them is to use a tray scanner to trace which pipes are connected to active vents on the station.
    
    The standard gas mix of the distribution network is 20 degrees celsius, 78% nitrogen, 22% oxygen. You can check this by using a gas analyzer on a distro pipe or any vent connected to it. Special circumstances may call for special mixes.
    
    When it comes to deciding on a distro pressure, there are a few things to consider. Active vents will regulate the station's pressure, so as long as everything is functioning properly, there's no such thing as too high of a distro pressure.
    
    A higher distro pressure will allow the distro network to act as a buffer between the gas miners and vents, providing a significant amount of extra air that can be used to re-pressurize the station after a spacing.
    
    A lower distro pressure will reduce the amount of gas lost in the event that the distro is spaced, a quick way to deal with distro contamination. It can also help slow or prevent over-pressurization of the station in the event of vent issues.
    
    Common distro pressures are in the range of 300-375 kPa, but other pressures can be used with knowledge of the risks and benefits.
    
    The pressure of the network is determined by the last pump pumping into it. To prevent bottlenecks, all other pumps between the miners and the last pump should be set to their maximum rate, and any unnecessary devices should be removed.
    
    You can validate the distro pressure with a gas analyzer, but keep in mind that high demand due to things like spacings can cause the distro to be below the set target pressure for extended periods. So, if you see a dip in pressure, don't panic — it might be temporary.
book-text-atmos-waste =
    Sieć odpadowa to główny system odpowiedzialny za utrzymywanie powietrza na stacji w stanie wolnym od zanieczyszczeń.
    
    Odpowiednie rury można rozpoznać po przyjemnym matowoczerwonym kolorze lub użyć skanera T-promieniowego, aby prześledzić, które rury są połączone z oczyszczaczami powietrza na stacji.
    
    Sieć odpadowa służy do transportu gazów odpadowych, które mają zostać przefiltrowane albo wyrzucone w przestrzeń kosmiczną. Idealnie ciśnienie utrzymuje się na poziomie 0 kPa, ale w trakcie użytkowania może czasem być niskie i niezerowe.
    
    Technicy mogą wybrać, czy gazy odpadowe filtrować, czy wyrzucać w kosmos. Wyrzucanie jest szybsze, ale filtrowanie pozwala ponownie wykorzystać gazy w recyklingu lub je sprzedać.
    
    Sieć odpadowa pomaga też diagnozować problemy z atmosferą na stacji. Wysoki poziom gazu odpadowego może wskazywać na duży wyciek, a obecność gazów nieodpadowych może oznaczać błąd w konfiguracji oczyszczacza powietrza lub w fizycznym połączeniu. Jeśli gazy mają wysoką temperaturę, może to oznaczać pożar.
book-text-atmos-alarms =
    Alarmy atmosferyczne są rozmieszczone na całej stacji, aby umożliwić zarządzanie lokalną atmosferą i jej monitorowanie.
    
    Interfejs alarmu atmosferycznego udostępnia technikom listę podłączonych czujników, ich odczyty oraz możliwość regulacji progów. Progi te określają stan alarmowy urządzenia. Za pomocą interfejsu technicy mogą też ustawiać docelowe ciśnienie otworów wentylacyjnych oraz konfigurować prędkość pracy i docelowe gazy oczyszczaczy powietrza.
    
    Interfejs pozwala precyzyjnie dostroić urządzenia podlegające alarmowi, ale dostępnych jest też kilka trybów do szybkiej konfiguracji. Tryby te włączają się automatycznie, gdy zmienia się stan alarmu:
    - Filtrowanie: tryb domyślny
    - Filtrowanie (szerokie): tryb filtrowania, który zmienia pracę oczyszczaczy powietrza tak, by filtrowały większy obszar
    - Napełnianie: wyłącza oczyszczacze powietrza i ustawia otwory wentylacyjne na maksymalne ciśnienie
    - Tryb paniki: wyłącza otwory wentylacyjne i ustawia oczyszczacze powietrza na zasysanie
    
    Do łączenia urządzeń z alarmami atmosferycznymi można użyć multitoola lub konfiguratora sieci.
book-text-atmos-vents =
    Poniżej znajduje się krótki przewodnik po kilku urządzeniach atmosferycznych:
    
                Pasywne otwory wentylacyjne:
                Nie wymagają one zasilania i pozwalają gazom swobodnie przepływać do sieci rur, do której są podłączone, i z niej na zewnątrz.
    
                Aktywne otwory wentylacyjne:
                To najpopularniejszy rodzaj otworów wentylacyjnych na stacji. Mają wewnętrzną pompę i wymagają zasilania. Domyślnie tylko wypompowują gazy z rur, i to tylko do 101 kPa. Można je jednak przekonfigurować za pomocą alarmu atmosferycznego. Blokują się też, gdy ciśnienie w pomieszczeniu spadnie poniżej 1 kPa, aby nie wpompowywać gazów w próżnię.
    
                Oczyszczacze powietrza:
                Te urządzenia usuwają gazy ze środowiska i przekazują je do podłączonej sieci rur. Po podłączeniu do alarmu atmosferycznego można je skonfigurować tak, by wybierały konkretne gazy.
    
                Wtryskiwacze powietrza:
                Wtryskiwacze przypominają aktywne otwory wentylacyjne, ale nie mają wewnętrznej pompy i nie wymagają zasilania. Nie można ich konfigurować, ale potrafią pompować gazy do znacznie wyższych ciśnień.
