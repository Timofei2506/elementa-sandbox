#!/usr/bin/env python3
"""Static check of C# sources: namespaces, using directives, obvious typos."""
import os, re, sys

def strip_literals(src):
    """Blank out comments, strings and char literals so brace counting is reliable."""
    out = []
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        if c == "/" and i + 1 < n and src[i + 1] == "/":
            while i < n and src[i] != "\n":
                i += 1
        elif c == "/" and i + 1 < n and src[i + 1] == "*":
            i += 2
            while i + 1 < n and not (src[i] == "*" and src[i + 1] == "/"):
                i += 1
            i += 2
        elif c == '"':
            i += 1
            while i < n and src[i] != '"':
                if src[i] == "\\":
                    i += 1
                i += 1
            i += 1
        elif c == "'":
            i += 1
            while i < n and src[i] != "'":
                if src[i] == "\\":
                    i += 1
                i += 1
            i += 1
        else:
            out.append(c)
            i += 1
    return "".join(out)

root = "Assets/Scripts"
errors = []
files = []
for base, _, names in os.walk(root):
    for n in names:
        if n.endswith(".cs"):
            files.append(os.path.join(base, n))

# collect declared types per file
decl = {}
for f in files:
    src = open(f, encoding="utf-8").read()
    ns = re.search(r"namespace\s+([\w\.]+)", src)
    for m in re.finditer(r"\b(?:public|internal|private)\s+(?:static\s+|sealed\s+|abstract\s+|partial\s+)*(class|struct|enum|interface)\s+(\w+)", src):
        decl.setdefault(m.group(2), set()).add(ns.group(1) if ns else "")

for f in files:
    src = open(f, encoding="utf-8").read()
    code = strip_literals(src)
    if code.count("{") != code.count("}"):
        errors.append(f"{f}: несбалансированные скобки {{}} {code.count('{')}/{code.count('}')}")
    if code.count("(") != code.count(")"):
        errors.append(f"{f}: несбалансированные скобки () {code.count('(')}/{code.count(')')}")
    # namespaces used must be imported
    used = set(re.findall(r"\b(Elementa\.Sim|UnityEngine\.UI|UnityEngine\.EventSystems|UnityEngine\.Rendering|UnityEditor\.[\w\.]+)\b", src))
    for u in used:
        head = ".".join(u.split(".")[:2])
        if head not in src and f"namespace {u}" not in src:
            errors.append(f"{f}: используется {u} без using")
    if "\t" in src:
        errors.append(f"{f}: символ табуляции в отступе")

print(f"проверено файлов: {len(files)}, объявлено типов: {len(decl)}")
if errors:
    print("ПРОБЛЕМЫ:")
    for e in errors[:30]:
        print("  -", e)
    sys.exit(1)
print("Статическая проверка пройдена.")
