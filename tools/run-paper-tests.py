"""Compiles the paper page core (Assets/Scripts/Book/Paper/Core/, no UnityEngine) with the test files in
tools/paper-tests/ and runs them, outside Unity (spec 025 D38). Uses Unity's own Roslyn (DotNetSdkRoslyn/csc.dll on its
bundled dotnet) and runs the exe on its bundled Mono, so nothing needs installing. Regenerates the goldens first.

    python tools/run-paper-tests.py

Exit code 0 = all tests passed.
"""
import glob
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, 'tools'))


def unity_data():
    version = '2021.3.12f1'
    for line in open(os.path.join(ROOT, 'ProjectSettings', 'ProjectVersion.txt'), encoding='utf-8'):
        if line.startswith('m_EditorVersion:'):
            version = line.split(':', 1)[1].strip()
    for c in (rf'C:\Program Files\Unity\Hub\Editor\{version}\Editor\Data', r'C:\Program Files\Unity\Editor\Data'):
        if os.path.isdir(c):
            return c
    sys.exit(f'Unity {version} not found')


def main():
    r = subprocess.run([sys.executable, os.path.join(ROOT, 'tools', 'make-paper-goldens.py')], cwd=ROOT)
    if r.returncode != 0:
        sys.exit('goldens failed')
    data = unity_data()
    dotnet = os.path.join(data, 'NetCoreRuntime', 'dotnet.exe')
    csc = os.path.join(data, 'DotNetSdkRoslyn', 'csc.dll')
    mono = os.path.join(data, 'MonoBleedingEdge', 'bin', 'mono.exe')
    api = os.path.join(data, 'MonoBleedingEdge', 'lib', 'mono', '4.7.1-api')
    out_dir = os.path.join(ROOT, 'Temp', 'paper-tests')
    os.makedirs(out_dir, exist_ok=True)
    exe = os.path.join(out_dir, 'PaperTests.exe')
    sources = sorted(glob.glob(os.path.join(ROOT, 'Assets', 'Scripts', 'Book', 'Paper', 'Core', '*.cs')))
    sources += sorted(glob.glob(os.path.join(ROOT, 'tools', 'paper-tests', '*.cs')))
    refs = [os.path.join(api, n) for n in ('mscorlib.dll', 'System.dll', 'System.Core.dll')]
    cmd = [dotnet, csc, '-nologo', '-nostdlib', '-langversion:9', '-warnaserror-', f'-out:{exe}'] + \
          [f'-r:{r}' for r in refs] + sources
    c = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True)
    if c.returncode != 0:
        print(c.stdout + c.stderr)
        sys.exit('[run-paper-tests] COMPILE FAILED')
    t = subprocess.run([mono, exe], cwd=ROOT, capture_output=True, text=True)
    print(t.stdout + t.stderr)
    print('[run-paper-tests] ' + ('ALL PASSED' if t.returncode == 0 else 'FAILED'))
    return t.returncode


if __name__ == '__main__':
    sys.exit(main())
