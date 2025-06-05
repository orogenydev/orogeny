using UnityEngine;
using System.Collections;

public class Clouds : MonoBehaviour {
    public int pixWidth;
    public int pixHeight;
    public float xOrg=0f;
    public float yOrg=0f;
    public float scale = 1F;
    private Texture2D noiseTex;
    private Color[] pix;
    void Start() {
		ApplyTex ();
    }

	void ApplyTex(){
		noiseTex = new Texture2D(pixWidth, pixHeight);
		pix = new Color[noiseTex.width * noiseTex.height];
		this.transform.GetComponent<Renderer>().material.SetTexture("_NoiseTex",noiseTex);

	}
    void CalcNoise() {
		//Creating a transparency map based on perlin noise to make a fake dynamic clouds effect
        float y = 0.0F;
        while (y < noiseTex.height) {
            float x = 0.0F;
            while (x < noiseTex.width) {
                float xCoord = (xOrg + x) / noiseTex.width;
                float yCoord = (yOrg + y) / noiseTex.height;
                float sample = Mathf.PerlinNoise(xCoord* scale, yCoord* scale)+Mathf.PerlinNoise(xCoord*2* scale, yCoord*2* scale)/2+Mathf.PerlinNoise(xCoord*4* scale, yCoord*4* scale)/4+Mathf.PerlinNoise(xCoord*8* scale, yCoord*8* scale)/8-.3f;
					pix[(int)(y * noiseTex.width + x)] = new Color(sample, sample, sample);
                x++;
            }
            y++;
        }
        noiseTex.SetPixels(pix);
        noiseTex.Apply();
    }
    void Update() {
		if (this.transform.GetComponent<Renderer>().material.GetTexture ("_NoiseTex") == null) {
			ApplyTex();		
		}
		this.transform.Rotate(Vector3.forward*.01f);
		CalcNoise();
		xOrg+=(1f+Mathf.Sin(Time.time*Mathf.PI/180f))/10f/scale;
		yOrg+=(1f+Mathf.Sin(Time.time*Mathf.PI/180f))/10f/scale;
		
    }
}
