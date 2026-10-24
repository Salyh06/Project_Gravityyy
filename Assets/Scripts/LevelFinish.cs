using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelFinish : MonoBehaviour
{
    private bool finished = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || finished)
            return;

        finished = true;

        int nextLevel = SceneManager.GetActiveScene().buildIndex + 1;

        if (nextLevel < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextLevel);
        }
        else
        {
            Debug.Log("Tüm bölümler tamamlandı!");
        }
    }
}