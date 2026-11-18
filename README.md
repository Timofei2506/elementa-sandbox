# ELEMENTA — симулятор физики и химии

Песочница в духе Melon Playground / People Playground, но с настоящей физикой материалов,
живой химией и баллистикой брони. 2.5D-геймплей: всё живёт в плоскости, но рендерится в
полноценном 3D с орбитальной камерой, светом и постобработкой.

![жанр](https://img.shields.io/badge/жанр-песочница-2ee6c8) ![unity](https://img.shields.io/badge/Unity-6000.0.85f2-black) ![platform](https://img.shields.io/badge/PC%20%2B%20Android-ready-2ee6c8)

---

## Что внутри

| Система | Что умеет |
|---|---|
| **118 химических элементов** | реальные атомные массы, плотности, температуры плавления/кипения, электроотрицательность |
| **46 материалов** | плотность, твёрдость (МПа), прочность, вязкость, теплоёмкость, теплопроводность, воспламеняемость |
| **62 вещества** | вода, кислоты, щёлочи, растворители, газы, окислители, взрывчатка (тротил, нитроглицерин, термит, порох) |
| **39 реакций** | нейтрализация, выделение газа, горение, детонация, термит, опасные смеси (белизна + кислота → хлор) |
| **Баллистика** | кинетическая энергия, пробитие в мм RHA, наклонная броня, рикошет, заброневой осколок, кумулятивные заряды |
| **23 оружия** | от 9 мм до .50 BMG, РПГ, рельсотрон, огнемёт, лазер |
| **5 типов NPC** | здоровье, ragdoll-смерть, поведение (патруль, преследование, бегство, паника) |
| **10 инструментов** | выбор/перетаскивание, спавн материалов и жидкостей, стрельба, огонь, жар, молот, разрушение, время |
| **Таблица Менделеева** | интерактивная, все 118 элементов; двойной клик создаёт блок с реальными свойствами |
| **6 готовых сцен** | стрельбище, химлаборатория, снос, полигон брони, пожар, химполигон |
| **Процедурный звук** | удары по металлу/керамике/мягкому, выстрелы, взрывы — без аудиофайлов |
| **Сохранение сцен** | JSON в `persistentDataPath`, восстановление объектов, жидкостей и NPC |

## Быстрый старт

1. Установи **Unity 6000.0.85f2 LTS** (или любую 6000.0.x LTS) через Unity Hub.
2. Hub → **Add project from disk** → папка `elementa-sandbox`.
3. В Unity: меню **ELEMENTA → 1. Создать главную сцену** (один раз).
4. Нажми **Play**. Проект собирается полностью в рантайме — никаких битых ссылок в сцене.

### Сборка в GitHub Actions (облако)

Пуш в `main` → GitHub Actions собирает **Windows `.exe`** и **Android `.apk`**, файлы лежат
в Artifacts запуска. Нужны 3 секрета: `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`
(только для билда — тесты работают без них). Полная инструкция: **[docs/CI_BUILD.md](docs/CI_BUILD.md)**.

### Локальная сборка

Подробная инструкция со скриншот-шагами: **[docs/UNITY_SETUP.md](docs/UNITY_SETUP.md)**.

* **ELEMENTA → 3. Собрать Windows (.exe)** — папка с `ELEMENTA.exe`
* **ELEMENTA → 4. Собрать Android (.apk)** — нужен Android Build Support + SDK/NDK в Unity Hub

## Управление

| Действие | ПК | Телефон |
|---|---|---|
| Вращение камеры | ПКМ + движение мыши | один палец |
| Зум | колесо мыши | два пальца (pinch) |
| Панорама | WASD | — (фокус по тапу) |
| Инструмент | ЛКМ | тап |
| Быстрый выбор | 1…9 | нижняя панель |
| Пауза | Space | кнопка «Пауза» |
| Сброс сцены | R | кнопка «Сброс» |
| Каталог | Tab | кнопка «Каталог» |
| Перезарядка | Enter | — |

## Как это устроено

```
Assets/
  Scripts/
    Sim/            чистая C#-симуляция без Unity-зависимостей (кроме Vector3/Mathf)
      Data/         MaterialDatabase — реестр материалов
      Chemistry/    Mixture, SubstanceDatabase, ReactionEngine
      Combat/       ArmorModel (пробитие), Weapons
    Core/           Game (сервис-локатор), Boot (создание мира), PlayerController, SaveSystem
    World/          SandboxWorld, SandboxObject, WorldEnvironment
    Combat/         Projectile, WeaponSystem, ArmorPlate
    Chemistry/      FluidContainer (живые реакции в сосуде)
    Npc/            NpcActor с поведением и ragdoll
    Graphics/       RuntimeVisuals (материалы + процедурные меши), EffectsSystem, ScreenShake/CameraRig
    UI/             UIFactory, UIRoot, CatalogView — интерфейс под ПК и телефон
    Editor/         меню сборки и проверки проекта
  Resources/
    ElementaData/   elements.json, materials.json, substances.json, reactions.json, weapons.json
    Fonts/          Roboto (кириллица)
```

Ключевая идея: **вся физика и химия — в `Assets/Scripts/Sim`, без зависимости от Unity**.
Поэтому её можно тестировать без редактора и менять баланс, не открывая Unity.

### Как добавить материал

`Assets/Resources/ElementaData/materials.json`:

```json
{ "id": "my_alloy", "ru": "Мой сплав", "en": "My alloy", "cat": "metal",
  "density": 8100, "hardness": 2100, "strength": 900, "toughness": 55,
  "melt": 1750, "heat": 460, "cond": 40, "flamm": 0, "corr": 0.8,
  "elast": 0.25, "color": "#9FA8B4", "rough": 0.35, "metal": 1 }
```

Материал сразу появится в каталоге интерфейса — код менять не нужно.

### Как добавить реакцию

`Assets/Resources/ElementaData/reactions.json`:

```json
{ "id": "my_reaction", "ru": "Моя реакция",
  "reactants": { "hcl": 1, "naoh": 1 },
  "products": { "salt": 1, "water": 1 },
  "dh": -57.3, "temp": 273, "rate": 0.9,
  "effects": { "heat": 1, "fizz": 1 } }
```

## Проверки

```bash
python3 tools/datatest/verify_data.py     # валидация JSON-контента
python3 tools/import_check.py             # статическая проверка C#
dotnet run --project tools/simtest        # 71 тест физики/химии/баллистики
```

Все три проверки прогоняются в GitHub Actions на каждый пуш.

## Тесты

Физика, химия и целостность контента проверяются **77 автотестами** без Unity:

```bash
dotnet run --project tools/simtest/SimTest.csproj
```

Проверяется: загрузка баз, плотности и теплоёмкости, нейтрализация, выделение газа,
термит, детонация тротила, урон по телу, пробитие брони (10 мм стали, кевлар, композит,
наклонная плита, рикошет под 80°, РПГ), испарение, остывание.

CI на GitHub Actions гоняет эти тесты на каждый пуш.
