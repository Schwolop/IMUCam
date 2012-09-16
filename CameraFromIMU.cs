using UnityEngine;
using System.Collections;
using System;
using Tinkerforge;

public class CameraFromIMU : MonoBehaviour {
	
	private static string _host = "localhost";
	private static int _port = 4223;
	private static string _uid = "a4JritAp6Go"; // Change to your UID

	private static BrickIMU _imu;
	private static IPConnection _ipcon;
	
	private static Quaternion _q = new Quaternion(0.0f,0.0f,0.0f,1.0f);
	private static Quaternion _qOrigin = new Quaternion(0.0f,0.0f,0.0f,1.0f);
	private static Quaternion _qPrevious = new Quaternion(0.0f,0.0f,0.0f,1.0f);
	
	public float _speed = 100.0f;
	
	// Use this for initialization
	void Start () {
		_ipcon = new IPConnection(_host, _port); // Create connection to brickd
		_imu = new BrickIMU(_uid); // Create device object
		_ipcon.AddDevice(_imu); // Add device to IP connection
		// Don't use device before it is added to a connection

		// Set period for quaternion callback to 10ms
		_imu.SetQuaternionPeriod(10);

		// Register quaternion callback to QuaternionCB
		_imu.RegisterCallback(new BrickIMU.Quaternion(QuaternionCB));
		
		// Set rotational origin to however the IMU is pointing at the start.
		float x,y,z,w;
		_imu.GetQuaternion(out x, out y, out z, out w);
		_qOrigin.w = w;
		_qOrigin.x = x;
		_qOrigin.y = y;
		_qOrigin.z = z;		
		
		_imu.LedsOff(); // Turn LEDs off.
		_imu.SetConvergenceSpeed( 5 ); // 5ms convergence.
	}
	
	// Quaternion callback
	static void QuaternionCB(float x, float y, float z, float w)
	{
		//Debug.Log("x: " + x + "\ny: " + y + "\nz: " + z + "\nw: " + w + "\n");
		_qPrevious = _q; // Save q to qPrevious
		_q.w = w;
		_q.x = x;
		_q.y = y;
		_q.z = z;
	}
	
	void OnApplicationExit() {
		_ipcon.Destroy();
	}
	
	void OnGUI() {
		GUI.Box( new Rect( 5, 5, 200, 24*2 ),
			"Quaternion: " + _q.ToString() + "\n" + 
		    "Camera: " + transform.rotation.ToString() );
	}
	
	// Update is called once per frame
	void Update () {
		// Conjugate of current quaternion:
		float x,y,z,w;
		x = -_q.x;
		y = -_q.y;
		z = -_q.z;
		w = _q.w;
		
		// Multiply with origin quaternion:
		float xn,yn,zn,wn;
		wn = w * _qOrigin.w - x * _qOrigin.x - y * _qOrigin.y - z * _qOrigin.z;
		xn = w * _qOrigin.x + x * _qOrigin.w + y * _qOrigin.z - z * _qOrigin.y;
		yn = w * _qOrigin.y - x * _qOrigin.z + y * _qOrigin.w + z * _qOrigin.x;
		zn = w * _qOrigin.z + x * _qOrigin.y - y * _qOrigin.x + z * _qOrigin.w;
		
		// Rotate the camera relative to the world frame axes, by the roll pitch yaw of the IMU.
		Quaternion q = new Quaternion( xn, yn, zn, wn );
		transform.rotation = Quaternion.identity;
		transform.Rotate( Vector3.right * q.eulerAngles.x, Space.World );
		transform.Rotate( Vector3.forward * q.eulerAngles.y, Space.World );
		transform.Rotate( Vector3.up * q.eulerAngles.z, Space.World );
		
		// Fly forwards.
		transform.Translate(Vector3.forward * Time.deltaTime * _speed);
	}
}