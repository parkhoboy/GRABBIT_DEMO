using UnityEditor;
using UnityEngine;

namespace Grabbit.Editor
{
    [InitializeOnLoad]
    public static class GrabbitMapMakerMenu
    {
        const string Pending="Grabbit.OpenMapMaker";
        static GrabbitMapMakerMenu()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false))return;
                SessionState.SetBool(Pending,false);EditorApplication.delayCall+=Open;
            };
        }
        [MenuItem("GRABBIT/Map Maker")]
        public static void Open()
        {
            if(EditorApplication.isPlaying)
            {
                var game=Object.FindFirstObjectByType<GrabbitGame>();if(game!=null)game.ShowMapMaker();
                return;
            }
            if(Object.FindFirstObjectByType<GrabbitGame>()==null)
            {
                Debug.LogWarning("Assets/Scenes/Grabbit.unity를 열고 Map Maker를 실행하세요.");return;
            }
            SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
        }
    }
}
