# Архитектура

## Слои

```
┌─────────────────────────────────────────────┐
│ UI (uGUI, генерируется кодом)               │  UIRoot, CatalogView, PeriodicTableView
├─────────────────────────────────────────────┤
│ Геймплей (MonoBehaviour)                    │  PlayerController, SandboxWorld, NPC, Projectile,
│                                             │  FluidContainer, ThermalSystem, EffectsSystem
├─────────────────────────────────────────────┤
│ Симуляция (чистый C#, без Unity-зависимости)│  MaterialDatabase, Mixture, ReactionEngine,
│                                             │  ArmorModel, ProjectileStats, Json/JsonWriter
└─────────────────────────────────────────────┘
```

Нижний слой не знает про `GameObject`, `Rigidbody` и сцены — только `Vector3`, `Mathf`, `Color`.
Поэтому он компилируется и тестируется обычным `dotnet` (см. `tools/simtest`).

## Поток кадра

1. `PlayerController.Update` — ввод, инструменты, рейкасты.
2. `FluidContainer.FixedUpdate` — тик смеси, реакции, давление.
3. `ThermalSystem.Update` (0.12 с) — теплообмен между объектами, плавление.
4. `SandboxObject.FixedUpdate` — горение, остывание, урон от перегрева.
5. `Projectile.FixedUpdate` — свип-рейкаст, `ArmorModel.Resolve`, заброневой урон.
6. `UIRoot.Update` — HUD, FPS, строка состояния, описание под курсором.
7. `CameraRig.LateUpdate` — орбита, инерция, тряска.

## Баллистика

```csharp
ProjectileStats.Make(kind, massGrams, velocity, caliberMM)
  КЭ = 0.5 * m * v²
  пробитие(мм RHA) = sqrt(КЭ) * 0.030 * sqrt(калибр) * множитель(тип)
  кумулятивный заряд = калибр * 5.5
```

`ArmorModel.Resolve` сравнивает пробитие с суммой `RHAEquivalent` слоёв:

```
RHA слоя = (толщина / cos(угол)) * (твёрдость/1500)^0.45 * (1.25..0.9 по эластичности)
```

Результаты: `Stopped / Partial / Penetrated / Ricochet`, плюс остаточная энергия,
масса заброневого осколка и урон по телу (`BodyDamage`).

## Химия

`Mixture` хранит моли каждого вещества, температуру, объём.
`ReactionEngine.FindReactions` отбирает реакции, у которых все реагенты присутствуют
и температура выше порога активации; сортировка по `ΔH` — сначала самые экзотермические,
то есть взрывы.

`Apply` ограничивает глубину протекания лимитирующим реагентом и возвращает выделенное тепло,
которое идёт в температуру смеси. Эффекты (`explosion`, `fire`, `gas`, ...) превращаются
в вызовы `EffectsSystem` и `SandboxWorld.Explode`.

## Сохранение

`SaveSystem` сериализует объекты (материал, форма, трансформ, HP, температура, горение),
ёмкости (моли по веществам, температура) и NPC. Формат — JSON в
`Application.persistentDataPath/scenes`.

## Производительность

* материалы объектов кэшируются (`RuntimeVisuals.ForDef`), цвет и нагрев идут через `MaterialPropertyBlock`;
* частицы — пул на 3 типа, нижний слой создаётся один раз;
* декали ограничены 160 штуками и живут 60 секунд;
* на мобильных уменьшено число частиц в взрыве и качество теней (Quality: Low/Medium);
* физика: 8 итераций решателя, континуальное обнаружение столкновений для снарядов.
