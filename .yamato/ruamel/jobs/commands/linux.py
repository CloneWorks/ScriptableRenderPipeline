def _cmd_base(project, components):
    return [ 
        f'sudo -H pip install --upgrade pip',
        f'sudo -H pip install unity-downloader-cli --extra-index-url https://artifactory.internal.unity3d.com/api/pypi/common-python/simple',
        f'sudo npm install upm-ci-utils -g --registry https://api.bintray.com/npm/unity/unity-npm',
        f'git clone git@github.cds.internal.unity3d.com:unity/utr.git TestProjects/{project["folder"]}/utr',
        f'cd TestProjects/{project["folder"]} && sudo unity-downloader-cli --source-file ../../unity_revision.txt {"".join([f"-c {c} " for c in components])} --wait --published-only'
    ]

def cmd_editmode(project, platform, api):
    base = _cmd_base(project, platform["components"])
    base.extend([ 
        f'cd TestProjects/{project["folder"]} && DISPLAY=:0.0 utr/utr --extra-editor-arg="{api["cmd"]}"  --suite=editor --platform=editmode --testproject=. --editor-location=.Editor --artifacts_path=test-results'
     ])
    return base

def cmd_playmode(project, platform, api):
    base = _cmd_base(project, platform["components"])
    base.extend([ 
        f'cd TestProjects/{project["folder"]} && DISPLAY=:0.0 utr/utr --extra-editor-arg="{api["cmd"]}"  --suite=playmode --testproject=. --editor-location=.Editor --artifacts_path=test-results'
     ])
    return base

def cmd_standalone(project, platform, api):
    base = _cmd_base(project, platform["components"])
    base.extend([
        f'cd TestProjects/{project["folder"]} && DISPLAY=:0.0 utr/utr --suite=playmode --platform=StandaloneLinux64 --extra-editor-arg="-executemethod" --extra-editor-arg="CustomBuild.BuildLinux{api["name"]}Linear" --testproject=. --editor-location=.Editor --artifacts_path=test-results'
      ])
    return base

def cmd_standalone_build(project, platform, api):
    raise Exception('linux: standalone_split set to true but build commands not specified')

