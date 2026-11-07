using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuLoader : MonoBehaviour
{
    public async void LoadMainMenu()
    {
        // A finished online match: leave the room so the menu doesn't reopen it.
        if (OnlineRoom.InRoom)
        {
            try { await OnlineRoom.LeaveAsync(); }
            catch (System.Exception e) { Debug.LogWarning("Leaving the room failed: " + e.Message); }
        }
        SceneManager.LoadScene("Menu");
    }
}
