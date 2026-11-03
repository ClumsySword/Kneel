using UnityEngine;

// The bow's string. The bow mesh has none, so it's drawn at runtime and can follow the drawing hand, then snap back
// when the arrow goes. The archer calls UpdateString at the end of its LateUpdate, once the hand has its final pose.
public class BowString : MonoBehaviour
{
    [SerializeField]
    private Transform top;

    [SerializeField]
    private Transform bottom;

    [SerializeField]
    private LineRenderer line;

    // How fast the shiver after a release dies away (1/s), and how fast it swings (radians/s).
    [SerializeField]
    private float snapDamping = 26f;

    [SerializeField]
    private float snapFrequency = 80f;

    private Transform puller;
    private Vector3 releasedOffset;
    private float releasedAt;

    public void Pull(Transform hand) => puller = hand;

    public void Release()
    {
        if (puller == null)
            return;

        releasedOffset = transform.InverseTransformVector(puller.position - Middle);
        releasedAt = Time.time;
        puller = null;
    }

    public void UpdateString()
    {
        Vector3 nock = Middle;
        if (puller != null)
        {
            nock = puller.position;
        }
        else
        {
            float since = Time.time - releasedAt;
            nock += transform.TransformVector(releasedOffset) * (Mathf.Exp(-snapDamping * since) * Mathf.Cos(snapFrequency * since));
        }

        line.SetPosition(0, top.position);
        line.SetPosition(1, nock);
        line.SetPosition(2, bottom.position);
    }

    private Vector3 Middle => (top.position + bottom.position) * 0.5f;
}
