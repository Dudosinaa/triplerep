using TMPro;
using UnityEngine;

internal sealed partial class FinalLabResonatorBuilder
{
    private static readonly Quaternion Vertical = Quaternion.identity;
    private static readonly Quaternion Face = Quaternion.Euler(90, 0, 0);

    private void Controls(Transform root)
    {
        var panel = Group(root, "K17_ControlPanel", Vector3.zero);
        Block(panel, "Steel console mount", new(.335f, 1.13f, .225f), new(.663f, .675f, .158f), olive, true);
        Fit(panel, "Industrial switchboard housing", "modular_industrial_electrical_switchboard.glb",
            new(.335f, .81f, .308f), new(.64f, .64f, .104f), Quaternion.Euler(0, 180, 0));
        Block(panel, "Recessed painted instrument face", new(.335f, 1.13f, .367f), new(.56f, .543f, .037f), steel, true);
        foreach (float x in new[] { .072f, .598f })
            foreach (float y in new[] { .875f, 1.384f }) Bolt(panel, new Vector3(x, y, .389f), .011f, Face);

        Gauge(panel, "K17_AnalogGauge", new(.189f, 1.289f, .385f), .090f, .30f);
        Plate(panel, "Contour gauge caption", new(.189f, 1.174f, .390f), new(.159f, .033f), black, Ink, ("ТОК КОНТУРА", 57));
        Isolation(panel);
        Indicator(panel, "K17_PowerIndicator", new(.341f, 1.130f, .390f), green);
        Indicator(panel, "K17_ContourIndicator", new(.341f, 1.069f, .390f), amber);
        Plate(panel, "Mains caption", new(.403f, 1.135f, .390f), new(.082f, .026f), black, Ink, ("СЕТЬ", 61));
        Plate(panel, "Circuit caption", new(.408f, 1.075f, .390f), new(.082f, .026f), black, Ink, ("КОНТУР", 56));

        var selector = Group(panel, "K17_ModeSelector", new(.177f, 1.005f, .407f));
        Plate(panel, "Selector title", new(.184f, 1.099f, .392f), new(.216f, .028f), black, Ink, ("РЕЖИМ РЕЗОНАНСА", 53));
        Fit(panel, "Vintage switch plate", "vintage_industrial_light_switches_type_a.glb", new(.177f, .954f, .405f),
            new(.105f, .105f, .033f), Vertical, r => r.name == "Object_6");
        Cylinder(selector, "Bakelite rotary handle", new(0, 0, .028f), .030f, .033f, black, Face);
        var index = Block(selector, "Ivory mode index", new(.013f, .013f, .047f), new(.007f, .023f, .0038f), ceramic);
        index.localRotation = Quaternion.Euler(0, 0, -40);
        Plate(panel, "A contact", new(.240f, .928f, .391f), new(.102f, .037f), black, Ink, ("А — КОНТАКТ", 55));
        Plate(panel, "B isolation", new(.120f, .928f, .391f), new(.114f, .037f), black, Ink, ("Б — ИЗОЛЯЦИЯ", 51));

        var button = Group(panel, "K17_OverloadButton", new(.504f, 1.013f, .390f));
        Cylinder(button, "Button socket", new(0, 0, .009f), .050f, .022f, steel, Face);
        Cylinder(button, "Red guarded actuator", new(0, 0, .032f), .033f, .035f, red, Face);
        foreach (float x in new[] { .449f, .559f })
            Block(panel, "Fixed button guard cheeks", new(x, 1.011f, .427f), new(.016f, .091f, .072f), steel);
        var cover = Group(panel, "K17_OverloadCover", new(.504f, 1.060f, .424f));
        Cylinder(cover, "Cover hinge pin", Vector3.zero, .010f, .124f, steel, Quaternion.Euler(0, 0, 90));
        var flap = Block(cover, "Raised steel safety cover", new(0, .032f, .025f), new(.115f, .076f, .014f), olive);
        flap.localRotation = Quaternion.Euler(55, 0, 0);
        Plate(panel, "Overload instructions", new(.485f, .914f, .391f), new(.205f, .069f), black, new Color(.84f, .61f, .36f),
            ("АВАРИЙНАЯ ПЕРЕГРУЗКА", 47), ("ТОЛЬКО ПОСЛЕ ИЗОЛЯЦИИ", 39));

        var knifeBay = Group(panel, "Breaker bay", new(.33f, .53f, .222f));
        Block(knifeBay, "Breaker back plate", Vector3.zero, new(.551f, .45f, .055f), steel, true);
        Fit(knifeBay, "Imported Type C knife switch", "early_1900s_knife_switches_type_c.glb", new(0, -.157f, .063f),
            new(.115f, .312f, .062f), Vertical, r => r.name == "Object_8");
        var knife = Group(knifeBay, "K17_KnifeSwitch", new(0, -.115f, .095f));
        Cylinder(knife, "Knife pivot axle", Vector3.zero, .014f, .106f, steel, Quaternion.Euler(0, 0, 90));
        foreach (float x in new[] { -.025f, .025f })
            Block(knife, "Copper actuator bar", new(x, .044f, .016f), new(.012f, .094f, .018f), copper);
        Cylinder(knife, "Insulated pull grip", new(0, .093f, .023f), .017f, .098f, black, Quaternion.Euler(0, 0, 90));
        Plate(knifeBay, "Breaker caption", new(0, .195f, .031f), new(.365f, .047f), black, Ink, ("РУБИЛЬНИК КОНТУРА", 58));
    }

