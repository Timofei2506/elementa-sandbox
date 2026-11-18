# Лицензия Unity для сборки в облаке (Personal, бесплатно)

Сборка `.exe`/`.apk` идёт в GitHub Actions (GameCI). Для неё нужна **активная лицензия**:
логина и пароля недостаточно — Unity требует файл `.ulf`. Есть два пути, оба бесплатные.

| Путь | Что делаешь | Что получаешь |
|---|---|---|
| **A. Через Actions (быстрее)** | запускаешь workflow `Unity license` → `request-file`, качаешь `.alf`, загружаешь его на страницу Unity | `.ulf` за 2 минуты в браузере |
| **B. Через Unity Hub (надёжнее)** | ставишь Unity Hub, входишь под аккаунтом для CI, «Get a free personal license» | `Unity_lic.ulf` уже лежит на диске, никуда загружать не надо |

В обоих случаях в GitHub нужны **три секрета**: `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`.

---

## Шаг 1. Добавь e-mail и пароль

GitHub → репозиторий → **Settings → Secrets and variables → Actions → New repository secret**:

| Имя | Значение |
|---|---|
| `UNITY_EMAIL` | e-mail аккаунта Unity |
| `UNITY_PASSWORD` | пароль **без спецсимволов** (`"`, `\`, `$` ломают GameCI) |

`UNITY_LICENSE` пока не добавляй — его получим на шаге 2.

> Хочешь отдельный «сервисный» аккаунт только для CI — так безопаснее: основной аккаунт
> останется только у тебя, и сборка не будет отбирать место у твоей локальной Unity.

## Шаг 2 (вариант A). Файл `.alf` из Actions

1. **Actions → Unity license → Run workflow**
2. `action` = **request-file** → **Run workflow**
3. Дождись зелёной галочки (~2–3 минуты: сначала качается образ Unity ~4 ГБ)
4. Внизу страницы запуска → **Artifacts** → **Unity-Activation-File**
   (внутри `Unity_v6000.0.85f2.alf`)

Артефакт хранится 7 дней — скачай сразу.

## Шаг 2 (вариант B). Файл `.ulf` из Unity Hub

1. Скачай и поставь [Unity Hub](https://unity.com/download)
2. Войди под **тем же** аккаунтом, чьи e-mail/пароль в секретах
3. `Preferences` → `Licenses` → **Add** → **Get a free personal license**
   (важно именно нажать **Add** — иначе файл может не создаться)
4. Забери файл лицензии:
   * Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   * macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   * Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
5. **Выйди из Unity Hub** (это возвращает место лицензии, чтобы CI его получил)

## Шаг 3. Получи `.ulf` в браузере (только для варианта A)

1. Открой **https://license.unity3d.com/manual**
2. Загрузи `.alf` из шага 2
3. Выбери **Unity Personal** → «I don't use Unity in a professional capacity» →
   категорию дохода (до 100 000 $) → **Next**
4. Скачай файл **`.ulf`** (например `Unity_v6000.x.ulf`)

Если страница пишет, что файл не подходит или «no seats» — просто используй вариант B.

## Шаг 4. Добавь `.ulf` в секреты

1. Открой `.ulf` блокнотом — это обычный XML (одна строка, начинается с `<?xml`)
2. Скопируй **всё содержимое** целиком, до последнего `</root>`
3. GitHub → Settings → Secrets → **New repository secret**
   * Name: `UNITY_LICENSE`
   * Secret: вставь скопированный XML

## Шаг 5. Проверь лицензию

**Actions → Unity license → Run workflow → action = check-license → Run**.

Что делает проверка: печатает длину и даты из `.ulf`, затем активирует лицензию в
контейнере `unityci/editor` через `game-ci/unity-activate` и возвращает место обратно.
Зелёная галочка = лицензия рабочая.

## Шаг 6. Собери `.exe` и `.apk`

**Actions → Build (Unity) → Run workflow** (или просто пуш в `main`).

Через ~15–25 минут внизу запуска появятся артефакты:

* `ELEMENTA-StandaloneWindows64` — папка с `ELEMENTA.exe`
* `ELEMENTA-Android` — `ELEMENTA.apk`

При пуше тега вида `v1.0.0` оба файла дополнительно прикрепляются к GitHub Release.

---

## Важно про места лицензии

У Personal-лицензии одно «место». Если Unity Hub открыт у тебя на компьютере под тем же
аккаунтом, CI может не получить место:

* выйди из Unity Hub на ПК, пока идёт сборка, **или**
* заведи отдельный аккаунт Unity только для CI (рекомендую).

Слот освобождается в [Unity ID](https://id.unity.com/) → *Seats / Activations*.

## Если что-то пошло не так

| Симптом | Решение |
|---|---|
| `Unable to get a valid Unity license` | `UNITY_LICENSE` содержит не весь XML или файл от другой версии — повтори шаги 2–4 |
| `Activation failed: no seats available` | выйди из Unity Hub на ПК, освободи слот в Unity ID |
| `.alf` не создался | повтори запуск `request-file`; в логе контейнера видно причину |
| `UNITY_PASSWORD` не принимается | смени пароль на буквы/цифры без спецсимволов |
| Сборка падает на `Library`/`IL2CPP` | это уже не лицензия — смотри лог шага `Build project`, кэш `Library` чистится удалением кэша Actions |

## Альтернатива: лицензия без `.alf`/`.ulf`

Если появится **Unity Pro/Plus** — вместо `.ulf` используются секреты
`UNITY_SERIAL` (вида `XX-XXXX-XXXX-XXXX-XXXX-XXXX`) + `UNITY_EMAIL` + `UNITY_PASSWORD`,
и вся возня с файлами активации не нужна.

## Не отправляй пароль в чат

Пароль и e-mail добавляй **только** в GitHub Secrets. Если пришлёшь их в чат — они попадут
в историю переписки. Технически я могу загрузить секреты через API, но делать это стоит
только с «сервисным» аккаунтом Unity, а не с основным.
