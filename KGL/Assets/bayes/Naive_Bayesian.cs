using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using Kinect;

public class Naive_Bayesian : MonoBehaviour {
	//record utilities
	bool train = false;	
	bool recording = false;
	string train_name = "";
	
	public GUISkin skin;
	
	
	GameObject KinectPrefab;
	KinectInterface kinect;
	public DeviceOrEmulator devOrEmu;
	
	
	//types of joints
	/*
	 * 
	 * A - angle between shoulder-ground and humerus
	 * B - angle between shoulder-forward and humerus
	 * C - angle between humerus and radius
	 * D - angle between hip-ground and femur
	 * E - angle between hip-forward and femus
	 * F - angle between femur and tibia
	 * G - twist of torso (angle offset of clavicle and pelvis)
	 * 
	 * */
	
	//struct to keep track of an individual joint position at a given time
	public struct JointPositionSnapshot{
		public Vector3 position;
		float time;
		public JointPositionSnapshot(Vector3 pos){	
			position = pos;
			time = Time.time;
		}
	};
	
	public string state = "none";
	float reset_timer = 0.0f;
	
	//struct to help out with image recording
	public DisplayColor best_img;
	
	//BETH EDITING - polymorphic struct to handle grouped joints revised struct to handle arm states - single arm
	public abstract class State{
		public float timestamp;
		public Vector3 [] joints;
		public string type;
		
		public abstract float GetLength();
		public abstract List <float> GenerateAngles();
		
	}
	
	
	public class TorsoState: State{
		//for this, the only angle we care about is the offset of the clavicle and pelvis
		
		Vector3 clavicle;
		Vector3 hip;
		
		//calling this will take a snapshot of torso and record time
		public TorsoState(GameObject [] torso){
			joints = new Vector3[4];
			timestamp = Time.time;
			for(int i = 0; i < 4; i++)
				joints[i] = torso[i].transform.position;
			clavicle = joints[0] - joints[1];
			hip = joints[2] - joints[3];
			type = "torso";
		}
		public static bool operator ==(TorsoState a, TorsoState b){
			for(int i = 0; i < 4; i++){
				if(a.joints[i] != b.joints[i])
					return false;
				
			}
			return true;
		}
		public static bool operator !=(TorsoState a, TorsoState b){
			return !(a==b);
		}
		public static bool operator ==(TorsoState a, GameObject[] b){
			for(int i = 0; i < 4; i++)
				if(a.joints[i] != b[i].transform.position)
					return false;
			return true;
		}
		public static bool operator !=(TorsoState a, GameObject[] b){
			return !(a==b);
		}
		
		public override float GetLength(){
			//find middle of clavicle and middle of hip, return length of vector separating them
			Vector3 mid_clav = (joints[0] + joints[1])/2;
			Vector3 mid_hip = (joints[2] + joints[3])/2;
			
			Vector3 displacement = mid_hip - mid_clav;
			return displacement.magnitude;			
		}
		public override List <float> GenerateAngles(){
			//angles for toros is G - twist of torso (angle offset between clavicle and hip)
			List <float> angles = new List <float>();
			angles.Add(Vector3.Angle(clavicle, hip));
			
			return angles;
		}
		
	}
	
	public class LegState: State{
		//joints are stored hip, knee, ankle
		Vector3 hip_knee;
		Vector3 knee_ankle;
		
		//calling this will take a snapeshot of arm and record time
		public LegState(GameObject [] leg){
			joints = new Vector3[3];
			timestamp = Time.time;
			for(int i = 0; i < 3; i++)
				joints[i] = leg[i].transform.position;
			hip_knee = joints[1] - joints[0];
			knee_ankle = joints[2] - joints[1];
			type = "leg";
		}
		//used for determining whether or not Kinect is actively tracking
		public static bool operator ==(LegState a, LegState b){
			for(int i = 0; i < 3; i++)
				if(a.joints[i] != b.joints[i])
					return false;
			return true;	
		}
		public static bool operator !=(LegState a, LegState b){
			return !(a==b);
		}
		public static bool operator ==(LegState a, GameObject [] b){
			for(int i = 0; i < 3; i++)
				if(a.joints[i] != b[i].transform.position)
					return false;
			return true;			
		}
		public static bool operator !=(LegState a, GameObject [] b){
			return !(a==b);
			
		}
		
