using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class bayes_test : MonoBehaviour {
	
	public class feature{
		public float mean;
		public float std_deviation;
		List <float> d = new List<float>();
		
		public feature(){}
		public feature(List <float> data){
			d = data;
			CalculateMean(data);
			CalculateStdDev(data);
			Debug.Log("Feature - Mean: " + mean + " Var: " + Mathf.Pow(std_deviation,2));
		}
		void CalculateMean(List <float> data){
			float sum = 0;
			foreach(float d in data)
				sum += d;
			mean = sum/(float)data.Count;
		}
		
		void CalculateStdDev(List <float> data){
			float sum_sqr_diff = 0;
			foreach(float d in data)
				sum_sqr_diff += Mathf.Pow(mean - d, 2);
			Debug.Log(sum_sqr_diff/data.Count);
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
			Debug.Log(prob);
			
			if(alpha*Mathf.Exp(beta) < .1f)
				return .1f;
			return alpha*Mathf.Exp(beta);
		}
		
		public void AddData(float data){
			d.Add(data);
			CalculateMean(d);
			CalculateStdDev(d);
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
	
	List <BayesianClassifier> classifiers = new List<BayesianClassifier>();
	
	
	// Use this for initialization
	void Start () {
		//testing!
		List <List<float>> male_data = new List<List<float>>();
		List <List<float>> female_data = new List<List<float>>();
		
		for(int i = 0; i < 3; i++){
			male_data.Add(new List<float>());
			female_data.Add(new List<float>());
			fields[i] = "";
		}
		male_data[0].Add(6); male_data[0].Add(5.92f); male_data[0].Add(5.58f); male_data[0].Add(5.92f);
		male_data[1].Add(180); male_data[1].Add(190); male_data[1].Add(170); male_data[1].Add(165);
		male_data[2].Add(12); male_data[2].Add(11); male_data[2].Add(12); male_data[2].Add(10);
		female_data[0].Add(5); female_data[0].Add(5.5f); female_data[0].Add(5.42f); female_data[0].Add(5.75f);
		female_data[1].Add(100); female_data[1].Add(150); female_data[1].Add(130); female_data[1].Add(150);
		female_data[2].Add(6); female_data[2].Add(8); female_data[2].Add(7); female_data[2].Add(9);
		
		classifiers.Add(new BayesianClassifier("male", male_data));
		classifiers.Add(new BayesianClassifier("female", female_data));
		
		
	
	}
	
	string identification = "none";
	float male_prob = 0;
	float female_prob = 0;
	string [] fields = new string[3];
	float best = 0;
	List <float> best_data = new List<float>();
	bool best_changed = false;
	
	void OnGUI(){
		
		fields[0] = GUI.TextField(new Rect(50, 100, 100, 25), fields[0]);
		GUI.Label(new Rect(0, 100, 50, 25), "Height");
		fields[1] = GUI.TextField(new Rect(50, 150, 100, 25), fields[1]);
		GUI.Label(new Rect(0, 150, 50, 25), "Weight");
		fields[2] = GUI.TextField(new Rect(50, 200, 100, 25), fields[2]);
		GUI.Label(new Rect(0, 200, 50, 25), "Shoe");
		
		if(GUI.Button(new Rect(50, 50, 100, 25), "classify")){
			List <float> data = new List<float>();
			for(int i = 0; i < 3; i++)
				data.Add(float.Parse(fields[i]));
			foreach(BayesianClassifier c in classifiers){
				float prob = c.GetProbablity(data);
				if(prob > best){
					best = prob;
					identification = c.pose_name;
					best_changed = true;
					for(int i = 0; i < 3; i++)
						best_data.Add(float.Parse(fields[i]));					
				}
				if(c.pose_name == "male")
					male_prob = prob;
				else
					female_prob = prob;
				
			}
			
		}
		
		GUI.Label(new Rect(400, 50, 100, 50), "Male: " +male_prob);
		GUI.Label(new Rect(400, 100, 100, 50), "Female: " +female_prob);
		
		
		if(best_changed){
			GUI.Label(new Rect(50, Screen.height - 100, 500, 50), "Best: " + identification +
				" " + best_data[0] + " " + best_data[1] + " " + best_data[2] + ". Accept?");
			if(GUI.Button(new Rect(50, Screen.height - 50, 100, 50), "Accept")){
				if(identification == "male")
					classifiers[0].AddData(best_data);
				else
					classifiers[1].AddData(best_data);
				best_changed = false;
				best = 0;
			}
			if(GUI.Button(new Rect(175, Screen.height - 50, 100, 50), "Reject"))
				best_changed = false;
					
		}
		
		
	}
	
	// Update is called once per frame
	void Update () {
	
	}
}
