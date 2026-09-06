#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Genera el .meta de un script o carpeta nueva con un GUID random verificado sin colisiones.

Unity los genera solo al refrescar, pero si el editor no tiene foco puede tardar muchisimo:
esto desbloquea trabajar desde consola. Unity adopta el .meta tal cual al refrescar.

Uso:  python tools/make-meta.py Assets/Scripts/Input Assets/Scripts/Input/InputHub.cs
"""
import io, os, subprocess, sys, uuid

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

FOLDER_META = u"""fileFormatVersion: 2
guid: %s
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:\x20
  assetBundleName:\x20
  assetBundleVariant:\x20
"""

SCRIPT_META = u"""fileFormatVersion: 2
guid: %s
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:\x20
  assetBundleName:\x20
  assetBundleVariant:\x20
"""


def guid_libre():
    while True:
        g = uuid.uuid4().hex
        # grep sobre Assets/ y ProjectSettings/: si aparece, sorteamos otro
        r = subprocess.call(["grep", "-rqs", g, "Assets", "ProjectSettings"], cwd=REPO)
        if r != 0:
            return g


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    for rel in sys.argv[1:]:
        path = os.path.join(REPO, rel)
        meta = path + ".meta"
        if os.path.exists(meta):
            print("ya existe, salteo: %s.meta" % rel)
            continue
        if not os.path.exists(path):
            sys.exit("no existe: %s" % rel)
        g = guid_libre()
        tpl = FOLDER_META if os.path.isdir(path) else SCRIPT_META
        io.open(meta, "w", encoding="utf-8", newline="\n").write(tpl % g)
        print("creado %s.meta  guid=%s" % (rel, g))


if __name__ == "__main__":
    main()