		public override float GetLength(){
			return hip_knee.magnitude + knee_ankle.magnitude;
		}
		public override List <float> GenerateAngles(){
			//angles for arm is ABC, armpit-x, armpit-y, and elbow hinge
			List <float> angles = new List<float>();
			//generate shoulder-ground
			Vector3 hip_ground = new Vector3(0, - 1, 0);
			//generate hip-forward
			Vector3 hip_forward = new Vector3(-1, 0, 0);
			//hip-x [shoulder_ground and shoulder_elbow)
			angles.Add(Vector3.Angle(hip_ground, hip_knee));
			//hip-y [shoulder_forward and shoulder_elbow)
			angles.Add(Vector3.Angle(hip_forward, hip_knee));
			//knee hinge
			angles.Add(Vector3.Angle(hip_knee, knee_ankle));
			
			return angles;			
		}
	}
	
	public class ArmState : State{
		//joints are stored shoulder, elbow, wrist
		Vector3 shoulder_elbow; 
		Vector3 elbow_wrist;
		
		
		//calling this will take a snapeshot of arm and record time
		public ArmState(GameObject [] arm){
			joints = new Vector3[3];
			timestamp = Time.time;
			for(int i = 0; i < 3; i++)
				joints[i] = arm[i].transform.position;
			shoulder_elbow = joints[1] - joints[0];
			elbow_wrist = joints[2] - joints[1];
			type = "arm";
		}
		//used for determining whether or not Kinect is actively tracking
		public static bool operator ==(ArmState a, ArmState b){
			for(int i = 0; i < 3; i++)
				if(a.joints[i] != b.joints[i])
					return false;
			return true;	
		}
		public static bool operator !=(ArmState a, ArmState b){
			return !(a==b);
		}
		public static bool operator ==(ArmState a, GameObject [] b){
			for(int i = 0; i < 3; i++)
				if(a.joints[i] != b[i].transform.position)
					return false;
			return true;			
		}
		public static bool operator !=(ArmState a, GameObject [] b){
			return !(a==b);
			
		}
		
		public override float GetLength(){
			return shoulder_elbow.magnitude + elbow_wrist.magnitude;
		}
		public override List <float> GenerateAngles(){
			//angles for arm is ABC, armpit-x, armpit-y, and elbow hinge
			List <float> angles = new List<float>();
			//generate shoulder-ground
			Vector3 shoulder_ground = new Vector3(0, - 1, 0);
			//generate shoulder-forward
			Vector3 shoulder_forward = new Vector3(-1, 0, 0);
			//armpit-x [shoulder_ground and shoulder_elbow)
			angles.Add(Vector3.Angle(shoulder_ground, shoulder_elbow));
			//armpit-y [shoulder_forward and shoulder_elbow)
			angles.Add(Vector3.Angle(shoulder_forward, shoulder_elbow));
			//elbow hinge
			angles.Add(Vector3.Angle(shoulder_elbow, elbow_wrist));
			
			return angles;			
		}
		
	}
	
	
	/*//struct to handle arm states
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
		public List <float> GenerateAngles(){
			List <float> angles = new List<float>();
			string line = "";
			
			Vector3 right_down = new Vector3(right_joints[2].position.x, right_joints[2].position.y - 1, right_joints[2].position.z);
			Vector3 left_down = new Vector3(left_joints[2].position.x, left_joints[2].position.y - 1, left_joints[2].position.z);
			//shoulder angles
			angles.Add(Vector3.Angle(left_joints[1].position-left_joints[2].position, left_down));
			angles.Add(Vector3.Angle(right_joints[1].position-right_joints[2].position, right_down));
			//elbow angles
			angles.Add(Vector3.Angle(left_joints[0].position - left_joints[1].position, 
				left_joints[2].position - left_joints[1].position));
			angles.Add(Vector3.Angle(right_joints[0].position - right_joints[1].position, 
				right_joints[2].position - right_joints[1].position));
			
			return angles;
			
		}
	};*/
	
