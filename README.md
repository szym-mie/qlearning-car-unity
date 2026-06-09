# Symulator ruchu samochodów z omijaniem przeszkód

> **Q-learning w środowisku Unity 3D** — projekt z przedmiotu *Inżynieria Wiedzy i Symboliczne Uczenie Maszynowe*

Pojazd-agent porusza się po grafie dróg w mieście wykonanym w Unity 3D. Po drodze losowo wybiera trasę za pomocą ważonego drzewka decyzyjnego, a w chwili wykrycia przeszkody przekazuje stery wyuczonemu agentowi Q-learning, który wyprowadza auto za przeszkodę i wraca na trasę.

<p align="center">
  <img src="docs/img/oversteer.png" width="55%" alt="Schemat manewru omijania" />
</p>

---

## Spis treści

- [Demo](#demo)
- [Jak to działa](#jak-to-działa)
- [Dwa warianty](#dwa-warianty)
- [Reprezentacja problemu](#reprezentacja-problemu)
- [Algorytm](#algorytm)
- [Wyniki](#wyniki)
- [Możliwe usprawnienia](#możliwe-usprawnienia)
- [Uruchomienie](#uruchomienie)
- [Struktura repozytorium](#struktura-repozytorium)
- [Autorzy](#autorzy)

---

## Demo

Krótkie nagranie z symulatora (manewr omijania pachołków po wytrenowaniu):

[`docs/iwium_out.mp4`](docs/iwium_out.mp4)

---

## Jak to działa

System łączy dwa mechanizmy, które wymieniają się sterowaniem w trakcie jazdy:

| | Tryb normalnej jazdy | Tryb omijania |
|---|---|---|
| **Aktywuje się gdy** | brak przeszkody przed autem | utknięcie >  ~2 s |
| **Decyduje** | drzewko decyzyjne na grafie `Waypoint`ów | Q-learning (tablica `Q(s,a)`) |
| **Wybiera** | następny węzeł trasy | jedną z trzech komend sterujących |
| **Reset stanu** | nie | tak — agent wraca do punktu startu manewru i próbuje ponownie |

W chwili wykrycia utknięcia auto cofa się o krótki odcinek, zapamiętuje pozycję startową jako *(0, 0)* w lokalnym układzie współrzędnych, po czym moduł Q-learning prowadzi je aż do warunku `y > 20 m` (sukces) lub do kolizji (porażka i reset).

---

## Dwa warianty

| | **Wariant 1 — wysokopoziomowe akcje** | **Wariant 2 — sterowanie kierownicą** |
|---|---|---|
| Gałąź | `main` | `method2` |
| Klasa | `CarController2.cs` | `CarController3.cs` |
| Akcje | `DRIVE_FWD`, `DRIVE_LEFT`, `DRIVE_RIGHT` | `DRIVE`, `STEER_L`, `STEER_R` |
| Kąt skrętu | ustawiany natychmiast (-30° / 0° / +30°) | obraca się z prędkością 120°/s |
| Markowowski? | tak | nie (kąt kół wprowadza opóźnienie) |
| Zbieżność | ok. 25 prób | ok. 65 prób |
| Stabilność w ewaluacji | wysoka | umiarkowana (oscylacje *oversteer*) |

Wariant 2 jest bliższy realnemu modelowi kierownicy, ale wprowadza opóźnienie między decyzją a fizyczną zmianą trajektorii — niedouczona polityka wpada w oscylacje od pasa do pasa, bo reaguje na obserwację, która jest już o kilka klatek nieaktualna.

<p align="center">
  <img src="docs/img/oversteer.png" width="65%" alt="Oversteer w wariancie 2" />
</p>

---

## Reprezentacja problemu

### Układ współrzędnych i sensory

<p align="center">
  <img src="docs/img/coords.png" height="180" alt="Lokalny układ współrzędnych" />
  &nbsp;&nbsp;&nbsp;&nbsp;
  <img src="docs/img/sensors.png" height="180" alt="Pięć raycastów" />
</p>

- **Lokalny układ (x, y)** zaczepiony w punkcie startu manewru — dzięki temu ta sama wiedza działa w każdym miejscu mapy.
- **Pięć raycastów** rzucanych z przodu pojazdu: środkowy (0°), dwa przednie skośne (±25°, do 10 m), dwa boczne (±75°, do 5 m).
- Asymetria `distFwdR − distFwdL` informuje, w którą stronę uciekać przed kolizją.

### Stan po dyskretyzacji

| Pole | Opis | Zakres |
|---|---|---|
| `x` | przesunięcie poprzeczne wzgl. startu | [-2.5, +2.5] m |
| `y` | postęp wzdłuż drogi | [-1, +15] m |
| `dir` / `angle` | orientacja auta / kąt kół | [-40°, +40°] |
| `vel` | prędkość liniowa | [-1, +3] m/s |
| `distCent` | raycast środkowy | [0, 10] m |
| `distFwdL/R` | raycasty przednie | [0, 10] m |
| `distSideL/R` | raycasty boczne | [0, 5] m |

### Rozmiar tablicy Q

| | `x` | `y` | `vel` | `dir/angle` | `dCent` | `dFwdL` | `dFwdR` | `dSideL` | `dSideR` | \|S\| | \|S\|·\|A\| |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Wariant 1 | 1 | 1 | 1 | 1 | 1 | 4 | 4 | 2 | 2 | 64 | **192** |
| Wariant 2 | 1 | 1 | 5 | 1 | 1 | 6 | 6 | 2 | 2 | 720 | **2 160** |

Dyskretyzacja redukuje przestrzeń stanu na tyle, że tablica Q mieści się w kilku kilobajtach i może być uczona w pełni na CPU.

### Funkcja nagrody

```
r = (1 + μ)·Δy + Σ Δdist_i      μ ∈ {0, 1}
```

gdzie `Δy` to przyrost postępu wzdłuż drogi, `Δdist_i` to przyrosty pięciu raycastów, a `μ = 1` aktywuje się gdy ruch agenta jest zgodny z asymetrią raycastów (uciekaj tam gdzie więcej miejsca).

**Wzmocnienia terminalne:** +100 za `y > 20 m`, −100 za kolizję, zbyt mały odstęp (`dist < 1 m`), utknięcie >1 s lub wypadnięcie poza świat.

**Pierwsza wersja nagrody** (`GetReward1`) była dwufazowa (przed/za przeszkodą) z mnożnikiem kosinusowym premiującym konkretny kąt natarcia — wymagała znacznie więcej prób i nie zawsze zbiegała się do dobrej polityki. Została zastąpiona prostszą formą powyżej.

### Detekcja utknięcia z histerezą

```
vel < 0.5 m/s   → uruchom stoper
stoper > 1.0 s  → koniec próby, kara −100
vel > 2.0 m/s   → reset stopera, kontynuuj
```

Pasmo histerezy 0.5–2.0 m/s zapobiega cyklowi *przyspieszam–hamuje* w okolicy progu.

---

## Algorytm

Tabelaryczny **Q-learning** w wersji off-policy z polityką eksploracji ε-greedy:

```
Q(s, a) ← (1 − α)·Q(s, a) + α·(r + γ·max_a' Q(s', a'))
```

| Parametr | Wartość | Opis |
|---|---|---|
| `α` | 0.20 | learning rate |
| `γ` | 0.98 | discount factor |
| `ε₀` | 1.0 | początkowa eksploracja |
| `ε *= 0.90 / 0.95` | po próbie > 10 | wygaszanie (v1 / v2) |
| `ε → 0` | gdy ε < 0.05 | przejście do ewaluacji |
| `actionChangeThreshold` | 0.2 | próg histerezy wyboru akcji |

**Histereza wyboru akcji** — agent przełącza się na inną akcję dopiero gdy jej Q przewyższa Q bieżącej akcji o `actionChangeThreshold`. Tłumi to przełączanie się wokół stanów o bardzo bliskich wartościach.

---

## Wyniki

### Wariant 1 — akcje wysokopoziomowe

<p align="center"><img src="docs/img/method1-chart.png" width="85%" /></p>

- Eksploracja przez ok. 12 prób, nagroda ≈ −90.
- Pierwszy ukończony przejazd ok. próby 25.
- Ewaluacja (ε = 0): stabilna nagroda ≈ +130.

### Wariant 2 — sterowanie kierownicą

<p align="center"><img src="docs/img/method2-chart.png" width="85%" /></p>

- Eksploracja dłuższa (ok. 13 prób), nagroda ≈ −100.
- Polityka stabilizuje się dopiero ok. próby 65.
- Wahania w ewaluacji wynikają z opóźnienia w pętli sterowania.

---

## Możliwe usprawnienia

### Area raycasts zamiast point raycasts

<p align="center">
  <img src="docs/img/odleglosc-do-punktu-i-powierzchni.png" height="170" />
  &nbsp;&nbsp;
  <img src="docs/img/raycasts.png" height="170" />
</p>

Obecnie używamy *point raycastów* — pojedynczych promieni z odczytem do pierwszej przeszkody. Dla prostych pachołków działa to dobrze, ale przy nieregularnych obiektach odczyt potrafi być silnie zaszumiony, co destabilizuje dyskretyzację stanu. *Area raycasty* (Unity `SphereCast` / `BoxCast`) uśredniają trafienia po kącie bryłowym — dają gładszą krzywą odczytu i większą odporność na szum.

### Inne pomysły

- Wyciągnięcie `angle` z `bins[3]=1` do faktycznego binowania w wariancie 2 — powinno zlikwidować pozostałe oscylacje *oversteer*.
- Wymiana tabelarycznego Q-learning na DQN dla bardziej złożonych przeszkód.
- Dodanie *experience replay*, żeby pełniej wykorzystywać każdą próbę.

---

## Uruchomienie

### Wymagania

- **Unity 6** (testowane na 6000.0.x)
- Pakiety: *Unity Collections*, *Unity Mathematics* (instalują się automatycznie przy otwarciu projektu)

### Krok po kroku

```bash
git clone https://github.com/rszepieniec/qlearning-car-unity.git
cd qlearning-car-unity

# Wariant 1 (domyślny):
git checkout main

# Wariant 2:
git checkout method2
```

1. Otwórz folder `DeliveryMaster/` w Unity Hub jako projekt.
2. Wczytaj scenę z `Assets/Scenes/` (np. `Map.unity`).
3. Naciśnij **Play** — pojazd-agent ruszy autonomicznie, a po napotkaniu przeszkody zacznie się uczyć omijania.
4. Logi prób trafiają do konsoli Unity w formacie:
   `Attempt N: y = ..., reward = ..., epsilon = ...`

---

## Struktura repozytorium

```
DeliveryMaster/
├── Assets/
│   ├── Scripts/
│   │   ├── CarController2.cs        # fasada pojazdu (wariant 1)
│   │   ├── CarController3.cs        # fasada pojazdu (wariant 2, branch method2)
│   │   ├── CarControllerNPC.cs      # sterowanie pojazdami otoczenia
│   │   ├── QLearn.cs                # tablica Q + ε-greedy + Bellman update
│   │   ├── Waypoint.cs              # węzeł grafu dróg
│   │   └── ...
│   ├── Scenes/
│   └── Prefabs/
├── Packages/
└── ProjectSettings/
```

### Kluczowe klasy

| Klasa | Rola |
|---|---|
| `CarController2` / `CarController3` | *Fasada* — fizyka auta, nawigacja po grafie, uruchamianie modułu Q-learning |
| `QLearner` | Tablica Q w `NativeArray<float>` + polityka ε-greedy + aktualizacja Bellmana |
| `State` / `Observation` | Struktury wymieniane między fasadą a `QLearner`em |
| `Waypoint` | Węzeł grafu dróg z listą sąsiadów; drzewko decyzyjne wybiera kolejny na skrzyżowaniu |

---

## Autorzy

- **Szymon Miękina**
- **Rafał Szepieniec**

Inżynieria Wiedzy i Symboliczne Uczenie Maszynowe, *Czerwiec 2026*.
