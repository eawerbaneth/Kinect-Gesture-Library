using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using Kinect;

public class KinectWrapper : MonoBehaviour, KinectInterface {
	
	private static KinectInterface instance;
	public static KinectInterface Instance{
		get{
			if(instance == null)
				throw new Exception("No active instnace of KinectWrapper component");
			return instance;
		}
		private set{
			instance = value;	
		}
		
	}
	
	public float sensorHeight;
	public Vector3 kinectCenter;
	public Vector4 lookAt;
	
	public float smoothing = 0.5f;
	public float correction = 0.5f;
	public float prediction = 0.5f;
	public float jitterRadius = 0.05f;
	public float maxDeviationRadius = 0.04f;
	
	private bool updatedSkeleton = false;
	private bool newSkeleton = false;
	private NuiSkeletonFrame skeletonFrame = new NuiSkeletonFrame(){ SkeletonData = new NuiSkeletonData[6] };
	
	private bool updatedColor = false;
	private bool newColor = false;
	private Color32[] colorImage;
	
	private bool updatedDepth = false;
	private bool newDepth = false;
	private short[] depthPlayerData;
	
	//image stream handles for the kinect
	private IntPtr colorStreamHandle;
	private IntPtr depthStreamHandle;
	[HideInInspector]
	private NuiTransformSmoothParameters smoothParameters = new NuiTransformSmoothParameters();
	
	float KinectInterface.getSensorHeight(){
		return sensorHeight;
	}
	Vector3 KinectInterface.getKinectCenter(){
		return kinectCenter;
	}
	Vector4 KinectInterface.getLookAt(){
		return lookAt;
	}
	
	void Awake(){
		if(KinectWrapper.instance != null){
			Debug.Log("There should only be one active instance of KinectWrapper component");
			throw new Exception("There should only be one active instance of KinectWrapper component");
		}
		try{
			int hr = NativeMethods.NuiInitialize(NuiInitializeFlags.UsesDepthAndPlayerIndex | NuiInitializeFlags.UsesSkeleton | NuiInitializeFlags.UsesColor);
			if(hr != 0)
				throw new Exception("Cannot initialize Skeleton Data.");
			
			depthStreamHandle = IntPtr.Zero;
			hr = NativeMethods.NuiImageStreamOpen(NuiImageType.DepthAndPlayerIndex, NuiImageResolution.resolution320x240, 0, 2, IntPtr.Zero, ref depthStreamHandle);
			Debug.Log(depthStreamHandle);
			if(hr != 0)
				throw new Exception("Cannot open depth stream.");
			
			colorStreamHandle = IntPtr.Zero;
			hr = NativeMethods.NuiImageStreamOpen(NuiImageType.Color, NuiImageResolution.resolution640x480, 0, 2, IntPtr.Zero, ref colorStreamHandle);
			Debug.Log(colorStreamHandle);
			if(hr != 0)
				throw new Exception("Cannot open color stream.");
			colorImage = new Color32[640*480];
			
			double theta = Math.Atan((lookAt.y + kinectCenter.y - sensorHeight) / (lookAt.z + kinectCenter.z));
			long kinectAngle = (long)(theta * (180 / Math.PI));
			NativeMethods.NuiCameraSetAngle(kinectAngle);
			
			DontDestroyOnLoad(gameObject);
			KinectWrapper.Instance = this;
		}
		catch(Exception e){
			Debug.Log(e.Message);
		}
		
		
	}
	
	void LateUpdate(){
		updatedSkeleton = false;
		newSkeleton = false;
		updatedColor = false;
		newColor = false;
		updatedDepth = false;
		newDepth = false;
		
	}
	
	//poll the kinect for updated skeleton data and return true if there is new data.
	//subsequent calls do nothing and return the same value
	bool KinectInterface.pollSkeleton(){
		if(!updatedSkeleton){
			updatedSkeleton = true;
			int hr = NativeMethods.NuiSkeletonGetNextFrame(100, ref skeletonFrame);
			if(hr == 0)
				newSkeleton = true;
			smoothParameters.fSmoothing = smoothing;
			smoothParameters.fCorrection = correction;
			smoothParameters.fJitterRadius = jitterRadius;
			smoothParameters.fMaxDeviationRadius = maxDeviationRadius;
			smoothParameters.fPrediction = prediction;
			hr = NativeMethods.NuiTransformSmooth(ref skeletonFrame, ref smoothParameters);
		}
		return newSkeleton;
		
	}
	
	NuiSkeletonFrame KinectInterface.getSkeleton(){
		return skeletonFrame;
	}
	
	//poll kinect for updated color data
	bool KinectInterface.pollColor(){
		if(!updatedColor){
			updatedColor = true;
			IntPtr imageFramePtr = IntPtr.Zero;
			int hr = NativeMethods.NuiImageStreamGetNextFrame(colorStreamHandle, 100, ref imageFramePtr);
			if(hr == 0){
				newColor = true;
				NuiImageFrame imageFrame = (NuiImageFrame)Marshal.PtrToStructure(imageFramePtr, typeof(NuiImageFrame));
				NuiImageBuffer imageBuf = (NuiImageBuffer)Marshal.PtrToStructure(imageFrame.pFrameTexture, typeof(NuiImageBuffer));
				colorImage = extractColorImage(imageBuf);
				
				hr = NativeMethods.NuiImageStreamReleaseFrame(colorStreamHandle, imageFramePtr);
			}	
		}
		return newColor;
		
	}
	
	Color32[] KinectInterface.getColor(){
		return colorImage;	
	}
	
	//poll the kinect for updated depth (and player) data
	bool KinectInterface.pollDepth(){
		if(!updatedDepth){
			updatedDepth = true;
			IntPtr imageFramePtr = IntPtr.Zero;
			int hr = NativeMethods.NuiImageStreamGetNextFrame(depthStreamHandle, 100, ref imageFramePtr);
			if(hr == 0){
				newDepth = true;
				NuiImageFrame imageFrame = (NuiImageFrame)Marshal.PtrToStructure(imageFramePtr, typeof(NuiImageFrame));
				
				NuiImageBuffer imageBuf = (NuiImageBuffer)Marshal.PtrToStructure(imageFrame.pFrameTexture, typeof(NuiImageBuffer));
				depthPlayerData = extractDepthImage(imageBuf);
				
				hr = NativeMethods.NuiImageStreamReleaseFrame(depthStreamHandle, imageFramePtr);
				
			}
		}
		return newDepth;
		
	}
	
	short[] KinectInterface.getDepth(){
		return depthPlayerData;	
	}
	
	private Color32[] extractColorImage(NuiImageBuffer buf){
		int totalPixels = buf.m_Width*buf.m_Height;
		Color32[] colorBuf = colorImage;
		ColorBuffer cb = (ColorBuffer)Marshal.PtrToStructure(buf.m_pBuffer, typeof(ColorBuffer));
		for(int pix = 0; pix < totalPixels; pix++){
			colorBuf[pix].r = cb.pixels[pix].r;
			colorBuf[pix].g = cb.pixels[pix].g;
			colorBuf[pix].b = cb.pixels[pix].b;
		}
		
		return colorBuf;
	}
	
	private short[] extractDepthImage(NuiImageBuffer buf){
		DepthBuffer db = (DepthBuffer)Marshal.PtrToStructure(buf.m_pBuffer, typeof(DepthBuffer));
		return db.pixels;
	}
	
	void OnApplicationQuit(){
		NativeMethods.NuiShutdown();	
	}
	
	// Use this for initialization
	void Start () {
	
	}                                                                                          
	
	// Update is called once per frame
	void Update () {
	
	}
}
