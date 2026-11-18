# Лицензия Unity для сборки в облаке (Personal, бесплатно)

**Коротко:** нужны **два секрета** — `UNITY_EMAIL` и `UNITY_PASSWORD`. Файл лицензии
`.ulf` больше не нужен и не поможет: Unity **отменила offline-активацию Personal-лицензий**
(страница `license.unity3d.com/manual` теперь пишет «Offline activation is available only for
Enterprise and Industry seats», а `.alf`/`.ulf` для бесплатной лицензии получить нельзя).

Как это работает в CI: `game-ci` CLI сам просит место у службы лицензий Unity через
Unity licensing client (`--activate-all --include-personal`), используя логин и пароль
аккаунта, и возвращает место в конце работы.

### Почему нельзя по-старому

| Путь | Что происходит |
|---|---|
| `.alf` → `license.unity3d.com/manual` → `.ulf` | страница отвечает «Offline activation is available only for Enterprise and Industry seats» — для Personal закрыто |
| `.ulf` из Unity Hub → секрет `UNITY_LICENSE` | файл привязан к машине, которая его создала; в облаке Unity отвечает `Machine bindings don't match` (код 400) |
| `-manualLicenseFile` с тем же `.ulf` | та же ошибка привязки |
| **`UNITY_EMAIL` + `UNITY_PASSWORD`** | **работает**: CI получает место на время джобы и возвращает его |

---

## Шаг 1. Подготовь аккаунт Unity

1. Аккаунт на [id.unity.com](https://id.unity.com/) (бесплатный, Personal).
2. **Отключи двухфакторную аутентификацию (2FA)** — headless-активация не умеет её проходить.
   Если 2FA включена, CI упадёт с ошибкой входа.
3. Пароль лучше сделать **без спецсимволов** (`"`, `\`, `$` ломают передачу в контейнер):
   смешанный регистр + цифры.
4. Освободи место: [id.unity.com](https://id.unity.com/) → *Seats / Activations* → снять старые
   активации. У Personal их всего несколько, а после каждого прогона CI место возвращается,
   но зависшие прогоны могут оставить место занятым.

## Шаг 2. Добавь два секрета

Прямая ссылка: **<https://github.com/Timofei2506/elementa-sandbox/settings/secrets/actions/new>**

| Имя | Значение |
|---|---|
| `UNITY_EMAIL` | e-mail аккаунта Unity |
| `UNITY_PASSWORD` | пароль этого аккаунта |

`UNITY_LICENSE` **не нужен** — если он у тебя уже есть в секретах, его можно удалить.

## Шаг 3. Проверь лицензию

**Actions → Unity license → Run workflow**.

Workflow печатает логин и длину пароля, ставит `game-ci` CLI, вызывает
`game-ci activate .` (получение места) и в конце всегда делает `game-ci return-license .`.

* зелёная галочка → лицензия работает;
* `Invalid credentials` → неверный e-mail/пароль или 2FA;
* `no seats available` → освободи места в Unity ID (шаг 1.4).

## Шаг 4. Собери `.exe` и `.apk`

**Actions → Build (Unity) → Run workflow** (или обычный пуш в `main`).

Через ~15–25 минут внизу запуска появятся артефакты:

* `ELEMENTA-StandaloneWindows64` — папка с `ELEMENTA.exe`
* `ELEMENTA-Android` — `ELEMENTA.apk`

При пуше тега вида `v1.0.0` оба архива дополнительно прикрепляются к GitHub Release.

---

## Если что-то пошло не так

| Симптом | Причина и решение |
|---|---|
| `Offline activation is available only for Enterprise and Industry seats` | это и есть та самая страница `.alf` — она для Personal больше не работает, файлы не нужны |
| `Machine bindings don't match` / код 400 | использован `.ulf` из Unity Hub — удали секрет `UNITY_LICENSE`, активация идёт по логину и паролю |
| `Invalid credentials` / `Access token is unavailable` | неверный пароль, включена 2FA или в пароле спецсимволы |
| `no seats available` | освободи активации на [id.unity.com](https://id.unity.com/) |
| Прогон отменён на середине | место могло остаться занятым: запусти `Unity license` ещё раз — он вернёт место в конце, либо сними активацию вручную в Unity ID |

## Альтернатива: Unity Pro/Plus

Если появится платная подписка — вместо логина/пароля используется серийный ключ:
секрет `UNITY_SERIAL` (`XX-XXXX-XXXX-XXXX-XXXX-XXXX`) + `UNITY_EMAIL` + `UNITY_PASSWORD`.

## Не отправляй пароль в чат

Пароль и e-mail добавляй **только** в GitHub Secrets. Если пришлёшь их в чат — они попадут
в историю переписки. Технически я могу загрузить секреты через API, но делать это стоит
только с «сервисным» аккаунтом Unity, а не с основным.
