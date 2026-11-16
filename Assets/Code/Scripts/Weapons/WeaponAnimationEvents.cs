using UnityEngine;
using UnityEngine.Scripting;

// Unity animation events are delivered to the GameObject that owns the Animator.
public sealed class WeaponAnimationEvents : MonoBehaviour
{
    public FirstPersonWeaponView owner;
    [Preserve]
    public void OnMagazineInserted(AnimationEvent animationEvent)
    {
        if (owner != null) owner.ReceiveMagazineInsertion(animationEvent.intParameter);
    }
}