	public class feature{
		public float mean;
		public float std_deviation;
		List <float> data = new List<float>();
		
		public feature(){}
		public feature(List <float> d){
			data = d;
			CalculateMean();
			CalculateStdDev();
			Debug.Log("Feature - Mean: " + mean + " Std Dev: " + std_deviation);
		}
		void CalculateMean(){
			float sum = 0;
			foreach(float d in data)
				sum += d;
			mean = sum/(float)data.Count;
		}
		
		void CalculateStdDev(){
			float sum_sqr_diff = 0;
			foreach(float d in data)
				sum_sqr_diff += Mathf.Pow(mean - d, 2);
			std_deviation = Mathf.Sqrt(sum_sqr_diff/data.Count);
		}
		public float CalculateProbability(float instance){
			//(1/sqrt(2*pi*std_dev^2)*e^(-(data - mean)^2/(2*std_dev^2))
			
			float alpha = 1/(Mathf.Sqrt(2*Mathf.PI*Mathf.Pow(std_deviation, 2)));
			float beta = -((Mathf.Pow(instance - mean, 2)/(2f*Mathf.Pow(std_deviation, 2))));
				//Mathf.Exp((-Mathf.Pow(instance - mean, 2))/(2*Mathf.Pow(std_deviation, 2)));
			
			/*if(alpha*beta > .3f)
				Debug.Log("close " + instance + " " + mean);
			*/
			
			float prob = alpha*Mathf.Exp(beta);
			
			if(prob < .1f)
				return .1f;
			return prob;
		}
		public void AddData(float val){
			data.Add(val);
			CalculateMean();
			CalculateStdDev();
		}
	};
	
	
	public class BayesianClassifier{
		public string pose_name;
		List <feature> features = new List<feature>();
		
		
		public BayesianClassifier(){}
		public BayesianClassifier(string n, List<List<float>> all_data){
			Debug.Log("creating new classifier : " + n);
			pose_name =  n;
			for(int i = 0; i < all_data.Count; i++){
				features.Add(new feature(all_data[i]));
			}
		}
		public float GetProbablity(List<float> instance){
			float prob = 1;
			string line = pose_name;
			for(int i = 0; i < features.Count && i < instance.Count; i++){
				feature feat = features[i];
				float new_prob = feat.CalculateProbability(instance[i]);
				
				Debug.Log(new_prob);
				
				line += " " + new_prob;
				
				if(new_prob < .1f)
					new_prob = .1f;
				prob*=new_prob;
			}
			
			line += ": " + prob;
			Debug.Log(line);
			return prob;
		}
		
		public void AddData(List<float> new_data){
			for(int i = 0; i < features.Count; i++)
				features[i].AddData(new_data[i]);
			
		}
		
	};
	
	
	//joints we're keeping track of
	public GameObject [] left_arm;
	public GameObject [] right_arm;
	public GameObject [] left_leg;
	public GameObject [] right_leg;
	//can get shoulders and hips from this info
	GameObject [] torso = new GameObject [4];
	
	//we're going to save a queue of positions for now and analyze recent strings
	//for movements we are checking for
	/*private List <ArmState> arm_states_left = new List<ArmState>();
	private List <ArmState> arm_states_right = new List<ArmState>();
	*/
	
	//keep it all in a list of type State ordered depending on the mask
	private List <State> states = new List<State>();
	private int tracked_states = 5;//default - arms, legs, and torso
	//current states
	private List <State> new_states = new List<State>();
	
	List <List<State>> recorded_states = new List<List<State>>();
	
	List <BayesianClassifier> classifiers = new List<BayesianClassifier>();
	
	
	//store detected motions here
	public List <string> motions = new List<string>();
	public bool updated = false;
	
