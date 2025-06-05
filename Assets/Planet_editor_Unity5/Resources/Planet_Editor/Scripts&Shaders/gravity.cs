using UnityEngine;
using System.Collections;

public class gravity : MonoBehaviour {

public Transform planet;
private float g = -9.81f;
void  FixedUpdate (){
    // get the direction vector and normalize it (magnitude = 1)
    Vector3 direction = transform.position-planet.transform.position;
    Vector3 force = direction.normalized * g;
 
    // accelerate the object in that direction
    this.GetComponent<Rigidbody>().AddForce(force, ForceMode.Acceleration);
	
}
}