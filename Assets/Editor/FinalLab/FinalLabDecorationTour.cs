using System.Collections;
using UnityEngine;

public static partial class FinalLabVerification
{
    private static IEnumerable DecoratedTour(FirstPersonPlayer player, Camera camera, CharacterController motor)
    {
        foreach (var frame in Capture(camera, "21-hall-from-entrance", new Vector3(0.10f, 1.68f, 6.00f),
            new Vector3(0.03f, 1.05f, 11.04f), fieldOfView: 67)) yield return frame;
        foreach (var frame in Capture(camera, "22-ivan-workspace", new Vector3(-2.02f, 1.61f, 10.57f),
            new Vector3(-4.53f, 0.82f, 11.37f), fieldOfView: 56)) yield return frame;
        foreach (var frame in Capture(camera, "23-experiment", new Vector3(2.60f, 1.56f, 10.07f),
            new Vector3(-0.16f, 1.06f, 11.42f), fieldOfView: 57)) yield return frame;
        foreach (var frame in Capture(camera, "24-control-room", new Vector3(-1.47f, 1.66f, 14.62f),
            new Vector3(-2.70f, 1.37f, 16.72f), fieldOfView: 69)) yield return frame;
        foreach (var frame in Capture(camera, "25-containment", new Vector3(0.17f, 1.64f, 15.47f),
            new Vector3(2.75f, 0.96f, 16.97f), fieldOfView: 59)) yield return frame;
        foreach (var frame in Capture(camera, "26-power-trunk", new Vector3(3.12f, 1.55f, 12.25f),
            new Vector3(0.28f, 2.36f, 13.68f), fieldOfView: 70)) yield return frame;
        foreach (var frame in Capture(camera, "27-hall-from-rear", new Vector3(3.25f, 1.71f, 13.35f),
            new Vector3(0.18f, 1.04f, 8.29f), fieldOfView: 75)) yield return frame;
        foreach (var frame in Capture(camera, "28-utility-glass", new Vector3(2.89f, 1.38f, 9.07f),
            new Vector3(4.57f, 0.91f, 9.42f), fieldOfView: 43)) yield return frame;
        foreach (var frame in Capture(camera, "29-archive", new Vector3(-0.21f, 1.62f, 1.47f),
            new Vector3(0.70f, 1.27f, 4.27f), fieldOfView: 67)) yield return frame;
        foreach (var frame in Capture(camera, "30-bed-through-window", new Vector3(2.50f, 1.66f, 12.68f),
            new Vector3(2.56f, 0.88f, 16.75f), fieldOfView: 51)) yield return frame;
        foreach (var frame in Capture(camera, "31-recorder", new Vector3(2.21f, 1.50f, 12.22f),
            new Vector3(2.06f, 0.73f, 13.43f), fieldOfView: 43)) yield return frame;
        foreach (var frame in Capture(camera, "32-storage", new Vector3(-2.15f, 1.67f, 8.60f),
            new Vector3(-4.45f, 0.98f, 7.78f), fieldOfView: 57)) yield return frame;
        foreach (var frame in Capture(camera, "33-control-panel", new Vector3(-3.35f, 1.69f, 15.02f),
            new Vector3(-1.10f, 1.62f, 15.91f), fieldOfView: 57)) yield return frame;
        foreach (var frame in Capture(camera, "34-ivan-instrument-detail", new Vector3(-3.62f, 1.29f, 11.92f),
            new Vector3(-4.61f, 0.91f, 11.60f), fieldOfView: 47)) yield return frame;

        var route = FinalLabProps.DressingWalk;
        for (int index = 0; index < route.Length; index++)
        {
            var point = route[index];
            foreach (var frame in Walk(player, point.x, point.y, "dressed route waypoint " + (index + 1))) yield return frame;
            Require(motor.isGrounded && Mathf.Abs(player.transform.position.y) < 0.10f,
                "Grounded beyond waypoint " + (index + 1) + ".");
        }
        foreach (var frame in Walk(player, 0, 0.64f, "bottom of the existing stairs")) yield return frame;
        foreach (var frame in Walk(player, 0, -4.03f, "upper landing")) yield return frame;
        Require(motor.isGrounded && Mathf.Abs(player.transform.position.y - FinalLabBuilder.LandingHeight) < 0.09f,
            "Completed the furnished route and returned up the existing stairs.");
        foreach (var frame in CapturePlan(camera)) yield return frame;
    }
}
