using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class RadioMemoryPuzzle
{
    private static readonly Vector3 FuseBay = new Vector3(-2.12f, .86f, -.46f);
    private static readonly Vector3 FuseRest = new Vector3(-2.40f, .18f, -.95f);
    private static readonly Vector3 PlugRest = new Vector3(2.02f, .25f, -.80f);
    private static readonly Vector3 SpeakerLead = new Vector3(2.60f, .49f, -.53f);
    private static readonly Vector3[] PortCenters =
    {
        new Vector3(-.76f, .86f, -.46f),
        new Vector3(.17f, .86f, -.46f),
        new Vector3(1.10f, .86f, -.46f)
    };
    private FirstMindCloseup radioCloseup;
    private MeshRenderer[] radioModel;
    private Collider fuseBayTarget, fuseTarget, plugTarget, playTarget, selectorTarget;
    private readonly Collider[] portTargets = new Collider[3];
    private Transform fuseModel, plugModel, needle, selectorLever;
    private LineRenderer speakerCable;
    private Renderer powerLamp;
    private Material green, dead, signal;

    private void OpenRadio()
    {
        radioCloseup = new FirstMindCloseup(view, gameObject.scene,
            new Vector3(0, 2.52f, -8), new Vector3(0, 1.14f, 0), 1.95f, 3.55f);
        var wood = radioCloseup.Material(new Color(.33f, .20f, .12f));
        var enamel = radioCloseup.Material(new Color(.26f, .28f, .22f));
        var rim = radioCloseup.Material(new Color(.62f, .56f, .38f));
        dead = radioCloseup.Material(new Color(.065f, .067f, .052f));
        green = radioCloseup.Material(new Color(.48f, .72f, .28f), true);
        signal = radioCloseup.Material(new Color(.87f, .72f, .35f), true);
        var copper = radioCloseup.Material(new Color(.74f, .30f, .13f), true);
        var glass = radioCloseup.Material(new Color(.35f, .67f, .58f));

        Box("Workbench", new Vector3(0, -.17f, .10f), new Vector3(6.8f, .32f, 2.85f), wood);
        Box("Amplifier", new Vector3(-.44f, .91f, .13f), new Vector3(4.27f, .70f, 1.05f), enamel);
        Box("Amplifier face", new Vector3(-.44f, .91f, -.403f), new Vector3(4.21f, .64f, .033f), dead);
        if (radioModel.Length > 0) radioCloseup.ShowExistingRadio(radioModel, new Vector3(-.76f, 1.27f, .20f));
        fuseBayTarget = Box("Fuse bay", FuseBay + Vector3.back * .045f,
            new Vector3(.49f, .25f, .18f), enamel, true).GetComponent<Collider>();
        Box("Left fuse terminal", FuseBay + new Vector3(-.19f, 0, -.14f), new Vector3(.077f, .12f, .065f), rim);
        Box("Right fuse terminal", FuseBay + new Vector3(.19f, 0, -.14f), new Vector3(.077f, .12f, .065f), rim);
        radioCloseup.Engrave("0,5 А", FuseBay + new Vector3(0, -.19f, -.055f), .020f);

        string[] ports = { "1  АНТЕННА", "2  ДИНАМИК", "3  ВНЕШ. ВХОД" };
        for (int i = 0; i < PortCenters.Length; i++)
        {
            Vector3 center = PortCenters[i];
            Cylinder("Socket rim", center, .25f, .054f, rim);
            portTargets[i] = Cylinder("Socket " + (i + 1), center + Vector3.back * .037f,
                .19f, .028f, dead, true).GetComponent<Collider>();
            radioCloseup.Engrave(ports[i], center + new Vector3(0, -.20f, -.052f), .016f);
        }

        Box("Timing gauge", new Vector3(-.35f, 1.143f, -.445f), new Vector3(2.58f, .113f, .067f), enamel);
        Box("Safe window", new Vector3(-1.58f + 2.46f * (SafeStart + SafeEnd) * .5f, 1.143f, -.486f),
            new Vector3(2.46f * (SafeEnd - SafeStart), .063f, .018f), green);
        needle = Box("Timing needle", new Vector3(-1.58f, 1.143f, -.51f), new Vector3(.026f, .115f, .024f), signal).transform;

        Box("Speaker cabinet", new Vector3(2.59f, .64f, -.08f), new Vector3(1.14f, 1.22f, .80f), wood);
        Cylinder("Speaker trim", new Vector3(2.59f, .75f, -.485f), .75f, .048f, rim);
        Cylinder("Speaker cone", new Vector3(2.59f, .75f, -.518f), .64f, .040f, dead);
        Cylinder("Speaker center", new Vector3(2.59f, .75f, -.55f), .20f, .066f, enamel);

        Box("Cassette deck", new Vector3(.02f, .27f, .13f), new Vector3(3.54f, .53f, 1.05f), enamel);
        Box("Cassette well", new Vector3(.11f, .27f, -.412f), new Vector3(1.52f, .34f, .058f), dead);
        Cylinder("Tape left reel", new Vector3(-.28f, .28f, -.46f), .22f, .028f, rim);
        Cylinder("Tape right reel", new Vector3(.50f, .28f, -.46f), .22f, .028f, rim);
        powerLamp = Cylinder("Power indicator", new Vector3(1.54f, .379f, -.46f), .074f, .02f, dead).GetComponent<Renderer>();
        playTarget = Box("Play button", new Vector3(1.30f, .219f, -.50f),
            new Vector3(.48f, .19f, .16f), rim, true).GetComponent<Collider>();
        radioCloseup.Engrave("PLAY", new Vector3(1.30f, .215f, -.586f), .020f);
        selectorTarget = Box("Input switch", new Vector3(-1.17f, .275f, -.50f),
            new Vector3(.72f, .24f, .17f), dead, true).GetComponent<Collider>();
        selectorLever = Box("Selector lever", new Vector3(-1.39f, .28f, -.62f), new Vector3(.16f, .15f, .17f), rim).transform;
        radioCloseup.Engrave("РАДИО / ВНЕШ.", new Vector3(-1.17f, .105f, -.50f), .016f);

        Box("Parts tray", FuseRest + Vector3.down * .12f, new Vector3(.68f, .08f, .46f), rim);
        fuseModel = Box("Fuse", FuseRest, new Vector3(.39f, .14f, .19f), glass, true).transform;
        fuseTarget = fuseModel.GetComponent<Collider>();
        foreach (float x in new[] { -.43f, .43f })
            radioCloseup.Part("Fuse cap", PrimitiveType.Cube, new Vector3(x, 0, 0),
                new Vector3(.25f, 1.08f, 1.08f), rim, parent: fuseModel);
        plugModel = Box("Speaker plug", PlugRest, new Vector3(.26f, .14f, .22f), copper, true).transform;
        plugTarget = plugModel.GetComponent<Collider>();
        radioCloseup.Part("Plug tip", PrimitiveType.Cube, new Vector3(0, 0, .64f),
            new Vector3(.37f, .35f, .45f), rim, parent: plugModel);
        speakerCable = radioCloseup.Cable("Speaker lead", copper, .047f);
        ShapeCable(radioCloseup.Cable("Cassette input cable", dead, .044f),
            PortCenters[2] + Vector3.back * .065f, new Vector3(.91f, .39f, -.47f));
        UpdateRadioParts(null);
        Physics.SyncTransforms();
    }

    private GameObject Box(string name, Vector3 pos, Vector3 size, Material material, bool pickable = false) =>
        radioCloseup.Part(name, PrimitiveType.Cube, pos, size, material, pickable);

    private GameObject Cylinder(string name, Vector3 pos, float diameter, float depth, Material material, bool pickable = false)
    {
        var part = radioCloseup.Part(name, PrimitiveType.Cylinder, pos,
            new Vector3(diameter, depth * .5f, diameter), material, pickable);
        part.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        return part;
    }

    private void UpdateRadio()
    {
        if (radioCloseup == null) return;
        radioCloseup.UpdateFraming();
        var mouse = Mouse.current;
        if (mouse == null) { dragging = DragPart.None; UpdateRadioParts(null); return; }
        Ray ray = radioCloseup.Camera.ScreenPointToRay(mouse.position.ReadValue());
        if (mouse.leftButton.wasPressedThisFrame) BeginPartInteraction(ray);
        if (!panelOpen) return;
        if (mouse.leftButton.wasReleasedThisFrame) PlacePart(ray);
        UpdateRadioParts(ray);
    }

    private void BeginPartInteraction(Ray ray)
    {
        if (hasFuse && !fuseInstalled && Hits(fuseTarget, ray)) dragging = DragPart.Fuse;
        else if (!speakerConnected && Hits(plugTarget, ray)) dragging = DragPart.SpeakerPlug;
        else if (Hits(playTarget, ray)) PlayRecording();
        else if (Hits(selectorTarget, ray))
        {
            externalInput = !externalInput;
            PlayEffect(contactClick);
            feedback = externalInput ? "Выбран внешний вход." : "Выбран вход радио.";
        }
    }

    private void PlacePart(Ray ray)
    {
        if (dragging == DragPart.Fuse && Hits(fuseBayTarget, ray)) InstallFuse();
        if (dragging == DragPart.SpeakerPlug)
            for (int i = 0; i < portTargets.Length; i++)
            {
                if (!Hits(portTargets[i], ray)) continue;
                ConnectSpeaker(i);
                break;
            }
        dragging = DragPart.None;
    }

    private static bool Hits(Collider target, Ray ray) => target.Raycast(ray, out _, 20f);

    private void UpdateRadioParts(Ray? pointer)
    {
        Vector3 fusePosition = fuseInstalled ? FuseBay + Vector3.back * .18f : FuseRest;
        Vector3 plugPosition = speakerConnected ? PortCenters[1] + Vector3.back * .13f : PlugRest;
        if (dragging != DragPart.None && pointer.HasValue)
        {
            var plane = new Plane(Vector3.back, radioCloseup.Root.TransformPoint(new Vector3(0, 0, -.77f)));
            Ray ray = pointer.Value;
            if (plane.Raycast(ray, out float distance))
            {
                Vector3 position = radioCloseup.Root.InverseTransformPoint(ray.GetPoint(distance));
                if (dragging == DragPart.Fuse) fusePosition = position;
                else plugPosition = position;
            }
        }
        fuseModel.gameObject.SetActive(hasFuse);
        fuseModel.localPosition = fusePosition;
        plugModel.localPosition = plugPosition;
        ShapeCable(speakerCable, SpeakerLead, plugPosition + Vector3.back * .115f);
        needle.gameObject.SetActive(fuseInstalled && !speakerConnected);
        needle.localPosition = new Vector3(-1.58f + 2.46f * TimingPhase, 1.143f, -.51f);
        selectorLever.localPosition = new Vector3(externalInput ? -.95f : -1.39f, .28f, -.62f);
        powerLamp.sharedMaterial = fuseInstalled ? green : Time.unscaledTime < faultUntil ? signal : dead;
    }

    private static void ShapeCable(LineRenderer cable, Vector3 from, Vector3 to)
    {
        for (int i = 0; i < cable.positionCount; i++)
        {
            float t = i / (float)(cable.positionCount - 1);
            Vector3 point = Vector3.Lerp(from, to, t);
            point.y -= Mathf.Sin(t * Mathf.PI) * .20f;
            point.z -= Mathf.Sin(t * Mathf.PI) * .30f;
            cable.SetPosition(i, point);
        }
    }
}
