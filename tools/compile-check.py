#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Compila Assembly-CSharp (y Assembly-CSharp-Editor) con el Roslyn que trae Unity, SIN abrir el editor.

Por que existe: hasta ahora la unica forma de saber si el codigo compilaba era que
Diego abriera Unity. Esto da el mismo veredicto (mismos defines, mismas referencias)
en ~20 segundos desde la consola.

Como funciona: Unity deja en Library/Bee/artifacts/*.dag/Assembly-CSharp.rsp el
response file exacto con el que compila el assembly (defines, referencias, y la lista
de .cs). Reusamos ese rsp pero le REGENERAMOS la lista de fuentes con los .cs que
existen hoy, asi los archivos nuevos/borrados desde el ultimo refresh de Unity no
rompen la corrida.

Since spec 025 (D39) it also compiles the Editor code (every Assets/**/Editor/ folder outside an asmdef) with
Assembly-CSharp-Editor.rsp, against the game dll it just built, so menu tools, importers and EditMode tests are
checked too.

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
# editor=True devuelve justamente los de Editor/ (Assembly-CSharp-Editor).
def collect_sources(rsp_sources, editor=False):
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
        if ("Editor" in parts) != editor:
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


def compile_assembly(dotnet, csc, rsp, out_dir, out_name, editor, game_dll=None):
    """One assembly from Unity's own rsp with today's source list. Returns (errors, warnings, raw output)."""
    lines = io.open(rsp, encoding="utf-8").read().splitlines()
    source_line = re.compile(r'^"?(Assets/|Packages/).*\.cs"?$')
    rsp_sources = [l.strip().strip('"') for l in lines if source_line.match(l.strip())]
    other = [l for l in lines if not source_line.match(l.strip())]

    # No queremos que pise el dll bueno de Unity ni que escriba un ref.dll: compilamos
    # a un temporal y solo nos importan los diagnosticos.
    cleaned = []
    for l in other:
        s = l.strip()
        if s.startswith("-out:") or s.startswith("-refout:"):
            continue
        # the Editor assembly must see the game code compiled just now, not Unity's last build of it
        if game_dll and s.startswith("-r:") and "Assembly-CSharp.ref.dll" in s:
            l = '-r:"%s"' % game_dll.replace("\\", "/")
        cleaned.append(l)
    cleaned.insert(1, '-out:"%s"' % os.path.join(out_dir, out_name).replace("\\", "/"))

    sources = collect_sources(rsp_sources, editor)
    new_rsp = os.path.join(out_dir, out_name + ".rsp")
    with io.open(new_rsp, "w", encoding="utf-8") as fh:
        fh.write(u"\n".join(cleaned))
        fh.write(u"\n")
        for s in sources:
            fh.write(u'"%s"\n' % s)

    print("Compilando %d archivos .cs de %s (rsp base: %s)" % (len(sources), out_name, os.path.relpath(rsp, REPO)))
    proc = subprocess.Popen([dotnet, csc, "@" + new_rsp], cwd=REPO, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    out = proc.communicate()[0].decode("utf-8", "replace")
    errors = [l for l in out.splitlines() if ": error " in l]
    warnings = [l for l in out.splitlines() if ": warning " in l]
    return errors, warnings, out


def report(name, errors, warnings, out):
    if errors:
        print("\n=== %s: ERRORES (%d) ===" % (name, len(errors)))
        for e in errors:
            print(e)
    if warnings:
        print("\n=== %s: WARNINGS (%d, primeros 20) ===" % (name, len(warnings)))
        for w in warnings[:20]:
            print(w)
    if not errors and not warnings:
        print(out.strip()[:2000])


def main():
    data_root = unity_editor_root()
    dotnet = os.path.join(data_root, "NetCoreRuntime", "dotnet.exe")
    csc = os.path.join(data_root, "DotNetSdkRoslyn", "csc.dll")
    for p in (dotnet, csc):
        if not os.path.isfile(p):
            sys.exit("Falta %s (instalacion de Unity incompleta?)" % p)

    rsp = find_rsp()
    # tag opcional para que dos corridas en paralelo (agentes) no se pisen el .dll
    tag = sys.argv[1] if len(sys.argv) > 1 else "default"
    out_dir = os.path.join(REPO, "Temp", "compile-check", tag)
    if not os.path.isdir(out_dir):
        os.makedirs(out_dir)

    errors, warnings, out = compile_assembly(dotnet, csc, rsp, out_dir, "Assembly-CSharp.check.dll", editor=False)
    report("Assembly-CSharp", errors, warnings, out)

    editor_rsp = os.path.join(os.path.dirname(rsp), "Assembly-CSharp-Editor.rsp")
    editor_errors = []
    if errors:
        print("\n(Editor: no se compila porque el juego no compila)")
    elif not os.path.isfile(editor_rsp):
        print("\n(Editor: no hay Assembly-CSharp-Editor.rsp en la Library, se saltea)")
    else:
        game_dll = os.path.join(out_dir, "Assembly-CSharp.check.dll")
        editor_errors, ew, eo = compile_assembly(dotnet, csc, editor_rsp, out_dir, "Assembly-CSharp-Editor.check.dll",
                                                 editor=True, game_dll=game_dll)
        report("Assembly-CSharp-Editor", editor_errors, ew, eo)

    total = len(errors) + len(editor_errors)
    print("\nRESULTADO: %s" % ("FALLA (%d errores)" % total if total else "COMPILA OK (juego + Editor)"))
    return 1 if total else 0


if __name__ == "__main__":
    sys.exit(main())
