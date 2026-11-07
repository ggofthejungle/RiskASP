using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    // Front door for the Menu scene. Built in code on the existing canvas.
    // Play → Single Player or Pass and Play opens the seat picker already in the scene.
    // Multiplayer is the online entry (host / join code) until the lobby service is connected.
    public class MainMenu : MonoBehaviour
    {
        public static MainMenu Instance { get; private set; }
        public static bool IsRunning => Instance != null;

        enum Page { Root, Play, Online, Room, Options, Credits, Licenses, Rules }

        static readonly Color Bg = new(0.045f, 0.05f, 0.075f, 1f);
        static readonly Color Column = new(0.07f, 0.08f, 0.11f, 1f);
        static readonly Color Muted = new(0.62f, 0.66f, 0.74f, 1f);
        static readonly Color Faint = new(1f, 1f, 1f, 0.45f);
        static readonly Color RowIdle = new(1f, 1f, 1f, 0f);
        static readonly Color RowHot = new(1f, 1f, 1f, 0.06f);

        Page _page = Page.Root;
        bool _lobbyOpen;
        string _status = "";

        Image _background;
        RectTransform _shell;
        RectTransform _content;
        TMP_Text _kicker;
        TMP_Text _hint;
        TMP_Text _statusText;
        GameObject _lobbyBar;
        TMP_Text _lobbyTitle;
        GameObject _quitModal;
        UIPlayerCreationManager _lobby;
        TMP_InputField _joinCode;

        public string PageName => _lobbyOpen ? "Lobby" : _page.ToString();
        public bool LobbyOpen => _lobbyOpen;
        public string StatusText => _statusText != null ? _statusText.text : _status;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate();

        static void TryCreate()
        {
            if (SceneManager.GetActiveScene().name != "Menu") return;
            if (FindAnyObjectByType<MainMenu>() != null) return;
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;
            Create(canvas);
        }

        public static MainMenu Create(Canvas canvas)
        {
            var go = new GameObject("MainMenu", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            TurnHud.Stretch((RectTransform)go.transform);
            go.transform.SetAsLastSibling();
            var menu = go.AddComponent<MainMenu>();
            menu.Build();
            return menu;
        }

        void Build()
        {
            Instance = this;
            _lobby = FindAnyObjectByType<UIPlayerCreationManager>(FindObjectsInactive.Include);
            var oldButtons = GameObject.Find("TopLeftButtons");
            if (oldButtons != null) oldButtons.SetActive(false);
            if (_lobby != null) _lobby.gameObject.SetActive(false);

            GameSettings.ApplySaved();

            _background = TurnHud.NewImage("Background", transform, Bg);
            _background.raycastTarget = true;
            TurnHud.Stretch(_background.rectTransform);

            var column = TurnHud.NewImage("Column", _background.transform, Column);
            column.raycastTarget = false;
            TurnHud.Place(column.rectTransform, new Vector2(0f, 0f), new Vector2(0.46f, 1f), Vector2.zero, Vector2.zero);
            var rule = TurnHud.NewImage("Rule", column.transform, TurnHud.PopupBorder);
            rule.raycastTarget = false;
            TurnHud.Place(rule.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-2f, 0f), Vector2.zero);

            _shell = NewRect("Shell", transform);
            TurnHud.Place(_shell, new Vector2(0.05f, 0.06f), new Vector2(0.44f, 0.94f), Vector2.zero, Vector2.zero);

            var title = TurnHud.NewText("Title", _shell, 64, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            title.text = "INVADE EARTH";
            title.characterSpacing = 4f;
            title.color = TurnHud.PopupBorder;
            TurnHud.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -120f), new Vector2(-8f, -28f));

            var underline = TurnHud.NewImage("Underline", _shell, TurnHud.PopupBorder);
            underline.raycastTarget = false;
            TurnHud.Place(underline.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -132f), new Vector2(148f, -128f));

            _kicker = TurnHud.NewText("Kicker", _shell, 18, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            _kicker.characterSpacing = 3f;
            _kicker.color = Muted;
            TurnHud.Place(_kicker.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -176f), new Vector2(-8f, -140f));

            _hint = TurnHud.NewText("Hint", _shell, 20, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _hint.color = Muted;
            _hint.textWrappingMode = TextWrappingModes.Normal;
            TurnHud.Place(_hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -248f), new Vector2(-8f, -184f));

            _content = NewRect("Content", _shell);
            TurnHud.Place(_content, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 88f), new Vector2(0f, -260f));

            _statusText = TurnHud.NewText("Status", _shell, 18, FontStyles.Italic, TextAlignmentOptions.TopLeft);
            _statusText.color = TurnHud.PopupBorder;
            _statusText.textWrappingMode = TextWrappingModes.Normal;
            TurnHud.Place(_statusText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 64f), new Vector2(-8f, 128f));

            var version = TurnHud.NewText("Version", _shell, 16, FontStyles.Normal, TextAlignmentOptions.BottomLeft);
            version.text = "v" + Application.version;
            version.color = Faint;
            TurnHud.Place(version.rectTransform, new Vector2(0f, 0f), new Vector2(0.4f, 0f), new Vector2(8f, 8f), new Vector2(0f, 36f));

            var discord = AddButton(_shell, "Discord", OpenDiscord, primary: true, footer: true);
            TurnHud.Place(discord, new Vector2(0.55f, 0f), new Vector2(1f, 0f), new Vector2(0f, 4f), new Vector2(-8f, 48f));
            var discordNav = discord.GetComponent<Button>().navigation;
            discordNav.mode = Navigation.Mode.None;
            discord.GetComponent<Button>().navigation = discordNav;

            BuildLobbyBar();
            BuildQuitModal();
            OnlineRoom.Changed += OnRoomChanged;
            OnlineRoom.MatchStarted += OnMatchStarted;
            // Back from an online match: this device's own rules again, not the last host's.
            MatchSession.Options = MatchOptions.Saved();
            Show(OnlineRoom.InRoom ? Page.Room : Page.Root);
        }

        void BuildLobbyBar()
        {
            _lobbyBar = new GameObject("LobbyBar", typeof(RectTransform));
            _lobbyBar.transform.SetParent(transform, false);
            var bar = _lobbyBar.GetComponent<RectTransform>();
            TurnHud.Place(bar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 16f), new Vector2(-24f, 88f));

            var back = AddButton(bar, "Back", CloseLobby, primary: false, footer: false);
            TurnHud.Place(back, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(220f, 0f));

            _lobbyTitle = TurnHud.NewText("LobbyTitle", bar, 22, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            _lobbyTitle.color = Color.white;
            _lobbyTitle.textWrappingMode = TextWrappingModes.NoWrap;
            _lobbyTitle.overflowMode = TextOverflowModes.Ellipsis;
            TurnHud.Place(_lobbyTitle.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(240f, 0f), new Vector2(-8f, 0f));
            _lobbyBar.SetActive(false);
        }

        void BuildQuitModal()
        {
            var dim = TurnHud.NewImage("QuitDim", transform, new Color(0f, 0f, 0f, 0.72f));
            dim.raycastTarget = true;
            TurnHud.Stretch(dim.rectTransform);
            _quitModal = dim.gameObject;

            var panel = TurnHud.NewImage("QuitPanel", dim.transform, TurnHud.PopupBorder);
            panel.raycastTarget = true;
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(560f, 260f);
            TurnHud.FramePopup(panel, TurnHud.PopupFill);

            var label = TurnHud.NewText("Question", panel.transform, 28, FontStyles.Bold, TextAlignmentOptions.Center);
            label.text = "Quit Invade Earth?";
            TurnHud.Place(label.rectTransform, new Vector2(0f, 0.45f), new Vector2(1f, 1f), new Vector2(24f, 0f), new Vector2(-24f, -16f));

            var quit = AddButton(panel.transform, "Quit", QuitGame, primary: true, footer: false);
            TurnHud.Place(quit, new Vector2(0.08f, 0.12f), new Vector2(0.48f, 0.42f), Vector2.zero, Vector2.zero);
            var cancel = AddButton(panel.transform, "Cancel", () => _quitModal.SetActive(false), primary: false, footer: false);
            TurnHud.Place(cancel, new Vector2(0.52f, 0.12f), new Vector2(0.92f, 0.42f), Vector2.zero, Vector2.zero);
            _quitModal.SetActive(false);
        }

        void Show(Page page)
        {
            _page = page;
            _status = "";
            _lobbyOpen = false;
            if (_background != null) _background.gameObject.SetActive(true);
            if (_shell != null) _shell.gameObject.SetActive(true);
            if (_lobbyBar != null) _lobbyBar.SetActive(false);
            if (_quitModal != null) _quitModal.SetActive(false);
            if (_statusText != null) _statusText.text = "";

            Clear(_content);
            _joinCode = null;

            switch (page)
            {
                case Page.Root:
                    _kicker.text = "";
                    _hint.text = "Five years. Bid for turn order, then take the map.";
                    Stack(
                        Row("Play", () => Show(Page.Play), true),
                        Row("Options", () => Show(Page.Options), false),
                        Row("Credits", () => Show(Page.Credits), false),
                        Row("Quit", () => _quitModal.SetActive(true), false));
                    break;
                case Page.Play:
                    _kicker.text = "PLAY";
                    _hint.text = "Single player is you against bots. Pass and Play shares this device.";
                    Stack(
                        Row("Single Player", () => OpenLobby(MatchKind.SinglePlayer), true),
                        Row("Multiplayer", () => Show(Page.Online), false),
                        Row("Pass and Play", () => OpenLobby(MatchKind.HotSeat), false),
                        Row("Match rules", () => Show(Page.Rules), false),
                        Row("Back", () => Show(Page.Root), false));
                    break;
                case Page.Rules:
                    _kicker.text = "MATCH RULES";
                    _hint.text = "Used for single player and pass and play. Online, the host's rules apply.";
                    BuildRules();
                    break;
                case Page.Online:
                    _kicker.text = "MULTIPLAYER";
                    _hint.text = "Host a private room, or join with a code.";
                    BuildOnline();
                    break;
                case Page.Room:
                    _kicker.text = "PRIVATE ROOM";
                    _hint.text = "";
                    BuildRoom();
                    break;
                case Page.Options:
                    _kicker.text = "OPTIONS";
                    _hint.text = "Changes save on this machine.";
                    BuildOptions();
                    break;
                case Page.Credits:
                    _kicker.text = "CREDITS";
                    _hint.text = "";
                    BuildCredits();
                    break;
                case Page.Licenses:
                    _kicker.text = "LICENSES";
                    _hint.text = "";
                    BuildLicenses();
                    break;
            }
        }

        void BuildOnline()
        {
            _joinCode = NewCodeField(_content);
            // Rejoining a match you dropped out of uses the same code.
            if (_joinCode != null && string.IsNullOrEmpty(_joinCode.text)) _joinCode.text = OnlineRoom.LastCode;
            var fieldRow = _joinCode.GetComponent<RectTransform>();
            var host = MakeRow(_content, "Host private room", () => RunRoomTask("Creating a room…", OnlineRoom.HostAsync()), true);
            var join = MakeRow(_content, "Join", () =>
            {
                var code = _joinCode != null ? _joinCode.text.Trim() : "";
                if (code.Length == 0) SetStatus("Enter a room code.");
                else RunRoomTask("Joining " + code.ToUpperInvariant() + "…", OnlineRoom.JoinAsync(code));
            }, false);
            var back = MakeRow(_content, "Back", () => Show(Page.Play), false);
            LayoutRows(new[] { fieldRow, host, join, back });
            WireNav(new[] { host.GetComponent<Button>(), join.GetComponent<Button>(), back.GetComponent<Button>() });
            SignIn();
        }

        bool _roomBusy;

        async void RunRoomTask(string working, System.Threading.Tasks.Task task)
        {
            if (_roomBusy) return;
            _roomBusy = true;
            SetStatus(working);
            try
            {
                await task;
                if (this == null) return;
                if (OnlineRoom.InRoom) Show(Page.Room);
            }
            catch (System.Exception e)
            {
                if (this == null) return;
                SetStatus(RoomError(e));
                Debug.LogWarning("Online room: " + e);
            }
            finally
            {
                _roomBusy = false;
            }
        }

        static string RoomError(System.Exception e)
        {
            var message = e.Message ?? "";
            if (message.Contains("project", System.StringComparison.OrdinalIgnoreCase)
                && message.Contains("id", System.StringComparison.OrdinalIgnoreCase))
                return "This build isn't linked to the InvadeEarth cloud project yet (Project Settings → Services).";
            if (message.Contains("not found", System.StringComparison.OrdinalIgnoreCase))
                return "No room with that code.";
            if (message.Contains("full", System.StringComparison.OrdinalIgnoreCase))
                return "That room is full.";
            return "Couldn't reach the room service: " + message;
        }

        void BuildRoom()
        {
            if (!OnlineRoom.InRoom)
            {
                Show(Page.Online);
                return;
            }
            _hint.text = $"Room code  <b><color=#FFD866>{OnlineRoom.Code}</color></b>   ·   share it with your friends";
            var rows = new List<RectTransform>();
            var buttons = new List<Button>();

            foreach (var m in OnlineRoom.Members)
            {
                var line = NewRect("Member", _content);
                line.sizeDelta = new Vector2(0f, 40f);
                var text = TurnHud.NewText("Name", line, 22, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                TurnHud.Stretch(text.rectTransform);
                var tag = m.Host ? "host" : m.Ready ? "ready" : "not ready";
                var me = m.Id == OnlineRoom.MyId ? " (you)" : "";
                text.text = $"{m.Name}{me}   <size=75%><color=#9AA3B5>{tag}</color></size>";
                if (OnlineRoom.IsHost && m.Id != OnlineRoom.MyId)
                {
                    var id = m.Id;
                    var kick = AddButton(line, "Kick", () => RunRoomTask("Removing…", OnlineRoom.KickAsync(id)), false, footer: false);
                    TurnHud.Place(kick, new Vector2(0.78f, 0.05f), new Vector2(1f, 0.95f), Vector2.zero, Vector2.zero);
                    buttons.Add(kick.GetComponent<Button>());
                }
                rows.Add(line);
            }
            for (var i = 1; i <= OnlineRoom.Bots; i++)
            {
                var line = NewRect("Bot", _content);
                line.sizeDelta = new Vector2(0f, 40f);
                var text = TurnHud.NewText("Name", line, 22, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                TurnHud.Stretch(text.rectTransform);
                text.text = $"{OnlineRoom.NameOf(OnlineRoom.BotSeatPrefix + i)}   <size=75%><color=#9AA3B5>AI, played by the host</color></size>";
                rows.Add(line);
            }

            if (OnlineRoom.IsHost)
                AddRuleRows(rows, buttons, () => RunRoomTask("Saving the rules…", OnlineRoom.SetRulesAsync(MatchSession.Options.Encode())));
            else
            {
                var rulesLine = NewRect("Rules", _content);
                rulesLine.sizeDelta = new Vector2(0f, 40f);
                var rulesText = TurnHud.NewText("Text", rulesLine, 18, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                TurnHud.Stretch(rulesText.rectTransform);
                rulesText.color = new Color(0.75f, 0.78f, 0.85f, 1f);
                rulesText.text = OnlineRoom.Rules != null ? "Rules: " + MatchOptions.Decode(OnlineRoom.Rules).Summary : "Rules: set by the host";
                rows.Add(rulesLine);
            }

            if (OnlineRoom.IsHost)
            {
                var start = MakeRow(_content, "Start match", () => RunRoomTask("Starting…", OnlineRoom.StartMatchAsync()), true);
                rows.Add(start);
                buttons.Add(start.GetComponent<Button>());
                var seated = OnlineRoom.Members.Count + OnlineRoom.Bots;
                if (seated < OnlineRoom.MaxPlayers)
                {
                    var add = MakeRow(_content, "Add a bot", () => RunRoomTask("Adding a bot…", OnlineRoom.SetBotsAsync(OnlineRoom.Bots + 1)), false);
                    rows.Add(add);
                    buttons.Add(add.GetComponent<Button>());
                }
                if (OnlineRoom.Bots > 0)
                {
                    var remove = MakeRow(_content, "Remove a bot", () => RunRoomTask("Removing a bot…", OnlineRoom.SetBotsAsync(OnlineRoom.Bots - 1)), false);
                    rows.Add(remove);
                    buttons.Add(remove.GetComponent<Button>());
                }
            }
            else
            {
                var meReady = false;
                foreach (var m in OnlineRoom.Members)
                    if (m.Id == OnlineRoom.MyId) meReady = m.Ready;
                var ready = MakeRow(_content, meReady ? "Not ready" : "Ready", () =>
                    RunRoomTask(meReady ? "…" : "Ready…", OnlineRoom.SetReadyAsync(!meReady)), true);
                rows.Add(ready);
                buttons.Add(ready.GetComponent<Button>());
            }

            var leave = MakeRow(_content, "Leave room", () => RunRoomTask("Leaving…", LeaveRoom()), false);
            rows.Add(leave);
            buttons.Add(leave.GetComponent<Button>());
            LayoutRows(rows.ToArray());
            WireNav(buttons.ToArray());

            var why = OnlineRoom.WhyCantStart();
            SetStatus(OnlineRoom.IsHost ? why ?? "Everyone's ready." : "Waiting for the host to start.");
        }

        async System.Threading.Tasks.Task LeaveRoom()
        {
            await OnlineRoom.LeaveAsync();
            if (this != null) Show(Page.Online);
        }

        void OnRoomChanged()
        {
            if (this == null || _page != Page.Room) return;
            Show(Page.Room);
        }

        void OnMatchStarted(int seed)
        {
            if (this == null) return;
            var seats = OnlineRoom.StartedSeats;
            var mySeat = System.Array.IndexOf(seats, OnlineRoom.MyId);
            if (mySeat < 0 || _lobby == null)
            {
                SetStatus(mySeat < 0 ? "The match started without you." : "The seat picker is missing from the Menu scene.");
                return;
            }
            var names = new List<string>();
            var bots = new List<bool>();
            foreach (var id in seats)
            {
                names.Add(OnlineRoom.NameOf(id));
                bots.Add(OnlineRoom.IsBotSeat(id));
            }
            _lobby.PrepareOnline(names, bots);
            // Every device plays the host's rules; this device's saved choice is left alone.
            MatchSession.Options = MatchOptions.Decode(OnlineRoom.Rules);
            OnlineMatch.Arm(seed, seats, mySeat, OnlineRoom.IsHost);
            MatchSession.Select(MatchKind.Online);
            MatchSession.ArmForNextLoad();
            SetStatus("Starting…");
            SceneManager.LoadScene("Main");
        }

        void SignIn()
        {
            if (PlayFabLogin.LoggedIn)
            {
                SetStatus("Signed in.");
                return;
            }
            SetStatus("Signing in…");
            PlayFabLogin.EnsureLoggedIn((ok, error) =>
            {
                if (this == null || _page != Page.Online) return;
                SetStatus(ok ? "Signed in." : "Couldn't sign in: " + error);
            });
        }

        void BuildOptions()
        {
            var rows = new List<RectTransform>();
            var buttons = new List<Button>();

            var volume = NewSliderRow(_content, "Master volume", GameSettings.Volume, GameSettings.SetVolume);
            rows.Add(volume);

            var resolutions = GameSettings.Resolutions();
            var resIndex = GameSettings.CurrentResolutionIndex(resolutions);
            Button resButton = null;
            var resRow = MakeValueRow(_content, "Resolution", resolutions[resIndex].Label, () =>
            {
                resIndex = (resIndex + 1) % resolutions.Count;
                var choice = resolutions[resIndex];
                GameSettings.SetResolution(choice.Width, choice.Height);
                SetRowValue(resButton, choice.Label);
            });
            resButton = resRow.GetComponent<Button>();
            rows.Add(resRow);
            buttons.Add(resButton);

            var fullOn = GameSettings.Fullscreen;
            Button fullButton = null;
            var fullRow = MakeValueRow(_content, "Fullscreen", fullOn ? "On" : "Off", () =>
            {
                fullOn = !fullOn;
                GameSettings.SetFullscreen(fullOn);
                SetRowValue(fullButton, fullOn ? "On" : "Off");
            });
            fullButton = fullRow.GetComponent<Button>();
            rows.Add(fullRow);
            buttons.Add(fullButton);

            var names = QualitySettings.names;
            var q = Mathf.Clamp(GameSettings.Quality, 0, Mathf.Max(0, names.Length - 1));
            Button qButton = null;
            var qRow = MakeValueRow(_content, "Quality", names.Length > 0 ? names[q] : "Default", () =>
            {
                if (names.Length == 0) return;
                q = (q + 1) % names.Length;
                GameSettings.SetQuality(q);
                SetRowValue(qButton, names[q]);
            });
            qButton = qRow.GetComponent<Button>();
            rows.Add(qRow);
            buttons.Add(qButton);

            var vsync = GameSettings.VSync;
            Button vButton = null;
            var vRow = MakeValueRow(_content, "VSync", vsync ? "On" : "Off", () =>
            {
                vsync = !vsync;
                GameSettings.SetVSync(vsync);
                SetRowValue(vButton, vsync ? "On" : "Off");
            });
            vButton = vRow.GetComponent<Button>();
            rows.Add(vRow);
            buttons.Add(vButton);

            var reset = MakeRow(_content, "Reset to defaults", () =>
            {
                GameSettings.ResetToDefaults();
                Show(Page.Options);
            }, false);
            rows.Add(reset);
            buttons.Add(reset.GetComponent<Button>());

            var back = MakeRow(_content, "Back", () => Show(Page.Root), false);
            rows.Add(back);
            buttons.Add(back.GetComponent<Button>());

            LayoutRows(rows.ToArray());
            WireNav(buttons.ToArray());
        }

        // Two value rows that cycle the match rules. Online, onChanged also sends them to the room.
        void AddRuleRows(List<RectTransform> rows, List<Button> buttons, System.Action onChanged)
        {
            var o = MatchSession.Options;
            Button devButton = null;
            var devRow = MakeValueRow(_content, "Devastated territories", o.Devastated.ToString(), () =>
            {
                o.Devastated = (o.Devastated + 1) % (MatchOptions.MaxDevastated + 1);
                o.Save();
                SetRowValue(devButton, o.Devastated.ToString());
                onChanged?.Invoke();
            });
            devButton = devRow.GetComponent<Button>();
            rows.Add(devRow);
            buttons.Add(devButton);

            Button placeButton = null;
            var placeRow = MakeValueRow(_content, "Starting territories", o.PlacementLabel, () =>
            {
                o.Placement = o.Placement == TerritorySetup.Random ? TerritorySetup.TakeTurns : TerritorySetup.Random;
                o.Save();
                SetRowValue(placeButton, o.PlacementLabel);
                onChanged?.Invoke();
            });
            placeButton = placeRow.GetComponent<Button>();
            rows.Add(placeRow);
            buttons.Add(placeButton);
        }

        void BuildRules()
        {
            var rows = new List<RectTransform>();
            var buttons = new List<Button>();
            AddRuleRows(rows, buttons, null);
            var note = NewRect("Note", _content);
            note.sizeDelta = new Vector2(0f, 64f);
            var text = TurnHud.NewText("Text", note, 18, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            TurnHud.Stretch(text.rectTransform);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.color = new Color(0.75f, 0.78f, 0.85f, 1f);
            text.text = "Devastated territories are out of the game: nobody owns them and no one can move through them. " +
                        "Take turns: players claim empty territories one at a time, then place their army 3 MODs at a time.";
            rows.Add(note);
            var back = MakeRow(_content, "Back", () => Show(Page.Play), false);
            rows.Add(back);
            buttons.Add(back.GetComponent<Button>());
            LayoutRows(rows.ToArray());
            WireNav(buttons.ToArray());
        }

        void BuildCredits()
        {
            var body = TurnHud.NewText("Body", _content, 22, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            body.color = new Color(0.9f, 0.91f, 0.94f, 1f);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.text =
                "A territory game for 2 to 5 players.\n\n" +
                "Includes RiskASP by Farfi55, MIT License.\n" +
                "Command cards and commanders by Nikolas Krecu.\n" +
                "Developed by George.\n\n" +
                "Development build " + Application.version + ".";
            body.rectTransform.sizeDelta = new Vector2(0f, 240f);
            var licenses = MakeRow(_content, "Licenses", () => Show(Page.Licenses), false);
            var back = MakeRow(_content, "Back", () => Show(Page.Root), false);
            LayoutRows(new RectTransform[] { body.rectTransform, licenses, back });
            WireNav(new[] { licenses.GetComponent<Button>(), back.GetComponent<Button>() });
        }

        // The MIT License asks for its notice in every copy of the software, so it ships in the game itself.
        const string RiskAspLicense =
            "<b>RiskASP</b>\n" +
            "MIT License\n\n" +
            "Copyright (c) 2023 Alessio Farfaglia\n\n" +
            "Permission is hereby granted, free of charge, to any person obtaining a copy of this software and " +
            "associated documentation files (the \"Software\"), to deal in the Software without restriction, including " +
            "without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell " +
            "copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the " +
            "following conditions:\n\n" +
            "The above copyright notice and this permission notice shall be included in all copies or substantial " +
            "portions of the Software.\n\n" +
            "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT " +
            "LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO " +
            "EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER " +
            "IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR " +
            "THE USE OR OTHER DEALINGS IN THE SOFTWARE.\n\n" +
            "<b>PlayFab SDK</b>: Copyright (c) Microsoft Corporation, Apache License 2.0 " +
            "(apache.org/licenses/LICENSE-2.0). Includes SimpleJson, Copyright (c) 2011 The Outercurve Foundation, MIT License.";

        void BuildLicenses()
        {
            var body = TurnHud.NewText("Body", _content, 15, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            body.color = new Color(0.82f, 0.84f, 0.88f, 1f);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.text = RiskAspLicense;
            body.rectTransform.sizeDelta = new Vector2(0f, 480f);
            var back = MakeRow(_content, "Back", () => Show(Page.Credits), false);
            LayoutRows(new RectTransform[] { body.rectTransform, back });
            WireNav(new[] { back.GetComponent<Button>() });
        }

        struct RowSpec
        {
            public string Label;
            public UnityEngine.Events.UnityAction Action;
            public bool Primary;
        }

        static RowSpec Row(string label, UnityEngine.Events.UnityAction action, bool primary) =>
            new RowSpec { Label = label, Action = action, Primary = primary };

        void Stack(params RowSpec[] items)
        {
            var rects = new RectTransform[items.Length];
            var buttons = new Button[items.Length];
            for (var i = 0; i < items.Length; i++)
            {
                rects[i] = MakeRow(_content, items[i].Label, items[i].Action, items[i].Primary);
                buttons[i] = rects[i].GetComponent<Button>();
            }
            LayoutRows(rects);
            WireNav(buttons);
        }

        void OpenLobby(MatchKind kind)
        {
            if (_lobby == null)
            {
                SetStatus("The seat picker is missing from the Menu scene.");
                return;
            }
            MatchSession.Select(kind);
            _lobbyOpen = true;
            _page = Page.Play;
            _background.gameObject.SetActive(false);
            _shell.gameObject.SetActive(false);
            _quitModal.SetActive(false);
            _lobbyBar.SetActive(true);
            _lobbyTitle.text = kind == MatchKind.HotSeat
                ? "Pass and Play  ·  add everyone on this device, then Start"
                : "Single Player  ·  you, plus bots in the other seats, then Start";
            _lobby.gameObject.SetActive(true);
            _lobby.Prepare(kind);
        }

        void CloseLobby()
        {
            if (_lobby != null)
            {
                _lobby.ClearAll();
                _lobby.gameObject.SetActive(false);
            }
            Show(Page.Play);
        }

        void OpenDiscord()
        {
            if (!GameLinks.HasDiscord)
            {
                SetStatus("Add the server invite in GameLinks.DiscordInvite.");
                return;
            }
            Application.OpenURL(GameLinks.DiscordInvite);
        }

        void SetStatus(string message)
        {
            _status = message;
            if (_statusText != null) _statusText.text = message;
        }

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (_quitModal != null && _quitModal.activeSelf)
            {
                _quitModal.SetActive(false);
                return;
            }
            if (_lobbyOpen) CloseLobby();
            else if (_page == Page.Root) _quitModal.SetActive(true);
            else if (_page == Page.Online) Show(Page.Play);
            else if (_page == Page.Licenses) Show(Page.Credits);
            else if (_page == Page.Rules) Show(Page.Play);
            else if (_page == Page.Room) RunRoomTask("Leaving…", LeaveRoom());
            else Show(Page.Root);
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            OnlineRoom.Changed -= OnRoomChanged;
            OnlineRoom.MatchStarted -= OnMatchStarted;
        }

        // ---------------- rows ----------------

        RectTransform MakeRow(Transform parent, string label, UnityEngine.Events.UnityAction action, bool primary)
        {
            var button = AddButton(parent, label, action, primary, footer: false);
            return button;
        }

        RectTransform MakeValueRow(Transform parent, string label, string value, UnityEngine.Events.UnityAction action)
        {
            var row = MakeRow(parent, label, action, false);
            var valueText = TurnHud.NewText("Value", row, 22, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            valueText.name = "Value";
            valueText.text = value;
            valueText.color = TurnHud.PopupBorder;
            TurnHud.Place(valueText.rectTransform, new Vector2(0.45f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-18f, 0f));
            return row;
        }

        static void SetRowValue(Button button, string value)
        {
            if (button == null) return;
            var t = button.transform.Find("Value")?.GetComponent<TMP_Text>();
            if (t != null) t.text = value;
        }

        RectTransform AddButton(Transform parent, string label, UnityEngine.Events.UnityAction action, bool primary, bool footer)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(MenuRow));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = RowIdle;
            image.raycastTarget = true;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var colors = button.colors;
            colors.normalColor = RowIdle;
            colors.highlightedColor = RowHot;
            colors.pressedColor = new Color(1f, 1f, 1f, 0.12f);
            colors.selectedColor = RowHot;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var accent = TurnHud.NewImage("Accent", go.transform, Color.clear);
            accent.raycastTarget = false;
            TurnHud.Place(accent.rectTransform, new Vector2(0f, 0.15f), new Vector2(0f, 0.85f), new Vector2(0f, 0f), new Vector2(4f, 0f));

            var text = TurnHud.NewText("Label", go.transform, footer ? 18 : 28, primary ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            text.text = label;
            text.color = primary ? TurnHud.PopupBorder : Color.white;
            if (footer) text.alignment = TextAlignmentOptions.MidlineRight;
            TurnHud.Place(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(footer ? 0f : 18f, 0f), new Vector2(footer ? 0f : -12f, 0f));

            var row = go.GetComponent<MenuRow>();
            row.Accent = accent;
            row.Label = text;
            row.Idle = text.color;
            return (RectTransform)go.transform;
        }

        static void LayoutRows(RectTransform[] rows)
        {
            float y = 0f;
            foreach (var rt in rows)
            {
                if (rt == null) continue;
                var h = rt.sizeDelta.y > 1f ? rt.sizeDelta.y : 58f;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(0f, -y);
                rt.sizeDelta = new Vector2(0f, h);
                y += h + 8f;
            }
        }

        static void WireNav(Button[] buttons)
        {
            Button first = null;
            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                if (first == null) first = buttons[i];
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = Neighbor(buttons, i, -1);
                nav.selectOnDown = Neighbor(buttons, i, 1);
                buttons[i].navigation = nav;
            }
            if (first != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        static Button Neighbor(Button[] buttons, int index, int dir)
        {
            var n = buttons.Length;
            for (var step = 1; step <= n; step++)
            {
                var b = buttons[(index + dir * step + n) % n];
                if (b != null) return b;
            }
            return null;
        }

        RectTransform NewSliderRow(Transform parent, string label, float value, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var row = NewRect(label, parent);
            row.sizeDelta = new Vector2(0f, 58f);
            var caption = TurnHud.NewText("Label", row, 28, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            caption.text = label;
            TurnHud.Place(caption.rectTransform, new Vector2(0f, 0f), new Vector2(0.48f, 1f), new Vector2(18f, 0f), Vector2.zero);

            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(row, false);
            var srt = (RectTransform)sliderGo.transform;
            TurnHud.Place(srt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-20f, -12f), new Vector2(240f, 12f));
            srt.anchorMin = new Vector2(0.5f, 0.5f);
            srt.anchorMax = new Vector2(1f, 0.5f);
            srt.offsetMin = new Vector2(8f, -10f);
            srt.offsetMax = new Vector2(-16f, 10f);

            var track = TurnHud.NewImage("Track", sliderGo.transform, new Color(1f, 1f, 1f, 0.12f));
            track.raycastTarget = true;
            TurnHud.Stretch(track.rectTransform);

            var fill = TurnHud.NewImage("Fill", sliderGo.transform, TurnHud.PopupBorder);
            fill.raycastTarget = false;
            var fr = fill.rectTransform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = new Vector2(0f, 1f);
            fr.offsetMin = Vector2.zero;
            fr.offsetMax = Vector2.zero;
            fr.pivot = new Vector2(0f, 0.5f);

            var handle = TurnHud.NewImage("Handle", sliderGo.transform, Color.white);
            handle.raycastTarget = true;
            var hr = handle.rectTransform;
            hr.sizeDelta = new Vector2(18f, 28f);

            var slider = sliderGo.GetComponent<Slider>();
            slider.fillRect = fr;
            slider.handleRect = hr;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            slider.onValueChanged.AddListener(onChanged);
            return row;
        }

        static TMP_InputField NewCodeField(Transform parent)
        {
            var go = new GameObject("Join code", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(0f, 58f);
            var plate = go.GetComponent<Image>();
            plate.color = new Color(1f, 1f, 1f, 0.06f);
            plate.raycastTarget = true;
            TurnHud.FramePopup(plate, new Color(0.09f, 0.1f, 0.14f, 1f), 2f);

            var text = TurnHud.NewText("Text", go.transform, 26, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            text.color = Color.white;
            TurnHud.Place(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 4f), new Vector2(-16f, -4f));
            var placeholder = TurnHud.NewText("Placeholder", go.transform, 22, FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
            placeholder.text = "Room code";
            placeholder.color = Faint;
            TurnHud.Place(placeholder.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 4f), new Vector2(-16f, -4f));

            var fill = go.transform.Find("Fill")?.GetComponent<Image>();
            if (fill != null) fill.raycastTarget = false;

            var field = go.GetComponent<TMP_InputField>();
            field.textViewport = rt;
            field.textComponent = (TMP_Text)text;
            field.placeholder = placeholder;
            field.characterLimit = 8;
            field.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
            field.pointSize = 26;
            return field;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Clear(Transform t)
        {
            if (t == null) return;
            for (var i = t.childCount - 1; i >= 0; i--)
                DestroyImmediate(t.GetChild(i).gameObject);
        }

        sealed class MenuRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
        {
            public Image Accent;
            public TMP_Text Label;
            public Color Idle;
            bool _hot;

            public void OnPointerEnter(PointerEventData eventData) => Hot(true);
            public void OnPointerExit(PointerEventData eventData) => Hot(false);
            public void OnSelect(BaseEventData eventData) => Hot(true);
            public void OnDeselect(BaseEventData eventData) => Hot(false);

            void Hot(bool on)
            {
                if (_hot == on) return;
                _hot = on;
                if (Accent != null) Accent.color = on ? TurnHud.PopupBorder : Color.clear;
                if (Label != null) Label.color = Idle;
            }
        }
    }
}
