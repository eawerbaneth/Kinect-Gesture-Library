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
	
	float best = 0f;
	
	GameObject KinectPrefab;
	KinectInterface kinect;
	public DeviceOrEmulator devOrEmu;
	
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
	
	//struct to help out with image recording
	public DisplayColor best_img;
	
	private Color32[] mipmapImg(Color32[] src, int width, int height)
	{
		int newWidth = width / 2;
		int newHeight = height / 2;
		Color32[] dst = new Color32[newWidth * newHeight];
		for(int yy = 0; yy < newHeight; yy++)
		{
			for(int xx = 0; xx < newWidth; xx++)
			{
				int TLidx = (xx * 2) + yy * 2 * width;
				int TRidx = (xx * 2 + 1) + yy * width * 2;
				int BLidx = (xx * 2) + (yy * 2 + 1) * width;
				int BRidx = (xx * 2 + 1) + (yy * 2 + 1) * width;
				dst[xx + yy * newWidth] = Color32.Lerp(Color32.Lerp(src[BLidx],src[BRidx],.5F),
				                                       Color32.Lerp(src[TLidx],src[TRidx],.5F),.5F);
			}
		}
		return dst;
	}
	
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
	};
	
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
			float beta = -((Mathf.Pow(instance - mean, 2)/(2*Mathf.Pow(std_deviation, 2))));
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
			pose_name = n;
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
				
				line += " " + new_prob;
					
//					features[i].CalculateProbablity(instance[i]);
				if(new_prob < .1f)
					new_prob = .1f;
				prob*=new_prob;
			}
			
			line += ": " + prob;
			//Debug.Log(line);
			return prob;
		}
		
		public void AddData(List<float> new_data){
			for(int i = 0; i < features.Count; i++)
				features[i].AddData(new_data[i]);
			
		}
		
	};
	
	
	//we're going to keep track of the arm joints to interpret movement
	//and relay that the DropletMovement
	public GameObject [] left_arm;
	public GameObject [] right_arm;
	
	//we're going to save a queue of positions for now and analyze recent strings
	//for movements we are checking for
	private List <ArmsSnapshot> arm_states = new List<ArmsSnapshot>();
	List <ArmsSnapshot> recorded_states = new List<ArmsSnapshot>();
	List <BayesianClassifier> classifiers = new List<BayesianClassifier>();
	
	
	//store detected motions here
	public List <string> motions = new List<string>();
	public bool updated = false;
	
	void RecordValues(){
		TextWriter tw = new StreamWriter("Assets\\bayes_data.txt", true);
		
		foreach(ArmsSnapshot shot in recorded_states){
			List<float> angles = shot.GenerateAngles();
			string line = train_name;
			foreach(float angle in angles)
				line += " " + angle;
			
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
		if(train){
			train_name = GUI.TextField(new Rect(25, 100, 100, 25), train_name, skin.label);
			if(recording){
				if(GUI.Button(new Rect(Screen.width - 150, 50, 100, 50), "Rec"))
					recorded_states.Add(new ArmsSnapshot(left_arm, right_arm));
				
				if(GUI.Button(new Rect(25, 25, 100, 50), "Stop and Save")){
					RecordValues();
					recording = false;
				}
				if(GUI.Button(new Rect(150, 25, 100, 50), "Stop (Don't Save)"))
					recording = false;	
			}
			else{
				if(GUI.Button(new Rect(25, 25, 100, 50), "Record"))
					recording = true;
			}
			if(GUI.Button(new Rect(275, 25, 100, 50), "Stop Training"))
				train = false;
		}
		else{
			if(GUI.Button(new Rect(25, 25, 100, 50), "Train"))
				train = true;
			if(GUI.Button(new Rect(150, 25, 100, 50), "Update Library"))
				ReadFile();
			
			//show our best picture if we have one
			//GUI.DrawTexture(new Rect(Screen.width - 200, Screen.height - 200, 200, 200), best_pose);
			
			
			ArmsSnapshot cur_state = arm_states[arm_states.Count-1];
			List <float> angles = cur_state.GenerateAngles();
			string line = "";
			foreach(float angle in angles)
				line += angle + " ";
			GUI.Label(new Rect(50, Screen.height - 100, 200, 50), line, skin.label);
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
			GUI.Label(new Rect(50, 200, 500, 50), "Best " + best_name + " is " + best + ". Accept?", skin.label);
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
		
		//save our first positions
		arm_states.Add(new ArmsSnapshot(left_arm, right_arm));
		//best_pose = new Texture2D(640, 480, TextureFormat.ARGB32, false);
		
	}
	
	// Update is called once per frame
	void Update () {
		updated = false;
		
		//check for a new arm state
		if(recording){
			;//do nothing for now, still debugging this feature
			//recorded_states.Add(new ArmsSnapshot(left_arm, right_arm));
		}
		if(!train){
			DetectMovement();
			InterpretGestures();			
		}		
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
	
	void InterpretGestures(){
		//note: for the rush segments, we care more about current state
		//than changes in state, but other games will be slightly different
		//Debug.Log(arm_states.Count-1);
		
		
		if(arm_states.Count == 0)
			return;
		
		ArmsSnapshot cur_state = arm_states[arm_states.Count-1];
		/*float left_out;
		float right_out;
		float arm_length = cur_state.GetLength();*/
		
		//order is hand, elbow, shoulder
		//we're going to generate angles on elbow joint and shoulder joint
		if(!train){
			List <float> angles = cur_state.GenerateAngles();
			foreach(BayesianClassifier classifier in classifiers){
				if(best < classifier.GetProbablity(angles)){
					best_changed = true;
					best_img.TakeSnapshot();
					best_img.best_found = true;
					/*if(kinect.pollColor()){
						//colors = kinect.getColor();
						best_pose.SetPixels32(mipmapImg(kinect.getColor(),640,480));
						best_pose.Apply(false);
					}*/
					
					
					best = classifier.GetProbablity(angles);
					best_name = classifier.pose_name;
					best_data = angles;
					Debug.Log(classifier.pose_name + " " + best);
				}
				
				//Debug.Log(classifier.pose_name + " " + classifier.GetProbablity(angles));
				if(classifier.GetProbablity(angles) > .001)
					Debug.Log("POSE DETECTED: " + classifier.pose_name);			
			}
		}
		
		

		
	}
}