	void RecordValues(){
		TextWriter tw = new StreamWriter("Assets\\bayes_data.txt", true);
		
		foreach(List <State> shots in recorded_states){
			string line = train_name;
			
			foreach(State shot in shots){
				List <float> angles = shot.GenerateAngles();
				foreach(float angle in angles)
					line += " " + angle;
			}
						
			//Debug.Log(line);
			tw.WriteLine(line);
		}
		
		tw.Close();
	}
	
	//best data
	int identification = 0;
	float best_prob = 0f;
	List <float> best_data = new List<float>();
	bool best_changed = false;
	string best_name = "";
	
	void OnGUI(){
		//training control block
		if(train){
			train_name = GUI.TextField(new Rect(25, 100, 100, 25), train_name);
			if(recording){
				//if(GUI.Button(new Rect(Screen.width - 150, 50, 100, 50), "Rec")){
					//only add in tracked states TODO- work with mask
					List <State> new_shots = new List<State>();
					for(int x = 0; x < tracked_states; x++){
						new_shots.Add(new_states[x]);
						Debug.Log(new_states[x].joints[0]);
					}
					
					recorded_states.Add(new_shots);
					
				//}
				
				if(GUI.Button(new Rect(25, 25, 100, 50), "Stop and Save")){
					RecordValues();
					recorded_states.Clear();
					recording = false;
				}
				if(GUI.Button(new Rect(150, 25, 100, 50), "Stop (Don't Save)")){
					recorded_states.Clear();
					recording = false;	
				}
			}
			else{
				if(GUI.Button(new Rect(25, 25, 100, 50), "Record"))
					recording = true;
			}
			if(GUI.Button(new Rect(275, 25, 100, 50), "Stop Training"))
				train = false;
		}
		//testing control block
		else{
			if(GUI.Button(new Rect(25, 25, 100, 50), "Train"))
				train = true;
			if(GUI.Button(new Rect(150, 25, 100, 50), "Update Library"))
				ReadFile();
			
			//show our best picture if we have one
			//GUI.DrawTexture(new Rect(Screen.width - 200, Screen.height - 200, 200, 200), best_pose);
			
			//new states should be initialized by this point...
			List <float> angles = new List<float>();
			for(int i = 0; i < tracked_states; i++)
				angles.AddRange(new_states[i].GenerateAngles());
			
			string line = "";
			foreach(float angle in angles)
				line += angle + " ";
			GUI.Label(new Rect(50, Screen.height - 100, 400, 200), line, skin.label);
			/*for(int i = 0; i < classifiers.Count; i++){
				if(classifiers[i].GetProbablity(angles) > best_prob){
					identification	= i;
					
					
					//GetComponent<KinectPointController>().
					
					best_data = angles;
					best_changed = true;
					best_prob = classifiers[i].GetProbablity(angles);
					best_name = classifiers[i].pose_name;
				}
				
				
				string l = classifiers[i].pose_name + " "  + classifiers[i].GetProbablity(angles);
				GUI.Label(new Rect(50, Screen.height - 150 - 50*i, 200, 50), l, skin.label);	
			}*/
		}
		
		if(best_changed){
			Debug.Log("best changed");
			GUI.Label(new Rect(50, 200, 500, 50), "Best " + best_name + " is " + best_prob + ". Accept?", skin.label);
			if(GUI.Button(new Rect(50, 250, 100, 25), "Accept")){
				classifiers[identification].AddData(best_data);
				best_changed = false;
				best_prob = 0f;
				RecordValues();
				best_img.best_found = false;
			}
			if(GUI.Button(new Rect(200, 250, 100, 25), "Reject")){
				best_changed = false;
				best_prob = 0f;
				best_img.best_found = false;	
			}			
		}
		
	}
	
	
	
