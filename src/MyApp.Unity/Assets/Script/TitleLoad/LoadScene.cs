using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class LoadScene : MonoBehaviour
{
    [SerializeField]private Slider progressBar;
    private string nextSceneName;
    void Start()
    {
        nextSceneName = SceneTransition.nextSceneName;
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            StartCoroutine(LoadSceneAsync());
        }
        else
        {
            nextSceneName = "Title";
            StartCoroutine(LoadSceneAsync());
        }
    }
    IEnumerator LoadSceneAsync()
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(nextSceneName);
        asyncLoad.allowSceneActivation = false;

        while(!asyncLoad.isDone)
        {
            Debug.Log($"読み込み進捗：{asyncLoad.progress * 100}%");
            float progressValue = Mathf.Clamp01(asyncLoad.progress / 0.9f);

            if(progressBar != null)
            {
                progressBar.value = progressValue;
            }
            if(asyncLoad.progress >= 0.9f)
            {
                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }
        SceneTransition.nextSceneName = null;
        Debug.Log("シーンの読み込みが完了しました。");
    }
}