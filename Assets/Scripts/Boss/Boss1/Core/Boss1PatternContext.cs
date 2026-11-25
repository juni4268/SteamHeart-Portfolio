using System.Collections;
using UnityEngine;

public sealed class Boss1PatternContext
{
    public Boss1PatternContext(
        Boss1Controller controller,
        Boss1HandController leftHand,
        Boss1HandController rightHand)
    {
        Controller = controller;
        LeftHand = leftHand;
        RightHand = rightHand;
    }

    public Boss1Controller Controller { get; }
    public Boss1HandController LeftHand { get; }
    public Boss1HandController RightHand { get; }
    public Transform BossTransform => Controller.transform;

    public IEnumerator WaitForHands()
    {
        return Controller.WaitForHands();
    }
}
