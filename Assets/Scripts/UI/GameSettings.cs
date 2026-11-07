using System.Collections.Generic;
using UnityEngine;

namespace UI
{
    // Options the main menu actually changes. Saved in PlayerPrefs and applied again
    // the next time the menu loads. Resolution and fullscreen are stored always, and
    // applied to the window when the player changes them (and on boot outside the editor).
    public static class GameSettings
    {
        const string VolKey = "ie.volume";
        const string FullKey = "ie.fullscreen";
        const string QualKey = "ie.quality";
        const string VSyncKey = "ie.vsync";
        const string WKey = "ie.w";
        const string HKey = "ie.h";

        public struct ResolutionChoice
        {
            public int Width;
            public int Height;
            public string Label => Width + " × " + Height;
        }

        public static float Volume => PlayerPrefs.GetFloat(VolKey, 1f);
        public static bool Fullscreen => PlayerPrefs.GetInt(FullKey, Screen.fullScreen ? 1 : 0) == 1;
        public static int Quality => PlayerPrefs.GetInt(QualKey, QualitySettings.GetQualityLevel());
        public static bool VSync => PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;

        public static void ApplySaved()
        {
            AudioListener.volume = Volume;
            if (PlayerPrefs.HasKey(QualKey))
            {
                var max = Mathf.Max(0, QualitySettings.names.Length - 1);
                QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(QualKey), 0, max), true);
            }
            if (PlayerPrefs.HasKey(VSyncKey))
                QualitySettings.vSyncCount = PlayerPrefs.GetInt(VSyncKey) == 1 ? 1 : 0;

            if (Application.isEditor) return;
            if (PlayerPrefs.HasKey(FullKey))
                Screen.fullScreen = Fullscreen;
            if (PlayerPrefs.HasKey(WKey) && PlayerPrefs.HasKey(HKey))
                Screen.SetResolution(PlayerPrefs.GetInt(WKey), PlayerPrefs.GetInt(HKey), Screen.fullScreen);
        }

        public static void SetVolume(float value)
        {
            value = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolKey, value);
            AudioListener.volume = value;
            PlayerPrefs.Save();
        }

        public static void SetFullscreen(bool on)
        {
            PlayerPrefs.SetInt(FullKey, on ? 1 : 0);
            Screen.fullScreen = on;
            PlayerPrefs.Save();
        }

        public static void SetQuality(int level)
        {
            var max = Mathf.Max(0, QualitySettings.names.Length - 1);
            level = Mathf.Clamp(level, 0, max);
            PlayerPrefs.SetInt(QualKey, level);
            QualitySettings.SetQualityLevel(level, true);
            PlayerPrefs.Save();
        }

        public static void SetVSync(bool on)
        {
            PlayerPrefs.SetInt(VSyncKey, on ? 1 : 0);
            QualitySettings.vSyncCount = on ? 1 : 0;
            PlayerPrefs.Save();
        }

        public static void SetResolution(int width, int height)
        {
            PlayerPrefs.SetInt(WKey, width);
            PlayerPrefs.SetInt(HKey, height);
            Screen.SetResolution(width, height, Screen.fullScreen);
            PlayerPrefs.Save();
        }

        public static List<ResolutionChoice> Resolutions()
        {
            var list = new List<ResolutionChoice>();
            foreach (var r in Screen.resolutions)
            {
                var exists = false;
                for (var i = 0; i < list.Count; i++)
                    if (list[i].Width == r.width && list[i].Height == r.height) { exists = true; break; }
                if (!exists) list.Add(new ResolutionChoice { Width = r.width, Height = r.height });
            }
            if (list.Count == 0)
                list.Add(new ResolutionChoice { Width = Screen.width, Height = Screen.height });
            list.Sort((a, b) => (b.Width * b.Height).CompareTo(a.Width * a.Height));
            if (list.Count > 10) list.RemoveRange(10, list.Count - 10);
            return list;
        }

        public static int CurrentResolutionIndex(List<ResolutionChoice> list)
        {
            var w = PlayerPrefs.GetInt(WKey, Screen.width);
            var h = PlayerPrefs.GetInt(HKey, Screen.height);
            for (var i = 0; i < list.Count; i++)
                if (list[i].Width == w && list[i].Height == h) return i;
            return 0;
        }

        public static void ResetToDefaults()
        {
            SetVolume(1f);
            SetVSync(true);
            if (QualitySettings.names.Length > 0)
                SetQuality(QualitySettings.names.Length - 1);
            SetFullscreen(true);
            var list = Resolutions();
            if (list.Count > 0)
                SetResolution(list[0].Width, list[0].Height);
        }
    }
}
