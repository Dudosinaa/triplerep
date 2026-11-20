using UnityEngine;

public sealed partial class RadioMemoryPuzzle
{
    private static readonly Color Metal = new Color32(66, 66, 57, 255);
    private static readonly Color Recess = new Color32(23, 24, 21, 255);
    private static readonly Color Edge = new Color32(120, 113, 91, 255);
    private static readonly Color Brass = new Color32(204, 169, 104, 255);
    private static readonly Color Copper = new Color32(177, 83, 49, 255);
    private static readonly Color Green = new Color32(142, 183, 102, 255);
    private static readonly Rect FuseHolder = new Rect(70, 189, 119, 54);
    private static readonly Rect FuseTray = new Rect(70, 268, 119, 42);
    private static readonly Vector2 LoosePlug = new Vector2(645, 281);
    private static readonly Vector2 SpeakerLead = new Vector2(697, 231);
    private static readonly Vector2 DeckSocket = new Vector2(515, 218);
    private static readonly Vector2 DeckLead = new Vector2(543, 354);
    private static readonly Rect[] PortTargets =
    {
        new Rect(250, 190, 90, 56),
        new Rect(360, 190, 90, 56),
        new Rect(470, 190, 90, 56)
    };
    private static Texture2D discTexture;
    private GUIStyle socketLabelStyle;
    private int dragControlId;
    private const int CableWidth = 800;
    private const int CableHeight = 516;
    private Texture2D cableTexture;
    private Color32[] cablePixels;
    private Vector2 renderedPlug;

    private void DrawPanel(Rect area)
    {
        if (Time.unscaledTime < faultUntil) Plate(area, new Color32(40, 23, 19, 255), Copper);
        else Backdrop(area);
        GUI.BeginGroup(area);
        GUI.Label(new Rect(28, 17, 590, 32), "Радиокомплекс", titleStyle);
        if (HardwareButton(new Rect(675, 17, 97, 31), "Esc", Metal)) Close();
        else DrawRadio();

        GUI.Label(new Rect(30, 517, 740, 47), feedback, bodyStyle);
        GUI.EndGroup();
    }

    private void DrawRadio()
    {
        Vector2 pointer = Event.current.mousePosition;
        var input = Event.current;
        int control = GUIUtility.GetControlID(FocusType.Passive);
        var type = input.GetTypeForControl(control);
        if (HandleRadioPointer(type, input.button, pointer))
        {
            if (dragging == DragPart.None) ReleaseDragControl();
            else GUIUtility.hotControl = dragControlId = control;
            input.Use();
        }
        GUI.Label(new Rect(30, 54, 740, 38),
            "Перетащи детали в гнёзда, выбери вход и запусти кассету.", bodyStyle);
        Plate(new Rect(30, 96, 740, 239), Metal, Edge);
        Plate(new Rect(51, 111, 520, 208), new Color32(49, 49, 43, 255), Edge);
        GUI.Label(new Rect(68, 119, 150, 29), "ПРЕДОХР. 0,5 А", bodyStyle);
        GUI.Label(new Rect(250, 120, 312, 29), "СХЕМА РАЗЪЁМОВ", bodyStyle);
        GUI.Label(new Rect(70, 151, 119, 30), "ГНЕЗДО", centeredStyle);

        bool fuseHover = dragging == DragPart.Fuse && FuseHolder.Contains(pointer);
        Plate(FuseHolder, Recess, fuseHover ? Green : Edge);
        Fill(new Rect(79, 203, 16, 23), Edge);
        Fill(new Rect(164, 203, 16, 23), Edge);
        if (fuseInstalled) DrawFuse(FuseArtAt(FuseHolder.center));
        else if (hasFuse)
        {
            Plate(FuseTray, Recess, FuseTray.Contains(pointer) ? Brass : Edge);
            if (dragging != DragPart.Fuse) DrawFuse(FuseArtAt(FuseTray.center));
        }

        socketLabelStyle ??= new GUIStyle(centeredStyle) { fontSize = 14 };
        for (int i = 0; i < PortTargets.Length; i++)
        {
            var port = PortTargets[i];
            bool hover = dragging == DragPart.SpeakerPlug && port.Contains(pointer);
            Disc(port.center, 38, hover ? Brass : Edge);
            Disc(port.center, 29, Recess);
            Disc(port.center, 11, new Color32(11, 12, 11, 255));
            GUI.Label(new Rect(port.center.x - 54, 149, 108, 38), $"{i + 1} · {Sockets[i]}", socketLabelStyle);
        }

        Plate(new Rect(647, 114, 99, 117), Recess, Edge);
        Disc(new Vector2(696, 161), 61, Edge);
        Disc(new Vector2(696, 161), 55, Metal);
        for (int y = 145; y <= 177; y += 8)
            Fill(new Rect(674, y, 44, 2), Recess);
        GUI.Label(new Rect(647, 197, 99, 25), "Динамик", centeredStyle);

        Vector2 plug = speakerConnected ? PortTargets[1].center :
            dragging == DragPart.SpeakerPlug ? pointer : LoosePlug;
        DrawDeck();
        DrawTimingMeter();
        DrawCables(plug);
        Disc(DeckSocket, 15, Edge);
        Disc(DeckLead, 12, Edge);
        DrawPlug(plug);
        if (dragging == DragPart.Fuse) DrawFuse(FuseArtAt(pointer));
    }