	void ReadFile(){
		TextReader tr = new StreamReader("Assets\\bayes_data.txt");
		classifiers.Clear();
		
		string line = tr.ReadLine();
		
		while(line != null){
			//need to separate each feature into a different list, each different name will be associated with different instance of classifier
			string[] sline = line.Split(' ');
			string pose_name = sline[0];
			List<List<float>> data = new List<List<float>>();
			for(int i = 1; i < sline.GetLength(0); i++)
				data.Add(new List<float>());
			
			while(line!= null && pose_name == sline[0]){
				for(int i = 1; i < sline.GetLength(0); i++)
					data[i-1].Add(float.Parse(sline[i]));				
				
				line = tr.ReadLine();
				if(line != null)
					sline = line.Split(' ');
			}
			
			//save current data to new classifier
			classifiers.Add(new BayesianClassifier(pose_name, data));
		}
		
		tr.Close();
	}	
	
	//testing
	/*Color32[] colors = new Color32[0];
	Texture2D best_pose; */
	
	
	// Use this for initialization
	void Start () {
		KinectPrefab = GameObject.Find("Kinect_Prefab");
		kinect = KinectPrefab.GetComponent<DeviceOrEmulator>().getKinect();
		
		//init our torso
		torso[0] = left_arm[0];
		torso[1] = right_arm[0];
		torso[2] = left_leg[0];
		torso[3] = right_leg[0];
		
		ReadFile();
		
	}
	
	// Update is called once per frame
	void Update () {
		updated = false;
		
		DetectMovement();
		//check for a new arm state
		/*if(recording){ THIS IS DONE ONGUI
			//do nothing for now, still debugging this feature
			recorded_states.Add(new_states);
		}*/
		if(!train){
			InterpretGestures();			
		}
	}
	
	
	void Decay(){
		for(int i = 0; i < states.Count; i++){
			if(states[i].timestamp > Time.time - 10f)
				return;
			else{
				states.RemoveAt(i);
				i--;
			}
		}
	}
	
	
	void DetectMovement(){
		//go through and check for movement - TODO - check for mask
		
		
		//pick up our new states
		new_states.Clear();
		new_states.Add(new ArmState(left_arm));
		new_states.Add(new ArmState(right_arm));
		new_states.Add(new LegState(left_leg));
		new_states.Add(new LegState(right_leg));
		new_states.Add(new TorsoState(torso));
		/*
		if(states.Count > 0){
			bool moved = false;
			for(int i = 0; i < tracked_states; i++){
				int index = states.Count - tracked_states + i;
				if(states[index]!=new_states[i])
					moved=true;
			}
			//if(moved)
				states.AddRange(new_states);				
		}
		else{*/
			states.AddRange(new_states);
		//}
		
		
		/*
		if(arm_states.Count > 0){
		//check to see if any of the positions changed before we record anything
			
			
			
			if(arm_states[arm_states.Count-1].CompareStates(left_arm, right_arm)){
				arm_states.Add(new ArmsSnapshot(left_arm, right_arm));
				updated = true;	
			}
		}*/
	}
	
	
	void Train(){
		//TODO - implement masks

		
	}
	
	void InterpretGestures(){
		//note: for the rush segments, we care more about current state
		//than changes in state, but other games will be slightly different
		//Debug.Log(arm_states.Count-1);
		
		
		
		if(states.Count == 0){
			Debug.Log("states count is 0");
			return;
			
		}
		
		//our current states are in new_states
		
		//order is hand, elbow, shoulder
		//we're going to generate angles on elbow joint and shoulder joint
		
			List <float> angles = new List<float>();
			
			for(int i = 0; i < tracked_states; i++)
				angles.AddRange(new_states[i].GenerateAngles());

			//List <float> angles = cur_state.GenerateAngles();
			foreach(BayesianClassifier classifier in classifiers){
				Debug.Log(classifier.GetProbablity(angles));
				if(best_prob < classifier.GetProbablity(angles)){
				
					best_changed = true;
					best_img.TakeSnapshot();
					best_img.best_found = true;
					
					
					best_prob = classifier.GetProbablity(angles);
					best_name = classifier.pose_name;
					best_data = angles;
					Debug.Log(classifier.pose_name + " " + best_prob);
				}
				
				//Debug.Log(classifier.pose_name + " " + classifier.GetProbablity(angles));
				if(classifier.GetProbablity(angles) > .001)
					Debug.Log("POSE DETECTED: " + classifier.pose_name);			
			}
		
		

		
	}
}
