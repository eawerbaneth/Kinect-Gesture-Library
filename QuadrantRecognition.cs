//-----------------------------------------------------------------------
// <copyright file="QuadrantRecognition.cs" company="eawerbaneth">
//     Open Source. Do with this as you will. Include this statement or 
//     don't - whatever you like.
//
//     No warranty or support given. No guarantees this will work or meet
//     your needs. Some elements of this project have been tailored to
//     the authors' needs and therefore don't necessarily follow best
//     practice. 
//
//     Code it and eat it :~)
// </copyright>
//-----------------------------------------------------------------------

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

///<summary>
/// This script analyzes the current 3-D position of a joint and places it in a 
/// control quadrant. Ideal for continuous movement, but not recognizing gesutres
/// proper.
///
/// Instruction: This script gets attached to a game object that you want to control.
///
/// For this example, I was only looking at shoulder, elbow, and hand movement in 
/// both arms, but extend as needed. I have a skeleton in my scene that reflects the literal 
/// movements of the detected skeleton, which left_arm and right_arm is monitoring.
/// 
///</summary>
public class QuadrantRecognition : MonoBehaviour {
	
	//struct to keep track of an individual joint position at a given time
	// could store more complex information here, but for now it's only position
	public struct JointPositionSnapshot{
		public Vector3 position;
		public JointPositionSnapshot(Vector3 pos){	
			position = pos;
		}
	};
	
	//struct to handle arm states (recognition will examine a series of snapshots)
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
		//returns the length of the left arm (a bit of easy vector math here)
		public float GetLength(){
			return (left_joints[0].position - left_joints[1].position).magnitude
				+ (left_joints[1].position - left_joints[2].position).magnitude;			
		}
	};
	
	
	//we're going to keep track of the arm joints to interpret movement
	//and relay that the DropletMovement
	public GameObject [] left_arm;
	public GameObject [] right_arm;
	
	// these are used for gesture recognition proper, don't see any action for 
	// Quadrants just yet
	private List <ArmsSnapshot> arm_states = new List<ArmsSnapshot>();
	public List <string> motions = new List<string>();
	public bool updated = false;
	float reset_timer = 0.0f;
	
	// Use this for initialization
	void Start () {
		// save our first positions (doesn't matter if Kinect isn't detecting anything
		// yet, we just need a starting point
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
		//finally, detect quadrant
		Quadrants();
	}
	
	//we don't want to have to analyze too many states, so we're going to have
	//old states decay after a time (10 seconds here)
	void Decay(){
		for(int i = 0; i < arm_states.Count; i++){
			if(arm_states.Count == 1)
				return;
			
			if(arm_states[i].timestamp > Time.time - 10f)
				return;
			else{
				arm_states.RemoveAt(i);
				i--;
			}
		}
		
	}
	
	
	void DetectMovement(){
		//check to see if any of the positions changed before we record anything
		if(arm_states[arm_states.Count-1].CompareStates(left_arm, right_arm)){
			arm_states.Add(new ArmsSnapshot(left_arm, right_arm));
			updated = true;	
		}
	}
	
	//we're going to look at the ways that the points are moving
	//and translate that into 5 directions (adjust as needed)
	void Quadrants(){
		// we are only analyzing current state right now!
		
		ArmsSnapshot cur_state = arm_states[arm_states.Count-1];
		float left_out;
		float right_out;
		float arm_length = cur_state.GetLength();
		
		//TODO: zero out current movements
		
		
		//left_out and right_out are going to be on a scale from 0 to 1
		//	0 all the way in, 1 all the way out
		left_out = (cur_state.left_joints[2].position.x - cur_state.left_joints[0].position.x)/arm_length;
		right_out = (cur_state.right_joints[0].position.x - cur_state.right_joints[2].position.x)/arm_length;
		
		
		//both hands forward
		if(left_out < 0.5 && right_out < 0.5)
			//TODO: adjust movement
		//both arms out, left slanted down, right slanted up (think airplane)
		if(cur_state.left_joints[0].position.y < cur_state.left_joints[2].position.y - 3*arm_length/4
		   && cur_state.right_joints[0].position.y > cur_state.right_joints[2].position.y - arm_length/4)
			//TODO: adjust movement
		//both arms out, left up, right down
		else if(cur_state.left_joints[0].position.y > cur_state.left_joints[2].position.y - arm_length/4
		         && cur_state.right_joints[0].position.y < cur_state.right_joints[2].position.y - 3*arm_length/4)
			//TODO: adjust movement
		//INVERTED controls - up and down
		if(cur_state.left_joints[0].position.y < cur_state.left_joints[2].position.y - 3*arm_length/4
		   && cur_state.right_joints[0].position.y < cur_state.right_joints[2].position.y - 3*arm_length/4){
			//set their up movement based on extremity of their arm motion
			float displacement = (cur_state.left_joints[2].position.y - cur_state.left_joints[0].position.y);
			//TODO: adjust movement: displacement/arm_length;
		}
		
		
		else if(cur_state.left_joints[0].position.y > cur_state.left_joints[2].position.y - arm_length/4
		   && cur_state.right_joints[0].position.y > cur_state.right_joints[2].position.y - arm_length/4){
			float displacement = (cur_state.left_joints[0].position.y - cur_state.left_joints[2].position.y);
			//TODO: adjust movement: displacement/arm_length;
		}
		
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
			//TODO: handle reset
		}
		else
			reset_timer = 0f;
		
	}
}
