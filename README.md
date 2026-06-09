# Symulator ruchu samochodów z omijaniem przeszkód

Projekt z przedmiotu *Inżynieria Wiedzy i Symboliczne Uczenie Maszynowe* — pojazd-agent w Unity 3D porusza się po grafie dróg w mieście, a po napotkaniu przeszkody uruchamia moduł **Q-learning**, który uczy się ją ominąć i wrócić na trasę.

## Dwa warianty

| | Gałąź | Akcje agenta | Sterowanie kierownicą |
|---|---|---|---|
| **Wariant 1** | `main` | jedź prosto / w lewo / w prawo | natychmiastowe |
| **Wariant 2** | `method2` | trzymaj / obróć w lewo / obróć w prawo | ciągłe (120°/s) |

## Uruchomienie

```bash
git clone https://github.com/rszepieniec/qlearning-car-unity.git
cd qlearning-car-unity
git checkout main          # lub: git checkout method2
```

Otwórz folder `DeliveryMaster/` w **Unity 6** jako projekt, wczytaj scenę z `Assets/Scenes/` i naciśnij **Play**. Logi prób trafiają do konsoli Unity.

## Struktura

```
DeliveryMaster/
└── Assets/Scripts/
    ├── CarController2.cs    # fasada pojazdu (wariant 1)
    ├── CarController3.cs    # fasada pojazdu (wariant 2)
    ├── QLearn.cs            # tablica Q + ε-greedy + Bellman update
    └── Waypoint.cs          # węzeł grafu dróg
```

## Autorzy

Szymon Miękina · Rafał Szepieniec — Czerwiec 2026
