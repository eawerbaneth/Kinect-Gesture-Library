using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Naive_Bayesian : MonoBehaviour {
	
	//mu_c is mean of values in x associated with class c, sigma_c^2 is variance
	//probablity of value in clas P(v|c) is computed by plugging v into the equation 
	//	 for a normal distribution parameterized by mu and sigma^2
	
	// P(v|c) = 1/sqrt(2Pi*sigma^2)*e^-(v - mu)^2/2Sigma^2
	
	float parameter(float val, float mu, float sigmasq){
		//multiply, then divide
		return Mathf.Exp(-Mathf.Pow((val - mu), 2) / (2*sigmasq)) / Mathf.Sqrt(2*Mathf.PI*sigmasq);
		
	}
	
	Vector3 variance(List <Vector3> l){
		float n = (float)l.Count;
		Vector3 xbar = Vector3.zero;
		foreach(Vector3 e in l)
			xbar += e;
		
		
		xbar = xbar/n;
		
		if(n <= 1)
			return Vector3.zero;
		else{
			float a, b, c = 0;
			foreach(Vector3 e in l){
				a += Mathf.Pow(e.x - xbar.x, 2);
				b += Mathf.Pow(e.y - xbar.y, 2);
				c += Mathf.Pow(e.z - xbar.z, 2);
			}
			a = a / (n - 1.0);
			b = b / (n - 1.0);
			c = c / (n - 1.0);
			return new Vector3(a, b, c);
		}
		
	}
	
	
	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}
	
	
}
