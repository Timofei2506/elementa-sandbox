# Пошаговая установка и сборка

## 1. Unity
1. Скачай **Unity Hub**: https://unity.com/download
2. В Hub → *Installs* → *Install Editor* → выбери **6000.0.85f2 LTS**
   (любая версия из ветки **6000.0.x LTS** тоже подойдёт).
3. При установке отметь модули:
   * **Windows Build Support (IL2CPP)** — для `.exe`
   * **Android Build Support** + **OpenJDK** + **Android SDK & NDK Tools** — для `.apk`

## 2. Открытие проекта
1. Unity Hub → **Add** → **Add project from disk** → папка `elementa-sandbox`
2. Открой проект. Первый импорт займёт 1–3 минуты.
3. Меню **ELEMENTA → 1. Создать главную сцену**
4. **Play** ▶

Если в Console есть красные ошибки — меню **ELEMENTA → 2. Проверить проект**.

## 3. Сборка Windows
**ELEMENTA → 3. Собрать Windows (.exe)** → выбери папку → получишь `ELEMENTA.exe`.

## 4. Сборка Android
**ELEMENTA → 4. Собрать Android (.apk)** → выбери папку → `ELEMENTA.apk`.
Для установки на телефон: включи «Установка из неизвестных источников», скопируй APK и установи.
Управление на телефоне: один палец — камера, два — зум, тап — инструмент.

## 5. Про секреты Unity
**Никакие логин/пароль в репозиторий вставлять не нужно** — проект собирается локально
через обычный Unity Hub, аккаунт нужен только для активации лицензии в самом Hub.

Секреты (в GitHub → Settings → Secrets and variables → Actions) требуются только если
захочешь **собирать автоматически в облаке**. Тогда нужны:

| Secret | Что это | Как получить |
|---|---|---|
| `UNITY_LICENSE` | XML-файл лицензии (.ulf) | на Windows: `C:\ProgramData\Unity\Unity_lic.ulf`, на macOS: `~/Library/Application Support/Unity/Unity_lic.ulf` |
| `UNITY_EMAIL` | e-mail аккаунта Unity | — |
| `UNITY_PASSWORD` | пароль аккаунта Unity | — |

Для Unity Personal лицензию получают командой
`Unity -quit -batchmode -createManualActivationFile` и загрузкой `.alf` на https://license.unity3d.com/manual.

**Не отправляй пароль от Unity в чат.** Если когда-нибудь понадобится CI — заведи
отдельный «сервисный» аккаунт Unity и храни его данные только в GitHub Secrets.

## 6. Частые вопросы
* **Чёрный экран / пусто** — нажмите *Play* в сцене `Assets/Scenes/Main.unity` (весь мир создаётся в рантайме).
* **Нет звука** — проверь кнопку «Звук» в верхней панели.
* **Тормозит на телефоне** — Project Settings → Quality → выбери *Low* или *Medium*.
* **Долгая сборка** — Project Settings → Player → *Managed Stripping Level: Low*.
