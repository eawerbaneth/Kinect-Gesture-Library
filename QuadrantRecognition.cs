using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MotionInterpreter : MonoBehaviour {
	
	//struct to keep track of an individual joint position at a given time
	public struct JointPositionSnapshot{
		public Vector3 position;
		public JointPositionSnapshot(Vector3 pos){	
			position = pos;
		}
	};
	
	string state = "none";
	public Texture2D[] states;
	
	
	float reset_timer = 0.0f;
	
	//struct to handle arm states
	public struct ArmsSnapshot{
		public float timestamp;
		public JointPositionSnapshot[] left_joints;
		public JointPositionSnapshot[] right_joints;
		//calling this will take a snapshot of the arms and record the time
		public ArmsSnapshot(GameObject[] left, GameObject[] right){
			left_joints = new JointPositionSnapshot[3];
			right_joints = new JointPositionSnapshot[3];
			timestamp = Time.time;
			for(int i = 0; i < 3; i++){
				left_joints[i] = new JointPositionSnapshot(left[i].transform.position);
				right_joints[i] = new JointPositionSnapshot(right[i].transform.position);
			}
			
			//testing - print when we make a new state
			//Debug.Log("Making an arm snapshot - " + timestamp);
		}
		public bool CompareStates(GameObject[] left, GameObject[] right){
			//if anything has changed, return true, else return false
			for(int i = 0; i < 3; i++){
				if(left[i].transform.position != left_joints[i].position)
					return true;
				if(right[i].transform.position != right_joints[i].position)
					return true;
			}
			return false;			
		}
		public float GetLength(){
			return (left_joints[0].position - left_joints[1].position).magnitude
				+ (left_joints[1].position - left_joints[2].position).magnitude;			
		}
	};
	
	
	public DropletMovement droplet;
	
	//we're going to keep track of the arm joints to interpret movement
	//and relay that the DropletMovement
	public GameObject [] left_arm;
	public GameObject [] right_arm;
	
	//we're going to save a queue of positions for now and analyze recent strings
	//for movements we are checking for
	private List <ArmsSnapshot> arm_states = new List<ArmsSnapshot>();
	//store detected motions here
	public List <string> motions = new List<string>();
	public bool updated = false;
	
	
	void OnGUI(){
		if(state == "none")
			return;
		if(state == "left")
			GUI.Label(new Rect(25, Screen.height-125, 100, 100), states[0]);
		if(state == "right")
			GUI.Label(new Rect(25, Screen.height-125, 100, 100), states[1]);
		if(state == "up")
			GUI.Label(new Rect(25, Screen.height-125, 100, 100), states[2]);
		if(state == "down")
			GUI.Label(new Rect(25, Screen.height-125, 100, 100), states[3]);
		if(state == "reset")
			GUI.Label(new Rect(25, Screen.height-125, 100, 100), states[4]);
		if(state == "stop")
			GUI.Label(new Rect(25, Screen.height-125, 100, 100), states[5]);
		
		
	}
	
	
	// Use this for initialization
	void Start () {
		//if we aren't using the Kinect, turn this off
		if(!droplet.isKinect){
			this.enabled = false;
			return;
		}
		
		//save our first positions
		arm_states.Add(new ArmsSnapshot(left_arm, right_arm));
		
	}
	
	// Update is called once per frame
	void Update () {
		updated = false;
		
		//check for a new arm state
		DetectMovement();
		//have arm states decay over time (but always have one in the chamber)
		if(arm_states.Count > 1)
			Decay();
		//finally, interpret gestures
		InterpretGestures();
		
		
		
		//check for level reset
		if(reset_timer > 3.0f)
			Application.LoadLevel("DemoScene");
	}
	
	
	void Decay(){
		for(int i = 0; i < arm_states.Count; i++){
			if(arm_states[i].timestamp > Time.time - 10f)
				return;
			else{
				arm_states.RemoveAt(i);
				i--;
			}
		}
		
	}
	
	
	void DetectMovement(){
		if(arm_states.Count > 0){
		//check to see if any of the positions changed before we record anything
		if(arm_states[arm_states.Count-1].CompareStates(left_arm, right_arm)){
			arm_states.Add(new ArmsSnapshot(left_arm, right_arm));
			updated = true;	
		}
		}
	}
	
	//we're going to look at the ways that the points are moving
	//and translate that into 5 directions (forward is controlled by the river)
	void InterpretGestures(){
		//note: for the rush segments, we care more about current state
		//than changes in state, but other games will be slightly different
		//Debug.Log(arm_states.Count-1);
		
		
		if(arm_states.Count == 0)
			return;
		
		ArmsSnapshot cur_state = arm_states[arm_states.Count-1];
		float left_out;
		float right_out;
		float arm_length = cur_state.GetLength();
		//look at just elbow length
		float _arm_length = (cur_state.left_joints[0].position - cur_state.left_joints[1].position).magnitude;
		
		//reset droplet movements
		droplet.back = droplet.left = droplet.right = droplet.up = droplet.down = 0f;
		
		//left_out and right_out are going to be on a scale from 0 to 1
		//	0 all the way in, 1 all the way out
		left_out = (cur_state.left_joints[2].position.x - cur_state.left_joints[0].position.x)/arm_length;
		right_out = (cur_state.right_joints[0].position.x - cur_state.right_joints[2].position.x)/arm_length;
		
		
		//NEW METHOD - create idealized gestures and measure angle displacement from those
		Vector3 _left_diag_down = Quaternion.Euler(0, 0, 30)*new Vector3(-_arm_length, 0, 0);
		Vector3 _left_diag_up = Quaternion.Euler(0, 0, -30)*new Vector3(-_arm_length, 0, 0);
		Vector3 _left_up = Quaternion.Euler(0, 0, -45)*new Vector3(-_arm_length, 0, 0);
		Vector3 _left_down = Quaternion.Euler(0, 0, 45)*new Vector3(-_arm_length, 0, 0);
		
		Vector3 _right_diag_down = Quaternion.Euler(0, 0, -30)*new Vector3(_arm_length, 0, 0);
		Vector3 _right_diag_up = Quaternion.Euler(0, 0, 30)*new Vector3(_arm_length, 0, 0);
		Vector3 _right_up = Quaternion.Euler(0, 0, 45)*new Vector3(_arm_length, 0, 0);
		Vector3 _right_down = Quaternion.Euler(0, 0, -45)*new Vector3(_arm_length, 0, 0);
		
		Vector3 _hand_forward = new Vector3(0, 0, _arm_length);
		
		//current state vectors
		Vector3 _left_wing = cur_state.left_joints[0].position - cur_state.left_joints[1].position;
		Vector3 _right_wing = cur_state.right_joints[0].position - cur_state.right_joints[1].position;
		
		
		//find displacements
		//back - both hands forward
		if(Mathf.Abs(Vector3.Angle(_hand_forward, _left_wing)) < 45 && Mathf.Abs(Vector3.Angle(_hand_forward, _right_wing)) < 45){
			state = "stop";
		}
		//left - left diag down, right diag up
		else if(Mathf.Abs(Vector3.Angle(_left_diag_down, _left_wing)) < 45 && Mathf.Abs(Vector3.Angle(_right_diag_up, _right_wing)) < 45){
			state = "left";
		}
		//right - left diag up, right diag down
		else if(Mathf.Abs(Vector3.Angle(_left_diag_up, _left_wing)) < 45 && Mathf.Abs(Vector3.Angle(_right_diag_down, _right_wing)) < 45){
			state = "right";
		}
		//inverse controls - UP and DOWN
		else if(Mathf.Abs(Vector3.Angle(_left_up, _left_wing)) < 45 && Mathf.Abs(Vector3.Angle(_right_up, _right_wing)) < 45){
			float displacement = (cur_state.left_joints[0].position.y - cur_state.left_joints[2].position.y);
			
			state = "down";
		}
		else if(Mathf.Abs(Vector3.Angle(_left_down, _left_wing)) < 45 && Mathf.Abs(Vector3.Angle(_right_down, _right_wing)) < 45){
			//set their up movement based on extremity of their arm motion
			float displacement = (cur_state.left_joints[2].position.y - cur_state.left_joints[0].position.y);
			
			state = "up";
		}
		else state = "none";
		
		
		//RESET APPLICATION CONTROLS
		Vector3 arm = cur_state.right_joints[0].position - cur_state.right_joints[2].position;
		Vector3 down = new  Vector3(cur_state.right_joints[2].position.x, 
			cur_state.right_joints[2].position.y-1,
			cur_state.right_joints[2].position.z) - cur_state.right_joints[2].position;
		Vector3 larm = cur_state.left_joints[0].position - cur_state.left_joints[2].position;
		Vector3 ldown = new  Vector3(cur_state.left_joints[2].position.x, 
			cur_state.left_joints[2].position.y-1,
			cur_state.left_joints[2].position.z) - cur_state.left_joints[2].position;
		
		
		//get angle between the vectors
		float angle = Vector3.Angle(down, arm);
		//Debug.Log(right_out + " Angle is " + angle + " " + reset_timer + " " + Time.deltaTime);
		float langle = Vector3.Angle(ldown, larm);
		
		//arm must be seventy percent extended and shoulder to hand is twenty to forty degrees from downwards
		if(right_out > .5 && (5 < angle && angle < 60) && langle < 15 ){
			reset_timer += Time.deltaTime;
			state = "reset";
		}
		else
			reset_timer = 0f;
		
	}
}
