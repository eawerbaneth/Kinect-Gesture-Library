using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class cow_movement : MonoBehaviour {
	
	public GameObject hand;
	public GameObject other;
	public GameObject cam;
	public GameObject river;
	
	//broadcast what we are currently selecting
	public string selection = "";
	public GameObject selected;
	
	Rect button = new Rect(50, 50, 100, 50);
	Rect cancel = new Rect(150, 50, 100, 50);
	Rect accept = new Rect(50, 100, 100, 50);
	
	public class state{
		public Vector3 pos;
		public float time;
		
		public state(){
			pos = Vector3.zero;
			time = -1f;
		}
		public state(Vector3 _pos){
			pos = _pos;
			time = Time.time;
		}
		
	};
	
	List <state> history = new List<state>();
	
	// Use this for initialization
	void Start () {
		history.Add(new state(hand.transform.position));
	
	}
	
	void Decay(){
		
		for(int i = 0; i < history.Count; i++){
			if(history[i].time + 3f > Time.time || history.Count == 1)
				break;
			else{
				history.RemoveAt(i);
				i--;
			}
		}
		
	}
	
	void OnGUI(){
		if(selection == "buy"){
			GUI.Label(cancel, "cancel");
			GUI.Label(accept, "accept");	
		}		
	}
	
	
	
	Vector3 AverageHistory(){
		//we're going to look at the last second of data
		Vector3 avg_delta = Vector3.zero;
		
		for(int i = history.Count - 1; i > 0; i--){
			if(history[i].time > Time.time - 1f){
				//add the delta!
				Vector3 delta = history[i].pos - history[i-1].pos;
				avg_delta += delta;				
			}			
		}
		
		return avg_delta;
		
	}
	
	
	
	void CheckSelect(){
		Vector3 dir = transform.position - cam.transform.position;
		
		//buy button
		
		
				
		Vector3 point = Camera.mainCamera.WorldToScreenPoint(transform.position);		
		Ray ray = new Ray(transform.position + dir/5, dir);
		RaycastHit hit;
		
		//check to see if we are hovering over a button
		if(button.Contains(new Vector2(point.x, Screen.height - point.y))){
			Debug.Log("trying to buy a fence " + Time.deltaTime);
			selection = "buy";
			
		}
		else if(selection == "buy" && cancel.Contains(new Vector2(point.x, Screen.height - point.y)))
			selection = "";
		else if(selection == "buy" && accept.Contains(new Vector2(point.x, Screen.height - point.y))){
			selection = "";
			riverscript r = river.GetComponent<riverscript>();
			if(r.money >= 100 && other.GetComponent<cow_movement>().selection == "fence"){
				other.GetComponent<cow_movement>().selected.GetComponent<MeshRenderer>().enabled = true;
				other.GetComponent<cow_movement>().selected.GetComponent<Collider>().enabled = true;
				r.money -= 100;
				r.fences++;				
			}		
		}
		//check to see if we are hovering over an object
		else if(Physics.SphereCast(ray, 1f, out hit, 1000)){
			if(hit.transform.name.Contains("poop")){
				selection = "poop";
				Debug.Log("Hit a poop!" + Time.time);	
				
				//check history, if trend is upwards, collect the poop
				Vector3 delta = AverageHistory();
				if(delta.y > 0){
					hit.transform.GetComponent<poopscript>().OnMouseDown();
					Debug.Log("collecting crap");
				}
				
			}
			else if(hit.transform.name.Contains("cow")){
				selection = "cow";
				Debug.Log("Hit a cow! " + Time.time);
				
				
				//check our history, if we've had a trend opposite of the river, push the cow away
				Vector3 delta = AverageHistory();
				if(transform.position.x < river.transform.position.x){
					if(delta.x < 0)	{
						hit.transform.GetComponent<cowscript>().OnMouseDown();
						Debug.Log("shooing cow");	
					}
				}
				else if(delta.x > 0){
					hit.transform.GetComponent<cowscript>().OnMouseDown();
					Debug.Log("shooing cow");
				}
				
			}
			//possibility of placing a fence, check to see if our other hand is on the fence
			//button
			else if(hit.transform.name.Contains("fence") && !hit.transform.gameObject.GetComponent<MeshRenderer>().enabled){
				selection = "fence";
				selected = hit.transform.gameObject;
			}
		}
		else
			selection = "";
	}
	
	
	
	// Update is called once per frame
	void Update () {
		
		
		//for now, just move based on our x and y axes
		if(hand.transform.position != history[history.Count - 1].pos){
			history.Add(new state(hand.transform.position));
			Vector3 trans = new Vector3((history[history.Count - 1].pos.x - history[history.Count - 2].pos.x)*5, (history[history.Count - 1].pos.y - history[history.Count - 2].pos.y)*5, 0);
			transform.Translate(trans);
			Debug.Log(hand.name + " " + trans);
		}
		Decay();
		CheckSelect();
		
	
	}
	
	
	
	
	
}