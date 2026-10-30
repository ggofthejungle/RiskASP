using UnityEngine;

namespace UI
{
    // The classic Risk card bar (sets of infantry/cavalry/artillery traded for troops) doesn't exist in 2210.
    // It's hidden; the command-card hand (UI/CommandCardHud) replaces it.
    public class UICardsManager : MonoBehaviour
    {
        private void Awake() => gameObject.SetActive(false);
    }
}
