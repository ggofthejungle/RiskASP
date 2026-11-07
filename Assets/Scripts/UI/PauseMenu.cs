using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    // Esc in a match: Resume / Leave match (with a confirm step). The game keeps running underneath,
    // because online the other players are still playing.
    public class PauseMenu : MonoBehaviour
    {
        GameManager _gm;
        GameObject _root;
        TMP_Text _body;
        TMP_Text _leaveLabel;
        bool _confirming;

        public bool IsOpen => _root != null && _root.activeSelf;
        public string BodyText => _body != null ? _body.text : "";

        public static PauseMenu Create(GameManager gm, Canvas canvas)
        {
            var go = new GameObject("PauseMenu", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            TurnHud.Stretch((RectTransform)go.transform);
            go.transform.SetAsLastSibling();
            var menu = go.AddComponent<PauseMenu>();
            menu._gm = gm;
            menu.Build();
            return menu;
        }

        void Build()
        {
            var shade = TurnHud.NewImage("Shade", transform, new Color(0f, 0f, 0f, 0.6f));
            shade.raycastTarget = true;
            TurnHud.Stretch(shade.rectTransform);
            _root = shade.gameObject;

            var panel = TurnHud.NewImage("Panel", shade.transform, TurnHud.PopupFill);
            panel.raycastTarget = true;
            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(460f, 300f);
            TurnHud.FramePopup(panel, TurnHud.PopupFill);

            var title = TurnHud.NewText("Title", rt, 30, FontStyles.Bold, TextAlignmentOptions.Center);
            title.text = "MENU";
            title.color = TurnHud.PopupBorder;
            title.characterSpacing = 4f;
            TurnHud.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -60f), new Vector2(-16f, -14f));

            _body = TurnHud.NewText("Body", rt, 18, FontStyles.Normal, TextAlignmentOptions.Top);
            _body.textWrappingMode = TextWrappingModes.Normal;
            _body.color = new Color(0.85f, 0.87f, 0.92f, 1f);
            TurnHud.Place(_body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 96f), new Vector2(-24f, -68f));

            Button("Resume", new Vector2(0.06f, 0f), new Vector2(0.47f, 0f), Close, TurnHud.PopupBorder);
            _leaveLabel = Button("Leave match", new Vector2(0.53f, 0f), new Vector2(0.94f, 0f), Leave, new Color(0.55f, 0.18f, 0.18f));

            _root.SetActive(false);
        }

        TMP_Text Button(string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction onClick, Color color)
        {
            var img = TurnHud.NewImage(label, _root.transform.GetChild(0), color);
            img.raycastTarget = true;
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(onClick);
            TurnHud.Place(img.rectTransform, min, max, new Vector2(0f, 22f), new Vector2(0f, 74f));
            var text = TurnHud.NewText("Label", img.rectTransform, 20, FontStyles.Bold, TextAlignmentOptions.Center);
            text.text = label;
            text.color = TurnHud.ReadableOn(color);
            TurnHud.Stretch(text.rectTransform);
            return text;
        }

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            _confirming = false;
            _leaveLabel.text = "Leave match";
            _body.text = _gm != null && _gm.Relay != null
                ? "The match keeps going while this is open."
                : "Leaving ends this game.";
            transform.SetAsLastSibling();
            _root.SetActive(true);
        }

        public void Close()
        {
            _confirming = false;
            _root.SetActive(false);
        }

        void Leave()
        {
            if (!_confirming)
            {
                _confirming = true;
                _leaveLabel.text = "Yes, leave";
                _body.text = LeaveWarning();
                return;
            }
            LeaveNow();
        }

        string LeaveWarning()
        {
            if (_gm == null || _gm.Relay == null) return "Leave this game? It can't be resumed.";
            if (OnlineNet.IsHost) return "You're the host. If you leave, another player's device takes over the match.";
            var code = OnlineRoom.LastCode;
            return string.IsNullOrEmpty(code)
                ? "Leave? The AI answers for you and your turns are skipped."
                : $"Leave? The AI answers for you and your turns are skipped. Rejoin with room code <b>{code}</b> while the host is still playing.";
        }

        async void LeaveNow()
        {
            _root.SetActive(false);
            if (_gm != null && _gm.Relay != null && OnlineRoom.InRoom)
            {
                try { await OnlineRoom.LeaveAsync(); }
                catch (System.Exception e) { Debug.LogWarning("Leaving the room failed: " + e.Message); }
            }
            SceneManager.LoadScene("Menu");
        }
    }
}
