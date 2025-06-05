using System.Collections;
using UnityEngine;

public class Movements : MonoBehaviour
{
    private int walkspeed;

    private int jumpHeight;

    private float distToGround;

    private float maxVelocityChange;

    private float dist = 3f;

    private RaycastHit hit;

    private Vector3 castPos;

    private Vector3 averageNormal_old;

    public GameObject planet;

    public float max_speed = 2f;

    private bool OnPlanet = false;

    void Start()
    {
        averageNormal_old = transform.up;
        distToGround = GetComponent<Collider>().bounds.extents.y;
    }

    void FixedUpdate()
    {
        float vel_m = this.GetComponent<Rigidbody>().velocity.magnitude;
        if (OnPlanet)
        {
            this
                .GetComponent<Rigidbody>()
                .AddForce((1f / vel_m) *
                transform.forward *
                Input.GetAxis("Vertical") /
                10f,
                ForceMode.VelocityChange);
            this
                .GetComponent<Rigidbody>()
                .AddTorque(-transform.up * Input.GetAxis("Horizontal") / 20f,
                ForceMode.VelocityChange);
        }

        Vector3 velocity = this.GetComponent<Rigidbody>().velocity;
        //this.GetComponent<Rigidbody>().velocity=new Vector3(velocity.x/vel_m,velocity.y/vel_m,velocity.z/vel_m);
    }

    void Update()
    {
        // Orient player upright
        Vector3 down =
            (planet.transform.position - transform.position).normalized;
        Vector3 forward = Vector3.Cross(transform.right, down);

        Player_rot(planet.GetComponent<MeshFilter>().mesh,
        this.transform.position);
        if (Input.GetKeyDown(KeyCode.Space))
        {
            //This sometimes doesn't work if put in a fixedupdate- frames are being missed
            this
                .GetComponent<Rigidbody>()
                .AddForce(-5f * transform.up, ForceMode.VelocityChange);
        }
        //if (Input.GetAxis("Vertical")==0f && Input.GetAxis("Horizontal")==0f && OnPlanet && !Input.GetKey(KeyCode.Space))
        //		this.GetComponent<Rigidbody>().velocity = this.GetComponent<Rigidbody>().velocity/2f;
    }

    void OnCollisionStay(Collision collisionInfo)
    {
        if (collisionInfo.collider.gameObject == planet)
            OnPlanet = true;
        else
            OnPlanet = false;
    }

    void Player_rot(Mesh mesh, Vector3 position)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        float sqrRadius = dist * dist;

        Vector3 averageNormal = averageNormal_old;
        for (int i = 0; i < vertices.Length; i++)
        {
            float sqrMagnitude = (vertices[i] - position).sqrMagnitude;

            // Early out if too far away
            if (sqrMagnitude > sqrRadius) continue;
            float distance = Mathf.Sqrt(sqrMagnitude);
            averageNormal += falloff(distance, dist) * normals[i];
        }
        averageNormal = averageNormal.normalized;
        Vector3 down =
            (planet.transform.position - transform.position).normalized;
        Vector3 forward = Vector3.Cross(transform.right, averageNormal);
        averageNormal_old = averageNormal;
        transform.rotation = Quaternion.LookRotation(-forward, -averageNormal);
    }

    float falloff(float distance, float dist)
    {
        return Mathf.Clamp01(1.0f - distance / dist);
    }
}
