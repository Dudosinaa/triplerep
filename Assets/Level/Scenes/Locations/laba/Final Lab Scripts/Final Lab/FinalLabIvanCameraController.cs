using System.Collections;
using UnityEngine;

internal sealed class FinalLabIvanCameraController
{
    private readonly Camera camera;
    private readonly Vector3 homePosition;
    private readonly Quaternion homeRotation;
    private readonly float homeFov;

    public FinalLabIvanCameraController(Camera camera)
    {
        this.camera = camera;
        homePosition = camera.transform.localPosition;
        homeRotation = camera.transform.localRotation;
        homeFov = camera.fieldOfView;
    }

    public IEnumerator LookAtDoor(Vector3 door)
    {
        var pose = new Pose(camera.transform.position, Looking(camera.transform.position, door));
        yield return Glide(pose, 61f, .83f);
    }

    public void Follow(Vector3 eye, Vector3 target, float fieldOfView, float delta)
    {
        var transform = camera.transform;
        float fraction = 1f - Mathf.Exp(-5.8f * delta);
        transform.position = Vector3.Lerp(transform.position, eye, fraction);
        transform.rotation = Quaternion.Slerp(transform.rotation, Looking(transform.position, target), fraction);
        camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, fieldOfView, fraction);
    }

    public IEnumerator FrameConversation(Vector3 eye, Vector3 target) => Glide(new Pose(eye, Looking(eye, target)), 53f, .40f);

    public IEnumerator Return()
    {
        var parent = camera.transform.parent;
        yield return Glide(new Pose(parent.TransformPoint(homePosition), parent.rotation * homeRotation), homeFov, .72f);
        Restore();
    }

    public void Restore()
    {
        if (camera == null) return;
        camera.transform.SetLocalPositionAndRotation(homePosition, homeRotation);
        camera.fieldOfView = homeFov;
    }

    private IEnumerator Glide(Pose destination, float fieldOfView, float duration)
    {
        var transform = camera.transform;
        var source = new Pose(transform.position, transform.rotation);
        float fov = camera.fieldOfView;
        for (float time = 0; time < duration; time += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0, 1, time / duration);
            transform.SetPositionAndRotation(Vector3.Lerp(source.position, destination.position, t), Quaternion.Slerp(source.rotation, destination.rotation, t));
            camera.fieldOfView = Mathf.Lerp(fov, fieldOfView, t);
            yield return null;
        }
        transform.SetPositionAndRotation(destination.position, destination.rotation);
        camera.fieldOfView = fieldOfView;
    }

    private static Quaternion Looking(Vector3 eye, Vector3 target) => Quaternion.LookRotation(target - eye, Vector3.up);
}
