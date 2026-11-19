#!/usr/bin/env python3
"""Sanity checks for the content JSON files (run in CI)."""
import json, sys, math

BASE = "Assets/Resources/ElementaData"
errors = []

def load(name):
    with open(f"{BASE}/{name}.json", encoding="utf-8") as f:
        return json.load(f)

elements = load("elements")["elements"]
materials = load("materials")["materials"]
substances = load("substances")["substances"]
reactions = load("reactions")["reactions"]
weapons = load("weapons")["weapons"]

if len(elements) != 118:
    errors.append(f"ожидалось 118 элементов, найдено {len(elements)}")
zs = [e["z"] for e in elements]
if zs != list(range(1, 119)):
    errors.append("нарушена нумерация атомных номеров")
for e in elements:
    if e["mass"] <= 0: errors.append(f"элемент {e['sym']}: масса <= 0")
    if not (0 <= e["density"] < 25000): errors.append(f"элемент {e['sym']}: странная плотность {e['density']}")
    # arsenic sublimes (solid -> gas), so its sublimation point is below the melting point
    if e["melt"] and e["boil"] and e["boil"] < e["melt"] and e["sym"] not in ("As",):
        errors.append(f"элемент {e['sym']}: кипение ниже плавления")

ids = [m["id"] for m in materials]
if len(ids) != len(set(ids)): errors.append("дубликаты id материалов")
for m in materials:
    if m["density"] <= 0: errors.append(f"материал {m['id']}: плотность <= 0")
    if m["heat"] <= 0: errors.append(f"материал {m['id']}: теплоёмкость <= 0")
    if not (0 <= m["flamm"] <= 1): errors.append(f"материал {m['id']}: воспламеняемость вне 0..1")
    if not (0 <= m["corr"] <= 1): errors.append(f"материал {m['id']}: коррозионная стойкость вне 0..1")
    if not (m["color"].startswith("#") and len(m["color"]) == 7): errors.append(f"материал {m['id']}: цвет не #RRGGBB")
    # water ice floats, steel sinks and tungsten is densest of the common metals
if not (materials[[m["id"] for m in materials].index("ice")]["density"] < 1000):
    errors.append("лёд должен быть легче воды")

sub_ids = {s["id"] for s in substances}
if len(sub_ids) != len(substances): errors.append("дубликаты id веществ")
for s in substances:
    if s["molar"] <= 0: errors.append(f"вещество {s['id']}: молярная масса <= 0")
    if not (0 <= s["ph"] <= 14): errors.append(f"вещество {s['id']}: pH вне 0..14")
    if s["state"] not in ("solid", "liquid", "gas"): errors.append(f"вещество {s['id']}: состояние {s['state']}")

for r in reactions:
    for side in ("reactants", "products"):
        for key, coef in r[side].items():
            if key not in sub_ids: errors.append(f"реакция {r['id']}: неизвестное вещество {key}")
            if coef < 0: errors.append(f"реакция {r['id']}: отрицательный коэффициент {key}")
    if not r["reactants"]: errors.append(f"реакция {r['id']}: нет реагентов")
    gas = r.get("effects", {}).get("gas")
    if gas and gas not in sub_ids: errors.append(f"реакция {r['id']}: неизвестный газ {gas}")

# key physical expectations
def find(mat_id): return next(m for m in materials if m["id"] == mat_id)
if not find("tungsten")["density"] > find("lead")["density"] > find("steel_1045")["density"] > find("aluminium")["density"]:
    errors.append("нарушен порядок плотностей металлов")
if not find("kevlar")["hardness"] > find("concrete")["hardness"]:
    errors.append("кевлар должен быть стойче бетона")

for w in weapons:
    if w["velocity"] <= 0 or w["mass"] <= 0: errors.append(f"оружие {w['id']}: невалидная масса/скорость")
    if w["kind"] not in ("bullet", "ap", "incendiary", "explosive", "shaped", "frag", "arrow", "laser",
                        "blunt", "blade", "pierce", "shock", "freeze", "poison", "flash", "smoke"):
        errors.append(f"оружие {w['id']}: неизвестный тип {w['kind']}")

print(f"элементов: {len(elements)}, материалов: {len(materials)}, веществ: {len(substances)}, "
      f"реакций: {len(reactions)}, оружий: {len(weapons)}")
if errors:
    print("ОШИБКИ ДАННЫХ:")
    for e in errors[:40]:
        print("  -", e)
    sys.exit(1)
print("Данные валидны.")
