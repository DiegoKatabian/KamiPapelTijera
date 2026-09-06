#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Compila Assembly-CSharp con el Roslyn que trae Unity, SIN abrir el editor.

Por que existe: hasta ahora la unica forma de saber si el codigo compilaba era que
Diego abriera Unity. Esto da el mismo veredicto (mismos defines, mismas referencias)
en ~20 segundos desde la consola.

Como funciona: Unity deja en Library/Bee/artifacts/*.dag/Assembly-CSharp.rsp el
response file exacto con el que compila el assembly (defines, referencias, y la lista
de .cs). Reusamos ese rsp pero le REGENERAMOS la lista de fuentes con los .cs que
existen hoy, asi los archivos nuevos/borrados desde el ultimo refresh de Unity no
rompen la corrida.

Uso:
    python tools/compile-check.py [tag]   # tag = subcarpeta de salida, para correr en paralelo

Salida: los errores/warnings de compilacion y un exit code 0/1.
"""

import io
import os
import re
import subprocess
import sys
import glob

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# El editor con el que esta abierto el proyecto (ProjectSettings/ProjectVersion.txt).
def unity_editor_root():
    version_file = os.path.join(REPO, "ProjectSettings", "ProjectVersion.txt")
    version = "2021.3.12f1"
    if os.path.isfile(version_file):
        for line in io.open(version_file, encoding="utf-8"):
            if line.startswith("m_EditorVersion:"):
                version = line.split(":", 1)[1].strip()
                break
    candidates = [
        r"C:\Program Files\Unity\Hub\Editor\%s\Editor\Data" % version,
        r"C:\Program Files\Unity\Editor\Data",
    ]
    for c in candidates:
        if os.path.isdir(c):
            return c
    sys.exit("No encuentro el editor de Unity %s. Buscado en:\n  %s" % (version, "\n  ".join(candidates)))


def find_rsp():
    pattern = os.path.join(REPO, "Library", "Bee", "artifacts", "*.dag", "Assembly-CSharp.rsp")
    matches = sorted(glob.glob(pattern), key=os.path.getmtime, reverse=True)
    if not matches:
        sys.exit(
            "No hay Assembly-CSharp.rsp en Library/Bee/artifacts/.\n"
            "Abri el proyecto en Unity una vez (para que genere la Library) y volve a correr esto."
        )
    return matches[0]


# Los .cs que van a Assembly-CSharp: todo Assets/ menos lo que vive bajo un asmdef
# propio (Spine, packages embebidos, plugins con assembly definition) y menos Editor/.
def collect_sources(rsp_sources):
    asmdef_dirs = []
    for root, dirs, files in os.walk(os.path.join(REPO, "Assets")):
        for f in files:
            if f.endswith(".asmdef"):
                asmdef_dirs.append(os.path.abspath(root))
    sources = []
    for root, dirs, files in os.walk(os.path.join(REPO, "Assets")):
        abs_root = os.path.abspath(root)
        if any(abs_root == d or abs_root.startswith(d + os.sep) for d in asmdef_dirs):
            continue
        parts = abs_root.split(os.sep)
        if "Editor" in parts:
            continue
        for f in files:
            if f.endswith(".cs"):
                rel = os.path.relpath(os.path.join(root, f), REPO).replace("\\", "/")
                sources.append(rel)
    # Si el rsp original tenia fuentes fuera de Assets (raro), las conservamos.
    for s in rsp_sources:
        if not s.startswith("Assets/") and os.path.isfile(os.path.join(REPO, s)):
            sources.append(s)
    return sorted(set(sources))


def main():
    data_root = unity_editor_root()
    dotnet = os.path.join(data_root, "NetCoreRuntime", "dotnet.exe")
    csc = os.path.join(data_root, "DotNetSdkRoslyn", "csc.dll")
    for p in (dotnet, csc):
        if not os.path.isfile(p):
            sys.exit("Falta %s (instalacion de Unity incompleta?)" % p)

    rsp = find_rsp()
    lines = io.open(rsp, encoding="utf-8").read().splitlines()

    source_line = re.compile(r'^"?(Assets/|Packages/).*\.cs"?$')
    rsp_sources = [l.strip().strip('"') for l in lines if source_line.match(l.strip())]
    other = [l for l in lines if not source_line.match(l.strip())]

    # No queremos que pise el dll bueno de Unity ni que escriba un ref.dll: compilamos
    # a un temporal y solo nos importan los diagnosticos.
    # tag opcional para que dos corridas en paralelo (agentes) no se pisen el .dll
    tag = sys.argv[1] if len(sys.argv) > 1 else "default"
    out_dir = os.path.join(REPO, "Temp", "compile-check", tag)
    if not os.path.isdir(out_dir):
        os.makedirs(out_dir)
    cleaned = []
    for l in other:
        s = l.strip()
        if s.startswith("-out:") or s.startswith("-refout:"):
            continue
        cleaned.append(l)
    cleaned.insert(1, '-out:"%s"' % os.path.join(out_dir, "Assembly-CSharp.check.dll").replace("\\", "/"))

    sources = collect_sources(rsp_sources)
    new_rsp = os.path.join(out_dir, "Assembly-CSharp.check.rsp")
    with io.open(new_rsp, "w", encoding="utf-8") as fh:
        fh.write(u"\n".join(cleaned))
        fh.write(u"\n")
        for s in sources:
            fh.write(u'"%s"\n' % s)

    print("Compilando %d archivos .cs (rsp base: %s)" % (len(sources), os.path.relpath(rsp, REPO)))
    proc = subprocess.Popen(
        [dotnet, csc, "@" + new_rsp],
        cwd=REPO,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
    )
    out = proc.communicate()[0].decode("utf-8", "replace")

    errors = [l for l in out.splitlines() if ": error " in l]
    warnings = [l for l in out.splitlines() if ": warning " in l]
    if errors:
        print("\n=== ERRORES (%d) ===" % len(errors))
        for e in errors:
            print(e)
    if warnings:
        print("\n=== WARNINGS (%d, primeros 20) ===" % len(warnings))
        for w in warnings[:20]:
            print(w)
    if not errors and not warnings:
        print(out.strip()[:2000])

    print("\nRESULTADO: %s" % ("FALLA (%d errores)" % len(errors) if errors else "COMPILA OK"))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