    private void Isolation(Transform panel)
    {
        var meter = Group(panel, "K17_IsolationGauge", new(.457f, 1.294f, .390f));
        Block(meter, "Recessed isolation scale", Vector3.zero, new(.216f, .113f, .022f), black);
        Plate(meter, "Isolation caption", new(0, .029f, .014f), new(.181f, .026f), black, Ink, ("ИЗОЛЯЦИЯ", 60));
        var scale = Group(meter, "Calibrated indicator rail", new(0, -.017f, .015f));
        for (int i = 0; i < 10; i++)
            Block(scale, "Scale segment", new(-.078f + .0173f * i, 0, 0), new(.012f, .014f, .007f), i == 9 ? amber : steel);
        Plate(meter, "Scale limits", new(0, -.042f, .015f), new(.175f, .019f), black, Ink, ("0%                        100%", 45));
    }

    private void Gauge(Transform panel, string name, Vector3 center, float radius, float value)
    {
        var dial = Group(panel, name, center);
        Cylinder(dial, "Bezel", new(0, 0, .008f), radius, .030f, black, Face);
        Cylinder(dial, "Ivory dial", new(0, 0, .025f), radius * .84f, .009f, ceramic, Face);
        for (int i = 0; i <= 8; i++)
        {
            float angle = 135f - 22.5f * i;
            var mark = Block(dial, "Calibration tick", Quaternion.Euler(0, 0, angle) * Vector3.right * (radius * .681f) +
                Vector3.forward * .034f, new(.013f, i % 2 == 0 ? .0042f : .0026f, .004f), black);
            mark.localRotation = Quaternion.Euler(0, 0, angle);
        }
        var needle = Group(dial, "Needle pivot", new(0, -.019f, .036f));
        needle.localRotation = Quaternion.Euler(0, 0, 52f - value * 116f);
        Block(needle, "Needle", new(0, .026f, 0), new(.005f, .067f, .005f), red);
        Cylinder(dial, "Needle pin", new(0, -.019f, .040f), .008f, .008f, black, Face);
    }

    private void Indicator(Transform panel, string name, Vector3 at, Material lens)
    {
        var assembly = Group(panel, name, at);
        Cylinder(assembly, "Indicator bezel", Vector3.zero, .018f, .024f, black, Face);
        Cylinder(assembly, "Ceramic lens", new(0, 0, .016f), .0117f, .012f, lens, Face);
    }
}
