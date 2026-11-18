# Сборка в GitHub Actions

В репозитории два workflow:

| Workflow | Что делает | Секреты |
|---|---|---|
| `.github/workflows/test.yml` | валидация данных, статические проверки, **77 тестов** физики/химии | не нужны |
| `.github/workflows/build-unity.yml` | собирает **Windows `.exe`** и **Android `.apk`** через GameCI | `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` |

Тестовый workflow работает сразу. Билд-воркфлоу без секретов не падает: он честно пишет
предупреждение в лог и пропускает сборку Unity.

---

## 1. Получить лицензию Unity для CI (один раз, ~5 минут)

1. Установи **Unity Hub** и войди тем аккаунтом, от которого будешь собирать.
   Лучше завести **отдельный аккаунт для CI**, чтобы не выбивать себя из локальной сессии.
2. В Hub: **Preferences → Licenses → Add → Get a free personal license**.
   Важно нажать **Add** и пройти активацию — иначе файл лицензии не создастся.
3. Забери файл лицензии:
   * **Windows:** `C:\ProgramData\Unity\Unity_lic.ulf`
   * **macOS:** `/Library/Application Support/Unity/Unity_lic.ulf`
   * **Linux:** `~/.local/share/unity3d/Unity/Unity_lic.ulf`
4. Выйди из аккаунта в Hub (это возвращает место лицензии, чтобы CI смог занять его).

> Если файла нет: `Unity -quit -batchmode -createManualActivationFile` создаст `.alf`,
> загрузи его на https://license.unity3d.com/manual и скачай `.ulf`.

## 2. Добавить секреты

GitHub → репозиторий → **Settings → Secrets and variables → Actions → New repository secret**:

| Имя | Значение |
|---|---|
| `UNITY_LICENSE` | **всё содержимое** файла `Unity_lic.ulf` (открыть блокнотом, скопировать целиком, вместе с `<?xml ...>` и закрывающим тегом) |
| `UNITY_EMAIL` | e-mail аккаунта Unity |
| `UNITY_PASSWORD` | пароль **без спецсимволов** (GameCI не любит `"`, `\`, `$`) |

## 3. Запустить сборку

* Автоматически: любой пуш в `main` (кроме правок только в `*.md` и `docs/**`).
* Вручную: **Actions → Build (Unity) → Run workflow**, можно выбрать `all`, `windows` или `android`.
* Релиз: поставь тег — `git tag v0.1.0 && git push origin v0.1.0` — собранные архивы
  прикрепятся к GitHub Release.

Готовые файлы лежат в **Actions → выбранный запуск → Artifacts**:
`ELEMENTA-StandaloneWindows64` (распаковать → `ELEMENTA.exe`) и
`ELEMENTA-Android` (`ELEMENTA.apk`).

## 4. Что уже настроено в проекте под CI

Чтобы сборка не упала на настройках, всё сделано кодом:

* `Assets/Scenes/Main.unity` есть в репозитории и прописан в `ProjectSettings/EditorBuildSettings.asset`;
* `Assets/Scripts/Editor/ProjectAutoSetup.cs` при открытии проекта в редакторе (в том числе в контейнере CI)
  автоматически выставляет: имя компании и продукта, бандл-ID `com.timofei2506.elementa`,
  версию, **Linear color space**, для Android — **IL2CPP + ARM64 + minSdk 24**,
  и добавляет шейдеры (`Standard`, `Sprites/Default`, `Skybox/Panoramic`, `Unlit/Color`, `UI/Default`)
  в *Always Included Shaders* — иначе `Shader.Find` в билде вернул бы `null` и графика почернела;
* кэш `Library/` по платформе — повторные сборки идут в 2–3 раза быстрее.

## 5. Если сборка упала

| Ошибка в логе | Причина и решение |
|---|---|
| `No valid Unity Editor license found` | неверный/просроченный `UNITY_LICENSE`; перегенерируй `.ulf` и обнови секрет |
| `Invalid serial` / `Activation failed` | занято место лицензии: выйди из Unity Hub, где она активирована |
| `No space left on device` | образ Android большой; шаг «Освободить диск» уже есть, при повторе уменьши `Library`-кэш |
| `Android build requires IL2CPP` | не применяется автонастройка — прогони меню **ELEMENTA → 2. Проверить проект** локально |
| `Shader 'Standard' not found` | сработал strip: автонастройка должна была добавить его в Always Included, проверь `ProjectSettings/GraphicsSettings.asset` |
| Долго (60+ мин) | первый билд всегда медленный (импорт + IL2CPP), дальше работает кэш |

## 6. Локальная сборка без CI

Unity Hub → **ELEMENTA → 3. Собрать Windows (.exe)** / **4. Собрать Android (.apk)**.
Секреты для этого не нужны вообще.
