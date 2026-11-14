#!/usr/bin/env python3
"""Build ASTRA Academy (Hub, L0, L1) as Vision Aura headset apps for macOS ARM64.

Creates/updates an isolated Unity project OUTSIDE the repository (the canonical project is not touched),
adds URP + XR Management + the local com.visionaura.xr package, syncs Assets/ASTRA, regenerates P1 scenes,
optionally runs the Unity test suites, builds "ASTRA Academy AR.app" / "ASTRA Academy VR.app" into
Builds/VisionAura (git-ignored) and ad-hoc signs the finished apps.

Usage:
  python3 .agents/build/build_vision_mac.py [--project DIR] [--vision-aura DIR] [--modes ar,vr] [--tests]

Generated scenes/prefabs/materials in P1-owned folders are not copied back automatically; review and copy
them deliberately (see .agents/evidence/p1-implementation.md).
"""
import argparse
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
EDITOR = Path('/Applications/Unity/Hub/Editor/6000.6.5f1/Unity.app')
DEFAULT_PROJECT = Path('/tmp/astra-vision-build')
DEFAULT_VISION = REPO.parent / 'Vision-Aura'


def unity(binary, project, log, *args, env=None):
    cmd = [str(binary), '-batchmode', '-nographics', '-projectPath', str(project), '-logFile', str(log), *args]
    print('+', ' '.join(cmd[:8]), '…')
    result = subprocess.run(cmd, env=env)
    if result.returncode != 0:
        sys.exit(f'Unity step failed ({result.returncode}); see {log}')


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--project', type=Path, default=DEFAULT_PROJECT)
    parser.add_argument('--vision-aura', type=Path, default=DEFAULT_VISION)
    parser.add_argument('--editor', type=Path, default=EDITOR)
    parser.add_argument('--modes', default='ar,vr')
    parser.add_argument('--tests', action='store_true', help='run EditMode and PlayMode suites before building')
    args = parser.parse_args()

    binary = args.editor / 'Contents/MacOS/Unity'
    package = args.vision_aura / 'Unity/com.visionaura.xr'
    if not (package / 'Plugins/macOS/VisionAuraXR.dylib').exists():
        sys.exit(f'Vision Aura native plugin missing; run python3 Tools/UnityXR/build.py --native-only in {args.vision_aura}')
    project = args.project.resolve()
    if REPO in project.parents or project == REPO:
        sys.exit('The build project must be outside the repository.')
    logs = project / 'BuildLogs'

    if not (project / 'ProjectSettings/ProjectVersion.txt').exists():
        project.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run([str(binary), '-batchmode', '-nographics', '-quit', '-createProject', str(project),
                        '-logFile', str(project.parent / 'astra-create.log')], check=True)
    logs.mkdir(exist_ok=True)

    builtin = args.editor / 'Contents/Resources/PackageManager/BuiltInPackages'
    manifest_path = project / 'Packages/manifest.json'
    deps = json.loads(manifest_path.read_text())['dependencies']
    for name in ('com.unity.xr.interaction.toolkit', 'com.unity.inputsystem', 'com.unity.xr.hands'):
        deps.pop(name, None)  # not compatible with 6000.6.5f1 at the pinned versions; not used by this build
    for name in ('com.unity.render-pipelines.universal', 'com.unity.render-pipelines.core', 'com.unity.shadergraph',
                 'com.unity.render-pipelines.universal-config', 'com.unity.ugui', 'com.unity.test-framework'):
        deps[name] = 'file:' + str(builtin / name)
    deps['com.unity.xr.management'] = '4.7.0'
    deps['com.visionaura.xr'] = 'file:' + str(package)
    manifest_path.write_text(json.dumps({'dependencies': deps}, indent=2) + '\n')

    settings = project / 'ProjectSettings/ProjectSettings.asset'
    text = settings.read_text()
    for value in ('1', '2'):
        text = text.replace('activeInputHandler: ' + value, 'activeInputHandler: 0')
    settings.write_text(text)

    # Mirror sources; keep the copy's generated metadata/assets so GUIDs stay stable between runs.
    subprocess.run(['rsync', '-a', '--delete', '--filter=P *.meta', '--filter=P *.unity', '--filter=P *.prefab',
                    '--filter=P *.mat', str(REPO / 'Assets/ASTRA') + '/', str(project / 'Assets/ASTRA') + '/'], check=True)
    if (REPO / 'fixtures').exists():
        shutil.copytree(REPO / 'fixtures', project / 'fixtures', dirs_exist_ok=True)
    (project / 'Assets/Editor').mkdir(parents=True, exist_ok=True)
    shutil.copyfile(REPO / '.agents/build/AstraVisionBuild.cs', project / 'Assets/Editor/AstraVisionBuild.cs')

    unity(binary, project, logs / 'import.log', '-quit')
    unity(binary, project, logs / 'prepare.log', '-quit', '-executeMethod', 'AstraVisionBuild.Prepare')
    unity(binary, project, logs / 'scenes.log', '-quit', '-executeMethod', 'AstraVisionBuild.BuildScenes')
    if args.tests:
        for platform in ('EditMode', 'PlayMode'):
            unity(binary, project, logs / f'{platform}.log', '-runTests', '-testPlatform', platform,
                  '-testResults', str(logs / f'{platform}.xml'))

    output = REPO / 'Builds/VisionAura'
    output.mkdir(parents=True, exist_ok=True)
    env = os.environ.copy()
    env['ASTRA_BUILD_OUTPUT'] = str(output)
    env['ASTRA_BUILD_MODES'] = args.modes
    unity(binary, project, logs / 'build.log', '-quit', '-executeMethod', 'AstraVisionBuild.BuildPlayers', env=env)
    for mode in args.modes.split(','):
        app = output / f'ASTRA Academy {mode.strip().upper()}.app'
        subprocess.run(['codesign', '--force', '--deep', '--sign', '-', str(app)], check=True)
        print('Built and ad-hoc signed', app)
    print('Desktop window works without a headset; live headset acceptance remains required.')


if __name__ == '__main__':
    main()
