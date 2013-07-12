using UnityEngine;
using System.Collections;
using System;
using Tinkerforge;

public class CameraFromIMU : MonoBehaviour {
	
	private static string _host = "localhost";
	private static int _port = 4223;
	private static string _uid = "6wUjm3"; // Change to your UID

	private static BrickIMU _imu;
	private static IPConnection _ipcon;
	
	private static Quaternion _q = new Quaternion(0.0f,0.0f,0.0f,1.0f);
	private static Quaternion _qOrigin = new Quaternion(0.0f,0.0f,0.0f,1.0f);
	private static Quaternion _qPrevious = new Quaternion(0.0f,0.0f,0.0f,1.0f);
	
	public float _speed = 100.0f; // Forwards speed.
	public float _maxStrafeVelocity = 100.0f; // Max speed left and right.
	public float _strafeKeyProportion = 0.0f; // Current left/right key press proportion (-ve = left, +ve = right).
	public float _updownKeyProportion = 0.0f; // Current up/down key press proportion.
	public float _rotateLeftRightKeyAngle = 0.0f; // Current angular rotation around world's up axis caused by Q/E keys.
	public float _maxLiftVelocity = 25.0f; // Velocity upwards when up is pressed (eventually, when left and right are down).
	public float _maxFallVelocity = 50.0f; // Velocity downwards when pressing downwards.
	public float _yawInfluenceFromRoll = 0.01f;
	
	// Use this for initialization
	void Start () {
		_ipcon = new IPConnection(); // Create connection to brickd
		_imu = new BrickIMU(_uid, _ipcon); // Create device object
		_ipcon.Connect(_host,_port); // Add device to IP connection
		// Don't use device before it is added to a connection

		// Set period for quaternion callback to 10ms
		_imu.SetQuaternionPeriod(10);

		// Register quaternion callback to QuaternionCB
		_imu.Quaternion += QuaternionCB;
		
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
	static void QuaternionCB(BrickIMU sender, float x, float y, float z, float w)
	{
		//Debug.Log("x: " + x + "\ny: " + y + "\nz: " + z + "\nw: " + w + "\n");
		_qPrevious = _q; // Save q to qPrevious
		_q.w = w;
		_q.x = x;
		_q.y = y;
		_q.z = z;
	}
	
	void OnApplicationExit() {
		_ipcon.Disconnect();
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
		
		// Strafe according to left/right keys.
		//_strafeKeyProportion = Input.GetAxis("Horizontal");
		if (Input.GetKey (KeyCode.A))
			_strafeKeyProportion = -1.0f;
		else if (Input.GetKey (KeyCode.D))
			_strafeKeyProportion = 1.0f;
        else
			_strafeKeyProportion = 0.0f;
		transform.Translate( Vector3.right * _strafeKeyProportion * _maxStrafeVelocity * Time.deltaTime );
		
		// Rise/fall (w.r.t. to the *world*'s z-axis) according to up/down keys.
		//_updownKeyProportion = Input.GetAxis("Vertical");
		if (Input.GetKey (KeyCode.W))
			_updownKeyProportion = 1.0f;
		else if (Input.GetKey (KeyCode.S))
			_updownKeyProportion = -1.0f;
        else
			_updownKeyProportion = 0.0f;
		if( _updownKeyProportion > 0.0f )
		{
			transform.Translate( Vector3.up * _updownKeyProportion * _maxLiftVelocity * Time.deltaTime, Space.World );
		}
		else
		{
			transform.Translate( Vector3.up * _updownKeyProportion * _maxFallVelocity * Time.deltaTime, Space.World );
		}
		
		// Rotate around up world's up axis by Q/E keys.
		if (Input.GetKey (KeyCode.Q))
			_rotateLeftRightKeyAngle = (_rotateLeftRightKeyAngle-1.0f) % 360.0f;
		else if (Input.GetKey (KeyCode.E))
			_rotateLeftRightKeyAngle = (_rotateLeftRightKeyAngle+1.0f) % 360.0f;
		
		// But also rotate around world axis by an amount proportional to roll.
		Debug.Log( q.eulerAngles.y );
		float roll = q.eulerAngles.y;
		if( roll > 180.0f ){
			roll = roll - 360.0f;}
		if(roll > 90.0f){
			roll = 180.0f - roll;}
		else if(roll < -90.0f){
			roll = -180.0f - roll;}
		_rotateLeftRightKeyAngle = (_rotateLeftRightKeyAngle - (roll * _yawInfluenceFromRoll)) % 360.0f;
		
		// Do that rotation.
		transform.Rotate( Vector3.up * _rotateLeftRightKeyAngle, Space.World );
			
		// Fly forwards.
		transform.Translate(Vector3.forward * Time.deltaTime * _speed);
	}
}