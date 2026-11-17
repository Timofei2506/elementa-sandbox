# Добавление контента

## Материал
`schema`: `id, ru, en, cat, density(кг/м³), hardness(МПа), strength(МПа), toughness(Дж), melt(K), heat(Дж/кг·К), cond(Вт/м·К), flamm(0..1), corr(0..1), elast(0..1), color(#hex), rough(0..1), metal(0..1)`

Категории: `metal, polymer, ceramic, composite, mineral, wood, organic`.

## Вещество
`schema`: `id, ru, en, formula, state(solid|liquid|gas), density, color, ph, soluble, flammable, oxidizer, ignition(K), toxicity, corrosivity, molar(г/моль), energy(МДж/кг)`

## Реакция
`schema`: `id, ru, reactants{id:коэф}, products{id:коэф}, dh(кДж/моль, минус = экзотермическая), temp(K активации), catalyst, rate, effects{}`

Эффекты: `explosion, fire, heat, light, smoke, shockwave, flash, fizz, corrode, color_shift, unstable, toxic, molten, sparks, gas, precipitate`.

## Оружие
`schema`: `id, ru, cat, kind(bullet|ap|incendiary|explosive|shaped|frag|arrow|laser), mass(г), velocity(м/с), caliber(мм), rpm, accuracy(0..1), recoil, auto, mag`

Пробитие считается как `sqrt(КЭ) * 0.030 * sqrt(калибр) * множитель типа`;
для кумулятивных зарядов — `калибр * 5.5` мм RHA.
