# Сборка в GitHub Actions

В репозитории два workflow:

| Workflow | Что делает | Секреты |
|---|---|---|
| `.github/workflows/test.yml` | валидация данных, статические проверки, **77 тестов** физики/химии | не нужны |
| `.github/workflows/build-unity.yml` | собирает **Windows `.exe`** и **Android `.apk`** через GameCI | `UNITY_EMAIL`, `UNITY_PASSWORD` |

Тестовый workflow работает сразу. Билд-воркфлоу без секретов не падает: он честно пишет
предупреждение в лог и пропускает сборку Unity.

---

## 1. Лицензия Unity для CI

Unity **отменила offline-активацию Personal-лицензий**: файлы `.alf`/`.ulf` больше не выпускаются,
а `.ulf` из Unity Hub привязан к машине и в облаке отклоняется. Поэтому лицензия получается
автоматически по логину и паролю аккаунта (Unity licensing client через game-ci CLI).
Подробный разбор: **[UNITY_LICENSE_CI.md](UNITY_LICENSE_CI.md)**.

Что нужно один раз: отключить в аккаунте 2FA, взять пароль без спецсимволов и освободить
старые активации на [id.unity.com](https://id.unity.com/) (*Seats / Activations*).

## 2. Добавить секреты

GitHub → репозиторий → **Settings → Secrets and variables → Actions → New repository secret**:

| Имя | Значение |
|---|---|
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
| `Invalid credentials` / `Machine bindings don't match` | неверный пароль, включена 2FA или остался старый секрет `UNITY_LICENSE` — удали его |
| `no seats available` | заняты все места аккаунта: сними старые активации на id.unity.com |
| `No space left on device` | образ Android большой; шаг «Освободить диск» уже есть, при повторе уменьши `Library`-кэш |
| `Android build requires IL2CPP` | не применяется автонастройка — прогони меню **ELEMENTA → 2. Проверить проект** локально |
| `Shader 'Standard' not found` | сработал strip: автонастройка должна была добавить его в Always Included, проверь `ProjectSettings/GraphicsSettings.asset` |
| Долго (60+ мин) | первый билд всегда медленный (импорт + IL2CPP), дальше работает кэш |

## 6. Локальная сборка без CI

Unity Hub → **ELEMENTA → 3. Собрать Windows (.exe)** / **4. Собрать Android (.apk)**.
Секреты для этого не нужны вообще.

## 6. Готовые файлы

* **GitHub Release (рекомендую):** https://github.com/Timofei2506/elementa-sandbox/releases
  — тег `v0.1.0` содержит `ELEMENTA-StandaloneWindows64.zip` и `ELEMENTA-Android.zip`.
* **Артефакты последнего запуска:** https://github.com/Timofei2506/elementa-sandbox/actions/workflows/build-unity.yml

Как выпускать новые версии: `git tag v0.1.1 && git push origin v0.1.1` — сборка запустится
сама и прикрепит архивы к новому релизу.
