using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Player))]
[RequireComponent(typeof(PlayerGroundSlam), typeof(PlayerCounter))]
public class PlayerSkillController : MonoBehaviour
{
    private Player player;
    private PlayerGroundSlam groundSlam;
    private PlayerCounter counter;
    private bool isSubscribed;

    private void Awake()
    {
        player = GetComponent<Player>();
        groundSlam = GetComponent<PlayerGroundSlam>();
        counter = GetComponent<PlayerCounter>();
    }

    private void OnEnable() => SubscribeToInput();
    private void Start() => SubscribeToInput();

    private void SubscribeToInput()
    {
        if (isSubscribed || player == null || player.input == null) return;

        player.input.Player.GroundSlam.performed += OnGroundSlamPerformed;
        player.input.Player.Counter.performed += OnCounterPerformed;
        isSubscribed = true;
    }

    private void OnDisable()
    {
        if (isSubscribed && player != null && player.input != null)
        {
            player.input.Player.GroundSlam.performed -= OnGroundSlamPerformed;
            player.input.Player.Counter.performed -= OnCounterPerformed;
        }
        isSubscribed = false;
        player.ClearBufferedAction(PlayerAction.GroundSlam);
        player.ClearBufferedAction(PlayerAction.Counter);
        if (groundSlam != null) groundSlam.Cancel();
        if (counter != null) counter.Cancel();
    }

    private void OnGroundSlamPerformed(InputAction.CallbackContext context) => player.RequestAction(PlayerAction.GroundSlam);
    private void OnCounterPerformed(InputAction.CallbackContext context) => player.RequestAction(PlayerAction.Counter);
}
