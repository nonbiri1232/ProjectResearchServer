using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransition : MonoBehaviour
{
    public static string nextSceneName{get;set;}

    public void GoToLoadingScene(string targetScene)
    {
        nextSceneName = targetScene;
        
        SceneManager.LoadScene("Load");
    }
}