#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace BoomForest.Editor
{
    [InitializeOnLoad]
    public static class ForestProjectSetup
    {
        const string Scene="Assets/Scenes/BoomForest.unity";
        static ForestProjectSetup(){EditorApplication.delayCall+=Configure;}
        static void Configure(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            PlayerSettings.productName="펑펑 숲";
            PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.template="PROJECT:BoomForest";
            PlayerSettings.WebGL.decompressionFallback=false;
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Scene,true)};
        }
        [MenuItem("펑펑 숲/게임 씬 열기")]
        public static void Open(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(Scene);}
        [MenuItem("펑펑 숲/Windows 빌드")]
        public static void Build(){
            string folder=EditorUtility.SaveFolderPanel("빌드 폴더","","BoomForest");if(string.IsNullOrEmpty(folder))return;
            Configure();
            BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=System.IO.Path.Combine(folder,"BoomForest.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result==BuildResult.Succeeded)EditorUtility.RevealInFinder(folder);else Debug.LogError("빌드 실패: Console을 확인하세요.");
        }
        [MenuItem("펑펑 숲/웹 빌드")]
        public static void BuildWeb(){
            string folder=EditorUtility.SaveFolderPanel("웹 빌드 저장 폴더","","BoomForestWeb");if(string.IsNullOrEmpty(folder))return;
            Configure();
            BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=folder,target=BuildTarget.WebGL,options=BuildOptions.None});
            if(report.summary.result==BuildResult.Succeeded)EditorUtility.RevealInFinder(folder);
            else Debug.LogError("웹 빌드 실패: Console의 오류를 확인하세요. Web Build Support 모듈도 필요합니다.");
        }
    }
}
#endif
