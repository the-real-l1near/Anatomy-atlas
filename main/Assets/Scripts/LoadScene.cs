using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadScene : MonoBehaviour
{
    [SerializeField] private Slider progressBar;
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField] private float fadeDuration = 0.5f;

    private void Start()
    {
        StartCoroutine(LoadMainScene());
    }

    private IEnumerator LoadMainScene()
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync("MainScene");
        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
        {
            if (progressBar != null)
            {
                progressBar.value = operation.progress / 0.9f;
            }

            yield return null;
        }

        if (progressBar != null)
        {
            progressBar.value = 1f;
        }

        // Activate MainScene while the overlay is still covering the screen.
        operation.allowSceneActivation = true;

        // Wait until MainScene is actually active.
        while (!operation.isDone)
        {
            yield return null;
        }

        // Fade the MainScene overlay away.
        if (fadeOverlay != null)
        {
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);

                fadeOverlay.alpha = 1f - t;

                yield return null;
            }

            fadeOverlay.alpha = 0f;
        }
    }
}