    private void DrawTimingMeter()
    {
        Plate(new Rect(30, 464, 740, 48), Metal, Edge);
        GUI.Label(new Rect(52, 475, 217, 24), "ОКНО ПОДКЛЮЧЕНИЯ", bodyStyle);
        var track = new Rect(272, 477, 465, 22);
        Plate(track, Recess, Edge);
        if (!fuseInstalled || speakerConnected)
        {
            GUI.Label(track, speakerConnected ? "КОНТАКТ УСТАНОВЛЕН" : "НЕТ ПИТАНИЯ", centeredStyle);
            return;
        }
        Fill(new Rect(track.x + track.width * SafeStart, track.y + 2,
            track.width * (SafeEnd - SafeStart), track.height - 4), Green);
        float phase = TimingPhase;
        Fill(new Rect(track.x + track.width * phase - 2, track.y - 4, 4, track.height + 8), Color.white);
    }

    private void ReleaseDragControl()
    {
        if (GUIUtility.hotControl == dragControlId) GUIUtility.hotControl = 0;
        dragControlId = 0;
    }

    private bool HandleRadioPointer(EventType eventType, int button, Vector2 pointer)
    {
        if (eventType == EventType.Ignore && dragging != DragPart.None)
        {
            dragging = DragPart.None;
            return false;
        }
        if (button != 0) return false;
        if (eventType == EventType.MouseDown && dragging == DragPart.None)
        {
            if (hasFuse && !fuseInstalled && FuseTray.Contains(pointer)) dragging = DragPart.Fuse;
            else if (!speakerConnected && new Rect(618, 257, 54, 49).Contains(pointer))
                dragging = DragPart.SpeakerPlug;
            else return false;
            return true;
        }
        else if (eventType == EventType.MouseUp && dragging != DragPart.None)
        {
            DragPart part = dragging;
            dragging = DragPart.None;
            if (part == DragPart.Fuse && FuseHolder.Contains(pointer)) InstallFuse();
            if (part == DragPart.SpeakerPlug)
                for (int i = 0; i < PortTargets.Length; i++)
                    if (PortTargets[i].Contains(pointer)) ConnectSpeaker(i);
            return true;
        }
        return false;
    }

    private void DrawDeck()
    {
        Plate(new Rect(30, 345, 740, 112), Metal, Edge);
        GUI.Label(new Rect(55, 349, 160, 27), "ВХОД УСИЛИТЕЛЯ", bodyStyle);
        var radio = new Rect(55, 384, 95, 43);
        var external = new Rect(154, 384, 103, 43);
        var selected = new Color32(112, 81, 38, 255);
        if (HardwareButton(radio, "Радио", !externalInput ? selected : Recess) && externalInput)
        {
            externalInput = false;
            PlayEffect(contactClick);
        }
        if (HardwareButton(external, "Внешний", externalInput ? selected : Recess) && !externalInput)
        {
            externalInput = true;
            PlayEffect(contactClick);
        }

        Plate(new Rect(295, 368, 265, 67), Recess, Edge);
        Disc(new Vector2(368, 401), 37, Metal);
        Disc(new Vector2(485, 401), 37, Metal);
        Disc(new Vector2(368, 401), 14, Edge);
        Disc(new Vector2(485, 401), 14, Edge);
        Fill(new Rect(387, 418, 79, 4), Edge);
        Disc(new Vector2(603, 379), 12, fuseInstalled ? Green : Recess);
        GUI.Label(new Rect(617, 367, 116, 23), "Питание", bodyStyle);
        if (HardwareButton(new Rect(595, 395, 147, 41), "▶  PLAY", Recess)) PlayRecording();
    }

