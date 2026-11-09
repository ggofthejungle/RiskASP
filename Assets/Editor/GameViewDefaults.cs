using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// The Game view opens at Full HD (1920x1080) and 0.6x zoom, on editor start and on every Play.
// Unity has no public API for the Game view's size list or zoom, so this goes through its internal types;
// if a Unity upgrade renames them, it logs one warning and does nothing.
[InitializeOnLoad]
internal static class GameViewDefaults
{
    private const int Width = 1920, Height = 1080;
    private const float Scale = 0.6f;
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    static GameViewDefaults()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode) ApplyLater();
        };
        if (!SessionState.GetBool("ie.gameview.applied", false))
        {
            SessionState.SetBool("ie.gameview.applied", true);
            ApplyLater();
        }
    }

    [MenuItem("Invade Earth/Game View: Full HD at 0.6x")]
    private static void ApplyLater() => EditorApplication.delayCall += Apply;

    private static void Apply()
    {
        try
        {
            var editor = typeof(Editor).Assembly;
            var gameViewType = editor.GetType("UnityEditor.GameView");
            var views = Resources.FindObjectsOfTypeAll(gameViewType);
            if (views.Length == 0) return;

            int index = FullHdIndex(editor);
            foreach (var view in views)
            {
                var select = gameViewType.GetMethod("SizeSelectionCallback", Any);
                if (select != null) select.Invoke(view, new object[] { index, null });
                else gameViewType.GetProperty("selectedSizeIndex", Any).SetValue(view, index);
            }
            // Picking a size resets the zoom to fit, so zoom on the next editor tick.
            EditorApplication.delayCall += () =>
            {
                foreach (var view in Resources.FindObjectsOfTypeAll(gameViewType))
                {
                    gameViewType.GetMethod("SnapZoom", Any, null, new[] { typeof(float) }, null)
                        .Invoke(view, new object[] { Scale });
                    ((EditorWindow)view).Repaint();
                    var zoomArea = gameViewType.GetField("m_ZoomArea", Any)?.GetValue(view);
                    var scale = zoomArea?.GetType().GetProperty("scale", Any)?.GetValue(zoomArea);
                    if (scale is Vector2 s && Mathf.Abs(s.x - Scale) > 0.01f)
                        Debug.LogWarning($"Game view zoom is {s.x:0.##}x, not {Scale}x (the window may be too small for Full HD at {Scale}x).");
                }
            };
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Could not set the Game view to {Width}x{Height} at {Scale}x: {e.GetBaseException().Message}");
        }
    }

    // Index of 1920x1080 in the current platform's size list; adds it as a custom size if missing.
    private static int FullHdIndex(Assembly editor)
    {
        var sizesType = editor.GetType("UnityEditor.GameViewSizes");
        var sizes = sizesType.BaseType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var group = sizesType.GetProperty("currentGroup", Any).GetValue(sizes);
        var groupType = group.GetType();
        var sizeType = editor.GetType("UnityEditor.GameViewSize");
        var kindType = editor.GetType("UnityEditor.GameViewSizeType");
        var fixedKind = Enum.Parse(kindType, "FixedResolution");

        int count = (int)groupType.GetMethod("GetTotalCount", Any).Invoke(group, null);
        var getSize = groupType.GetMethod("GetGameViewSize", Any);
        for (int i = 0; i < count; i++)
        {
            var size = getSize.Invoke(group, new object[] { i });
            if ((int)sizeType.GetProperty("width", Any).GetValue(size) == Width
                && (int)sizeType.GetProperty("height", Any).GetValue(size) == Height
                && sizeType.GetProperty("sizeType", Any).GetValue(size).Equals(fixedKind))
                return i;
        }

        var ctor = sizeType.GetConstructors(Any).First(c => c.GetParameters().Length == 4);
        var fullHd = ctor.Invoke(new[] { fixedKind, Width, Height, (object)"Full HD" });
        groupType.GetMethod("AddCustomSize", Any).Invoke(group, new[] { fullHd });
        sizesType.GetMethod("SaveToHDD", Any)?.Invoke(sizes, null);
        return count;
    }
}
