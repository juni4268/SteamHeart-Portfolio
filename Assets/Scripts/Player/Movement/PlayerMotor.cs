using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMotor : MonoBehaviour
{
    private Rigidbody2D rb;

    public Vector2 Velocity => Rigidbody.linearVelocity;
    public float GravityScale => Rigidbody.gravityScale;

    private Rigidbody2D Rigidbody
    {
        get
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
            }

            return rb;
        }
    }

    private void Awake()
    {
        rb = Rigidbody;
    }

    public void SetVelocity(Vector2 velocity)
    {
        Rigidbody.linearVelocity = velocity;
    }

    public void SetVelocity(float x, float y)
    {
        Rigidbody.linearVelocity = new Vector2(x, y);
    }

    public void SetVelocityX(float x)
    {
        Rigidbody.linearVelocity = new Vector2(x, Rigidbody.linearVelocity.y);
    }

    public void SetVelocityY(float y)
    {
        Rigidbody.linearVelocity = new Vector2(Rigidbody.linearVelocity.x, y);
    }

    public void Stop()
    {
        Rigidbody.linearVelocity = Vector2.zero;
    }

    public void SetPosition(Vector2 position)
    {
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        Rigidbody.position = position;
    }

    public void SetGravityScale(float gravityScale)
    {
        Rigidbody.gravityScale = gravityScale;
    }
}