    private bool HardwareButton(Rect rect, string label, Color color)
    {
        bool hover = rect.Contains(Event.current.mousePosition);
        Plate(rect, hover ? Color.Lerp(color, Edge, .4f) : color, hover ? Brass : Edge);
        GUI.Label(rect, label, centeredStyle);
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    private static Rect FuseArtAt(Vector2 center) => new Rect(center.x - 42, center.y - 10, 84, 20);

    private static void DrawFuse(Rect rect)
    {
        Fill(rect, new Color32(121, 150, 139, 255));
        Fill(new Rect(rect.x, rect.y + 2, rect.width, 3), new Color32(196, 214, 191, 255));
        Fill(new Rect(rect.x, rect.center.y, rect.width, 2), Brass);
        Fill(new Rect(rect.x, rect.y, 15, rect.height), Edge);
        Fill(new Rect(rect.xMax - 15, rect.y, 15, rect.height), Edge);
    }

    private static void DrawPlug(Vector2 center)
    {
        Plate(new Rect(center.x - 16, center.y - 8, 24, 16), Copper, Recess);
        Fill(new Rect(center.x + 8, center.y - 4, 10, 8), Brass);
    }

    private void DrawCables(Vector2 plug)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (cableTexture == null || renderedPlug != plug)
            UpdateCableTexture(plug);
        GUI.DrawTexture(new Rect(0, 0, CableWidth, CableHeight), cableTexture);
    }

    private void UpdateCableTexture(Vector2 plug)
    {
        if (cableTexture == null)
        {
            cableTexture = new Texture2D(CableWidth, CableHeight, TextureFormat.RGBA32, false)
                { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            cablePixels = new Color32[CableWidth * CableHeight];
        }
        renderedPlug = plug;
        System.Array.Clear(cablePixels, 0, cablePixels.Length);

        // Paint in panel coordinates so every bend uses the same transform as the hardware.
        PaintCurve(DeckSocket, new Vector2(515, 275), new Vector2(543, 308), DeckLead, 5, Recess);
        PaintCurve(DeckSocket, new Vector2(515, 275), new Vector2(543, 308), DeckLead, 2, Edge);
        Vector2 plugTail = plug + Vector2.left * 13;
        float sag = Mathf.Min(CableHeight - 12, Mathf.Max(SpeakerLead.y, plug.y) + 83);
        var firstBend = new Vector2(SpeakerLead.x, sag);
        var lastBend = new Vector2(plugTail.x, sag);
        PaintCurve(SpeakerLead, firstBend, lastBend, plugTail, 8, Recess);
        PaintCurve(SpeakerLead, firstBend, lastBend, plugTail, 3, Copper);

        cableTexture.SetPixels32(cablePixels);
        cableTexture.Apply(false);
    }

    private void PaintCurve(Vector2 start, Vector2 firstBend, Vector2 lastBend,
        Vector2 end, float thickness, Color32 ink)
    {
        const int steps = 32;
        Vector2 previous = start;
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            float u = 1f - t;
            Vector2 point = start * (u * u * u) + firstBend * (3 * u * u * t) +
                lastBend * (3 * u * t * t) + end * (t * t * t);
            PaintCableSpan(previous, point, thickness * .5f, ink);
            previous = point;
        }
    }

    private void PaintCableSpan(Vector2 from, Vector2 to, float radius, Color32 ink)
    {
        float margin = radius + 1;
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.x, to.x) - margin));
        int maxX = Mathf.Min(CableWidth - 1, Mathf.CeilToInt(Mathf.Max(from.x, to.x) + margin));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.y, to.y) - margin));
        int maxY = Mathf.Min(CableHeight - 1, Mathf.CeilToInt(Mathf.Max(from.y, to.y) + margin));
        Vector2 span = to - from;
        float lengthSquared = Mathf.Max(span.sqrMagnitude, .001f);
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                var pixel = new Vector2(x + .5f, y + .5f);
                float t = Mathf.Clamp01(Vector2.Dot(pixel - from, span) / lengthSquared);
                float alpha = Mathf.Clamp01(radius + .5f - Vector2.Distance(pixel, from + span * t));
                int index = (CableHeight - y - 1) * CableWidth + x;
                if (alpha > 0) cablePixels[index] = Color32.Lerp(cablePixels[index], ink, alpha);
            }
    }

    private void OnDestroy()
    {
        if (cableTexture != null) Destroy(cableTexture);
        DisposeRoomProps();
    }

    private static void Plate(Rect rect, Color fill, Color edge)
    {
        Fill(rect, edge);
        Fill(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), fill);
    }

    private static void Disc(Vector2 center, float size, Color color)
    {
        if (discTexture == null)
        {
            const int resolution = 48;
            discTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
                { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[resolution * resolution];
            float radius = resolution * .5f - .5f;
            var origin = Vector2.one * radius;
            for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                {
                    float alpha = Mathf.Clamp01(radius - Vector2.Distance(new Vector2(x, y), origin));
                    pixels[y * resolution + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            discTexture.SetPixels32(pixels);
            discTexture.Apply(false, true);
        }
        DrawTexture(new Rect(center.x - size / 2, center.y - size / 2, size, size), discTexture, color);
    }

    private static void Fill(Rect rect, Color color) => DrawTexture(rect, Texture2D.whiteTexture, color);

    private static void DrawTexture(Rect rect, Texture texture, Color color)
    {
        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, texture);
        GUI.color = previous;
    }
}
