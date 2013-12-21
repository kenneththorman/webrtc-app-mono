using System;
using System.Collections.Generic;
/*
 *  Copyright (c) 2012 The WebRTC project authors. All Rights Reserved.
 *
 *  Use of this source code is governed by a BSD-style license
 *  that can be found in the LICENSE file in the root of the source
 *  tree. An additional intellectual property rights grant can be found
 *  in the file PATENTS.  All contributing project authors may
 *  be found in the AUTHORS file in the root of the source tree.
 */
using WebRtc;

namespace org.webrtc.videoengineapp
{

	using AlertDialog = android.app.AlertDialog;
	using TabActivity = android.app.TabActivity;
	using BroadcastReceiver = android.content.BroadcastReceiver;
	using Context = android.content.Context;
	using DialogInterface = android.content.DialogInterface;
	using Intent = android.content.Intent;
	using IntentFilter = android.content.IntentFilter;
	using ActivityInfo = android.content.pm.ActivityInfo;
	using Configuration = android.content.res.Configuration;
	using Canvas = android.graphics.Canvas;
	using Paint = android.graphics.Paint;
	using PixelFormat = android.graphics.PixelFormat;
	using Camera = android.hardware.Camera;
	using CameraInfo = android.hardware.Camera.CameraInfo;
	using SensorManager = android.hardware.SensorManager;
	using AudioManager = android.media.AudioManager;
	using MediaPlayer = android.media.MediaPlayer;
	using Uri = android.net.Uri;
	using Bundle = android.os.Bundle;
	using Environment = android.os.Environment;
	using Handler = android.os.Handler;
	using Log = android.util.Log;
	using Display = android.view.Display;
	using Gravity = android.view.Gravity;
	using KeyEvent = android.view.KeyEvent;
	using LayoutInflater = android.view.LayoutInflater;
	using OrientationEventListener = android.view.OrientationEventListener;
	using Surface = android.view.Surface;
	using SurfaceView = android.view.SurfaceView;
	using View = android.view.View;
	using ViewGroup = android.view.ViewGroup;
	using Window = android.view.Window;
	using WindowManager = android.view.WindowManager;
	using AdapterView = android.widget.AdapterView;
	using OnItemSelectedListener = android.widget.AdapterView.OnItemSelectedListener;
	using ArrayAdapter = android.widget.ArrayAdapter;
	using Button = android.widget.Button;
	using CheckBox = android.widget.CheckBox;
	using EditText = android.widget.EditText;
	using LinearLayout = android.widget.LinearLayout;
	using RadioGroup = android.widget.RadioGroup;
	using Spinner = android.widget.Spinner;
	using TabHost = android.widget.TabHost;
	using TabSpec = android.widget.TabHost.TabSpec;
	using TextView = android.widget.TextView;

	using ViERenderer = org.webrtc.videoengine.ViERenderer;


	public class WebRTCDemo : TabActivity, IViEAndroidCallback, View.OnClickListener, AdapterView.OnItemSelectedListener
	{
		private ViEAndroidJavaAPI vieAndroidAPI = null;

		// remote renderer
		private SurfaceView remoteSurfaceView = null;

		// local renderer and camera
		private SurfaceView svLocal = null;

		// channel number
		private int channel = -1;
		private int cameraId;
		private int voiceChannel = -1;

		// flags
		private bool viERunning = false;
		private bool voERunning = false;

		// debug
		private bool enableTrace = true;

		// Constant
		private const string TAG = "WEBRTC";
		private const int RECEIVE_CODEC_FRAMERATE = 15;
		private const int SEND_CODEC_FRAMERATE = 15;
		private const int INIT_BITRATE = 500;
		private const string LOOPBACK_IP = "127.0.0.1";
		// Zero means don't automatically start/stop calls.
		private const long AUTO_CALL_RESTART_DELAY_MS = 0;

		private Handler handler = new Handler();
		private Runnable startOrStopCallback = new RunnableAnonymousInnerClassHelper();

		private class RunnableAnonymousInnerClassHelper : Runnable
		{
			public RunnableAnonymousInnerClassHelper()
			{
			}

			public virtual void run()
			{
				outerInstance.startOrStop();
			}
		}

		private int volumeLevel = 204;

		private TabHost mTabHost = null;

		private TabHost.TabSpec mTabSpecConfig;
		private TabHost.TabSpec mTabSpecVideo;

		private LinearLayout mLlRemoteSurface = null;
		private LinearLayout mLlLocalSurface = null;

		private Button btStartStopCall;
		private Button btSwitchCamera;

		// Global Settings
		private CheckBox cbVideoSend;
		private bool enableVideoSend = true;
		private CheckBox cbVideoReceive;
		private bool enableVideoReceive = true;
		private bool enableVideo = true;
		private CheckBox cbVoice;
		private bool enableVoice = true;
		private EditText etRemoteIp;
		private string remoteIp = "";
		private CheckBox cbLoopback;
		private bool loopbackMode = true;
		private CheckBox cbStats;
		private bool isStatsOn = true;
		public enum RenderType
		{
			OPENGL,
			SURFACE,
			MEDIACODEC
		}
		internal RenderType renderType = RenderType.OPENGL;

		// Video settings
		private Spinner spCodecType;
		private int codecType = 0;
		private Spinner spCodecSize;
		private int codecSizeWidth = 0;
		private int codecSizeHeight = 0;
		private TextView etVRxPort;
		private int receivePortVideo = 11111;
		private TextView etVTxPort;
		private int destinationPortVideo = 11111;
		private CheckBox cbEnableNack;
		private bool enableNack = true;
		private CheckBox cbEnableVideoRTPDump;

		// Audio settings
		private Spinner spVoiceCodecType;
		private int voiceCodecType = 0;
		private TextView etARxPort;
		private int receivePortVoice = 11113;
		private TextView etATxPort;
		private int destinationPortVoice = 11113;
		private CheckBox cbEnableSpeaker;
		private CheckBox cbEnableAGC;
		private bool enableAGC = false;
		private CheckBox cbEnableAECM;
		private bool enableAECM = false;
		private CheckBox cbEnableNS;
		private bool enableNS = false;
		private CheckBox cbEnableDebugAPM;
		private CheckBox cbEnableVoiceRTPDump;

		// Stats variables
		private int frameRateI;
		private int bitRateI;
		private int packetLoss;
		private int frameRateO;
		private int bitRateO;
		private int numCalls = 0;

		private int widthI;
		private int heightI;

		// Variable for storing variables
		private string webrtcName = "/webrtc";
		private string webrtcDebugDir = null;

		private bool usingFrontCamera = true;
		// The orientations (in degrees) of each of the cameras CCW-relative to the
		// device, indexed by CameraInfo.CAMERA_FACING_{BACK,FRONT}, and -1
		// for unrepresented |facing| values (i.e. single-camera device).
		private int[] cameraOrientations = new int[] {-1, -1};

		private string[] mVideoCodecsStrings = null;
		private string[] mVideoCodecsSizeStrings = new string[] {"176x144", "320x240", "352x288", "640x480"};
		private string[] mVoiceCodecsStrings = null;

		private OrientationEventListener orientationListener;
		internal int currentDeviceOrientation = OrientationEventListener.ORIENTATION_UNKNOWN;

		private StatsView statsView = null;

		private BroadcastReceiver receiver;

		// Rounds rotation to the nearest 90 degree rotation.
		private static int roundRotation(int rotation)
		{
			return (int)(Math.Round((double)rotation / 90) * 90) % 360;
		}

		// Populate |cameraOrientations| with the first cameras that have each of
		// the facing values.
		private void populateCameraOrientations()
		{
			Camera.CameraInfo info = new Camera.CameraInfo();
			for (int i = 0; i < Camera.NumberOfCameras; ++i)
			{
				Camera.getCameraInfo(i, info);
				if (cameraOrientations[info.facing] != -1)
				{
					continue;
				}
				cameraOrientations[info.facing] = info.orientation;
			}
		}

		// Return the |CameraInfo.facing| value appropriate for |usingFrontCamera|.
		private static int facingOf(bool usingFrontCamera)
		{
			return usingFrontCamera ? Camera.CameraInfo.CAMERA_FACING_FRONT : Camera.CameraInfo.CAMERA_FACING_BACK;
		}

		// This function ensures that egress streams always send real world up
		// streams.
		// Note: There are two components of the camera rotation. The rotation of
		// the capturer relative to the device. I.e. up for the camera might not be
		// device up. When rotating the device the camera is also rotated.
		// The former is called orientation and the second is called rotation here.
		public virtual void compensateCameraRotation()
		{
			int cameraOrientation = cameraOrientations[facingOf(usingFrontCamera)];
			// The device orientation is the device's rotation relative to its
			// natural position.
			int cameraRotation = roundRotation(currentDeviceOrientation);

			int totalCameraRotation = 0;
			if (usingFrontCamera)
			{
				// The front camera rotates in the opposite direction of the
				// device.
				int inverseCameraRotation = (360 - cameraRotation) % 360;
				totalCameraRotation = (inverseCameraRotation + cameraOrientation) % 360;
			}
			else
			{
				totalCameraRotation = (cameraRotation + cameraOrientation) % 360;
			}
			vieAndroidAPI.SetRotation(cameraId, totalCameraRotation);
		}

		// Called when the activity is first created.
		public override void onCreate(Bundle savedInstanceState)
		{
			Log.d(TAG, "onCreate");

			base.onCreate(savedInstanceState);
			requestWindowFeature(Window.FEATURE_NO_TITLE);
			Window.addFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN);
			Window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
			RequestedOrientation = ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE;

			populateCameraOrientations();

			ContentView = R.layout.tabhost;

			IntentFilter receiverFilter = new IntentFilter(Intent.ACTION_HEADSET_PLUG);

			receiver = new BroadcastReceiverAnonymousInnerClassHelper(this);
			registerReceiver(receiver, receiverFilter);

			mTabHost = TabHost;

			// Main tab
			mTabSpecVideo = mTabHost.newTabSpec("tab_video");
			mTabSpecVideo.Indicator = "Main";
			mTabSpecVideo.Content = R.id.tab_video;
			mTabHost.addTab(mTabSpecVideo);

			// Shared config tab
			mTabHost = TabHost;
			mTabSpecConfig = mTabHost.newTabSpec("tab_config");
			mTabSpecConfig.Indicator = "Settings";
			mTabSpecConfig.Content = R.id.tab_config;
			mTabHost.addTab(mTabSpecConfig);

			TabHost.TabSpec mTabv;
			mTabv = mTabHost.newTabSpec("tab_vconfig");
			mTabv.Indicator = "Video";
			mTabv.Content = R.id.tab_vconfig;
			mTabHost.addTab(mTabv);
			TabHost.TabSpec mTaba;
			mTaba = mTabHost.newTabSpec("tab_aconfig");
			mTaba.Indicator = "Audio";
			mTaba.Content = R.id.tab_aconfig;
			mTabHost.addTab(mTaba);

			int childCount = mTabHost.TabWidget.ChildCount;
			for (int i = 0; i < childCount; i++)
			{
				mTabHost.TabWidget.getChildAt(i).LayoutParams.height = 50;
			}
			orientationListener = new OrientationEventListenerAnonymousInnerClassHelper(this, this, SensorManager.SENSOR_DELAY_UI);
			orientationListener.enable();

			// Create a folder named webrtc in /scard for debugging
			webrtcDebugDir = Environment.ExternalStorageDirectory.ToString() + webrtcName;
			File webrtcDir = new File(webrtcDebugDir);
			if (!webrtcDir.exists() && webrtcDir.mkdir() == false)
			{
				Log.v(TAG, "Failed to create " + webrtcDebugDir);
			}
			else if (!webrtcDir.Directory)
			{
				Log.v(TAG, webrtcDebugDir + " exists but not a folder");
				webrtcDebugDir = null;
			}

			startMain();

			if (AUTO_CALL_RESTART_DELAY_MS > 0)
			{
				startOrStop();
			}
		}

		private class BroadcastReceiverAnonymousInnerClassHelper : BroadcastReceiver
		{
			private readonly WebRTCDemo outerInstance;

			public BroadcastReceiverAnonymousInnerClassHelper(WebRTCDemo outerInstance)
			{
				this.outerInstance = outerInstance;
			}

			public override void onReceive(Context context, Intent intent)
			{
				if (intent.Action.compareTo(Intent.ACTION_HEADSET_PLUG) == 0)
				{
					int state = intent.getIntExtra("state", 0);
					Log.v(TAG, "Intent.ACTION_HEADSET_PLUG state: " + state + " microphone: " + intent.getIntExtra("microphone", 0));
					if (outerInstance.voERunning)
					{
						outerInstance.routeAudio(state == 0 && outerInstance.cbEnableSpeaker.Checked);
					}
				}
			}
		}

		private class OrientationEventListenerAnonymousInnerClassHelper : OrientationEventListener
		{
			private readonly WebRTCDemo outerInstance;

			public OrientationEventListenerAnonymousInnerClassHelper(WebRTCDemo outerInstance, org.webrtc.videoengineapp.WebRTCDemo this, UnknownType SENSOR_DELAY_UI) : base(this, SENSOR_DELAY_UI)
			{
				this.outerInstance = outerInstance;
			}

			public virtual void onOrientationChanged(int orientation)
			{
				if (orientation != ORIENTATION_UNKNOWN)
				{
					outerInstance.currentDeviceOrientation = orientation;
					outerInstance.compensateCameraRotation();
				}
			}
		}

		// Called before the activity is destroyed.
		public override void onDestroy()
		{
			Log.d(TAG, "onDestroy");
			handler.removeCallbacks(startOrStopCallback);
			unregisterReceiver(receiver);
			base.onDestroy();
		}

		private class StatsView : View
		{
			private readonly WebRTCDemo outerInstance;

			public StatsView(WebRTCDemo outerInstance, Context context) : base(context)
			{
				this.outerInstance = outerInstance;
			}

			protected internal override void onDraw(Canvas canvas)
			{
				base.onDraw(canvas);
				// Only draw Stats in Main tab.
				if (outerInstance.mTabHost.CurrentTabTag == "tab_video")
				{
					Paint loadPaint = new Paint();
					loadPaint.AntiAlias = true;
					loadPaint.TextSize = 16;
					loadPaint.setARGB(255, 255, 255, 255);

					canvas.drawText("#calls " + outerInstance.numCalls, 4, 222, loadPaint);

					string loadText;
					loadText = "> " + outerInstance.frameRateI + " fps/" + outerInstance.bitRateI / 1024 + " kbps/ " + outerInstance.packetLoss;
					canvas.drawText(loadText, 4, 242, loadPaint);
					loadText = "< " + outerInstance.frameRateO + " fps/ " + outerInstance.bitRateO / 1024 + " kbps";
					canvas.drawText(loadText, 4, 262, loadPaint);
					loadText = "Incoming resolution " + outerInstance.widthI + "x" + outerInstance.heightI;
					canvas.drawText(loadText, 4, 282, loadPaint);
				}
				updateDisplay();
			}

			internal virtual void updateDisplay()
			{
				invalidate();
			}
		}

		private string LocalIpAddress
		{
			get
			{
				string localIPs = "";
				try
				{
					for (IEnumerator<NetworkInterface> en = NetworkInterface.NetworkInterfaces; en.MoveNext();)
					{
						NetworkInterface intf = en.Current;
						for (IEnumerator<InetAddress> enumIpAddr = intf.InetAddresses; enumIpAddr.MoveNext();)
						{
							InetAddress inetAddress = enumIpAddr.Current;
							if (!inetAddress.LoopbackAddress)
							{
								localIPs += inetAddress.HostAddress.ToString() + " ";
								// Set the remote ip address the same as
								// the local ip address of the last netif
								remoteIp = inetAddress.HostAddress.ToString();
							}
						}
					}
				}
				catch (SocketException ex)
				{
					Log.e(TAG, ex.ToString());
				}
				return localIPs;
			}
		}

		public override bool onKeyDown(int keyCode, KeyEvent @event)
		{
			if (keyCode == KeyEvent.KEYCODE_BACK)
			{
				if (viERunning)
				{
					stopAll();
					startMain();
				}
				finish();
				return true;
			}
			return base.onKeyDown(keyCode, @event);
		}

		private void stopAll()
		{
			Log.d(TAG, "stopAll");

			if (vieAndroidAPI != null)
			{

				if (voERunning)
				{
					voERunning = false;
					stopVoiceEngine();
				}

				if (viERunning)
				{
					viERunning = false;
					vieAndroidAPI.StopRender(channel);
					vieAndroidAPI.StopReceive(channel);
					vieAndroidAPI.StopSend(channel);
					vieAndroidAPI.RemoveRemoteRenderer(channel);
					vieAndroidAPI.ViE_DeleteChannel(channel);
					channel = -1;
					vieAndroidAPI.StopCamera(cameraId);
					vieAndroidAPI.Terminate();
					mLlRemoteSurface.removeView(remoteSurfaceView);
					mLlLocalSurface.removeView(svLocal);
					remoteSurfaceView = null;
					svLocal = null;
				}
			}
		}

		/// <summary>
		/// {@ArrayAdapter} </summary>
		public class SpinnerAdapter : ArrayAdapter<string>
		{
			private readonly WebRTCDemo outerInstance;

			internal string[] mCodecString = null;
			public SpinnerAdapter(WebRTCDemo outerInstance, Context context, int textViewResourceId, string[] objects) : base(context, textViewResourceId, objects)
			{
				this.outerInstance = outerInstance;
				mCodecString = objects;
			}

			public override View getDropDownView(int position, View convertView, ViewGroup parent)
			{
				return getCustomView(position, convertView, parent);
			}

			public override View getView(int position, View convertView, ViewGroup parent)
			{
				return getCustomView(position, convertView, parent);
			}

			public virtual View getCustomView(int position, View convertView, ViewGroup parent)
			{
				LayoutInflater inflater = LayoutInflater;
				View row = inflater.inflate(R.layout.row, parent, false);
				TextView label = (TextView) row.findViewById(R.id.spinner_row);
				label.Text = mCodecString[position];
				return row;
			}
		}

		private void startMain()
		{
			mTabHost.CurrentTab = 0;

			mLlRemoteSurface = (LinearLayout) findViewById(R.id.llRemoteView);
			mLlLocalSurface = (LinearLayout) findViewById(R.id.llLocalView);

			if (null == vieAndroidAPI)
			{
				vieAndroidAPI = new ViEAndroidJavaAPI(this);
			}
			if (0 > setupVoE() || 0 > vieAndroidAPI.GetVideoEngine() || 0 > vieAndroidAPI.Init(enableTrace))
			{
				// Show dialog
				AlertDialog alertDialog = (new AlertDialog.Builder(this)).create();
				alertDialog.Title = "WebRTC Error";
				alertDialog.Message = "Can not init video engine.";
				alertDialog.setButton(DialogInterface.BUTTON_POSITIVE, "OK", new OnClickListenerAnonymousInnerClassHelper(this));
				alertDialog.show();
			}

			btSwitchCamera = (Button) findViewById(R.id.btSwitchCamera);
			if (cameraOrientations[0] != -1 && cameraOrientations[1] != -1)
			{
				btSwitchCamera.OnClickListener = this;
			}
			else
			{
				btSwitchCamera.Enabled = false;
			}
			btStartStopCall = (Button) findViewById(R.id.btStartStopCall);
			btStartStopCall.OnClickListener = this;
			findViewById(R.id.btExit).OnClickListener = this;

			// cleaning
			remoteSurfaceView = null;
			svLocal = null;

			// Video codec
			mVideoCodecsStrings = vieAndroidAPI.GetCodecs();
			spCodecType = (Spinner) findViewById(R.id.spCodecType);
			spCodecType.OnItemSelectedListener = this;
			spCodecType.Adapter = new SpinnerAdapter(this, this, R.layout.row, mVideoCodecsStrings);
			spCodecType.Selection = 0;

			// Video Codec size
			spCodecSize = (Spinner) findViewById(R.id.spCodecSize);
			spCodecSize.OnItemSelectedListener = this;
			spCodecSize.Adapter = new SpinnerAdapter(this, this, R.layout.row, mVideoCodecsSizeStrings);
			spCodecSize.Selection = mVideoCodecsSizeStrings.Length - 1;

			// Voice codec
			mVoiceCodecsStrings = vieAndroidAPI.VoE_GetCodecs();
			spVoiceCodecType = (Spinner) findViewById(R.id.spVoiceCodecType);
			spVoiceCodecType.OnItemSelectedListener = this;
			spVoiceCodecType.Adapter = new SpinnerAdapter(this, this, R.layout.row, mVoiceCodecsStrings);
			spVoiceCodecType.Selection = 0;
			// Find ISAC and use it
			for (int i = 0; i < mVoiceCodecsStrings.Length; ++i)
			{
				if (mVoiceCodecsStrings[i].Contains("ISAC"))
				{
					spVoiceCodecType.Selection = i;
					break;
				}
			}

			RadioGroup radioGroup = (RadioGroup) findViewById(R.id.radio_group1);
			radioGroup.clearCheck();
			if (renderType == RenderType.OPENGL)
			{
				radioGroup.check(R.id.radio_opengl);
			}
			else if (renderType == RenderType.SURFACE)
			{
				radioGroup.check(R.id.radio_surface);
			}
			else if (renderType == RenderType.MEDIACODEC)
			{
				radioGroup.check(R.id.radio_mediacodec);
			}

			etRemoteIp = (EditText) findViewById(R.id.etRemoteIp);
			etRemoteIp.Text = remoteIp;

			cbLoopback = (CheckBox) findViewById(R.id.cbLoopback);
			cbLoopback.Checked = loopbackMode;

			cbStats = (CheckBox) findViewById(R.id.cbStats);
			cbStats.Checked = isStatsOn;

			cbVoice = (CheckBox) findViewById(R.id.cbVoice);
			cbVoice.Checked = enableVoice;

			cbVideoSend = (CheckBox) findViewById(R.id.cbVideoSend);
			cbVideoSend.Checked = enableVideoSend;
			cbVideoReceive = (CheckBox) findViewById(R.id.cbVideoReceive);
			cbVideoReceive.Checked = enableVideoReceive;

			etVTxPort = (EditText) findViewById(R.id.etVTxPort);
			etVTxPort.Text = Convert.ToString(destinationPortVideo);

			etVRxPort = (EditText) findViewById(R.id.etVRxPort);
			etVRxPort.Text = Convert.ToString(receivePortVideo);

			etATxPort = (EditText) findViewById(R.id.etATxPort);
			etATxPort.Text = Convert.ToString(destinationPortVoice);

			etARxPort = (EditText) findViewById(R.id.etARxPort);
			etARxPort.Text = Convert.ToString(receivePortVoice);

			cbEnableNack = (CheckBox) findViewById(R.id.cbNack);
			cbEnableNack.Checked = enableNack;

			cbEnableSpeaker = (CheckBox) findViewById(R.id.cbSpeaker);
			cbEnableAGC = (CheckBox) findViewById(R.id.cbAutoGainControl);
			cbEnableAGC.Checked = enableAGC;
			cbEnableAECM = (CheckBox) findViewById(R.id.cbAECM);
			cbEnableAECM.Checked = enableAECM;
			cbEnableNS = (CheckBox) findViewById(R.id.cbNoiseSuppression);
			cbEnableNS.Checked = enableNS;

			cbEnableDebugAPM = (CheckBox) findViewById(R.id.cbDebugRecording);
			cbEnableDebugAPM.Checked = false; // Disable APM debugging by default

			cbEnableVideoRTPDump = (CheckBox) findViewById(R.id.cbVideoRTPDump);
			cbEnableVideoRTPDump.Checked = false; // Disable Video RTP Dump

			cbEnableVoiceRTPDump = (CheckBox) findViewById(R.id.cbVoiceRTPDump);
			cbEnableVoiceRTPDump.Checked = false; // Disable Voice RTP Dump

			etRemoteIp.OnClickListener = this;
			cbLoopback.OnClickListener = this;
			cbStats.OnClickListener = this;
			cbEnableNack.OnClickListener = this;
			cbEnableSpeaker.OnClickListener = this;
			cbEnableAECM.OnClickListener = this;
			cbEnableAGC.OnClickListener = this;
			cbEnableNS.OnClickListener = this;
			cbEnableDebugAPM.OnClickListener = this;
			cbEnableVideoRTPDump.OnClickListener = this;
			cbEnableVoiceRTPDump.OnClickListener = this;

			if (loopbackMode)
			{
				remoteIp = LOOPBACK_IP;
				etRemoteIp.Text = remoteIp;
			}
			else
			{
				LocalIpAddress;
				etRemoteIp.Text = remoteIp;
			}

			// Read settings to refresh each configuration
			readSettings();
		}

		private class OnClickListenerAnonymousInnerClassHelper : DialogInterface.OnClickListener
		{
			private readonly WebRTCDemo outerInstance;

			public OnClickListenerAnonymousInnerClassHelper(WebRTCDemo outerInstance)
			{
				this.outerInstance = outerInstance;
			}

			public virtual void onClick(DialogInterface dialog, int which)
			{
				return;
			}
		}

		private string RemoteIPString
		{
			get
			{
				return etRemoteIp.Text.ToString();
			}
		}

		private void startCall()
		{
			int ret = 0;

			if (enableVoice)
			{
				startVoiceEngine();
			}

			if (enableVideo)
			{
				if (enableVideoSend)
				{
					// camera and preview surface
					svLocal = ViERenderer.CreateLocalRenderer(this);
				}

				channel = vieAndroidAPI.CreateChannel(voiceChannel);
				ret = vieAndroidAPI.SetLocalReceiver(channel, receivePortVideo);
				ret = vieAndroidAPI.SetSendDestination(channel, destinationPortVideo, RemoteIPString);

				if (enableVideoReceive)
				{
					if (renderType == RenderType.OPENGL)
					{
						Log.v(TAG, "Create OpenGL Render");
						remoteSurfaceView = ViERenderer.CreateRenderer(this, true);
					}
					else if (renderType == RenderType.SURFACE)
					{
						Log.v(TAG, "Create SurfaceView Render");
						remoteSurfaceView = ViERenderer.CreateRenderer(this, false);
					}
					else if (renderType == RenderType.MEDIACODEC)
					{
						Log.v(TAG, "Create MediaCodec Decoder/Renderer");
						remoteSurfaceView = new SurfaceView(this);
					}

					if (mLlRemoteSurface != null)
					{
						mLlRemoteSurface.addView(remoteSurfaceView);
					}

					if (renderType == RenderType.MEDIACODEC)
					{
						ret = vieAndroidAPI.SetExternalMediaCodecDecoderRenderer(channel, remoteSurfaceView);
					}
					else
					{
						ret = vieAndroidAPI.AddRemoteRenderer(channel, remoteSurfaceView);
					}

					ret = vieAndroidAPI.SetReceiveCodec(channel, codecType, INIT_BITRATE, codecSizeWidth, codecSizeHeight, RECEIVE_CODEC_FRAMERATE);
					ret = vieAndroidAPI.StartRender(channel);
					ret = vieAndroidAPI.StartReceive(channel);
				}

				if (enableVideoSend)
				{
					ret = vieAndroidAPI.SetSendCodec(channel, codecType, INIT_BITRATE, codecSizeWidth, codecSizeHeight, SEND_CODEC_FRAMERATE);
					int camId = vieAndroidAPI.StartCamera(channel, usingFrontCamera ? 1 : 0);

					if (camId >= 0)
					{
						cameraId = camId;
						compensateCameraRotation();
					}
					else
					{
						ret = camId;
					}
					ret = vieAndroidAPI.StartSend(channel);
				}

				// TODO(leozwang): Add more options besides PLI, currently use pli
				// as the default. Also check return value.
				ret = vieAndroidAPI.EnablePLI(channel, true);
				ret = vieAndroidAPI.EnableNACK(channel, enableNack);
				ret = vieAndroidAPI.SetCallback(channel, this);

				if (enableVideoSend)
				{
					if (mLlLocalSurface != null)
					{
						mLlLocalSurface.addView(svLocal);
					}
				}

				isStatsOn = cbStats.Checked;
				if (isStatsOn)
				{
					addStatusView();
				}
				else
				{
					removeStatusView();
				}

				viERunning = true;
			}
		}

		private void stopVoiceEngine()
		{
			// Stop send
			if (0 != vieAndroidAPI.VoE_StopSend(voiceChannel))
			{
				Log.d(TAG, "VoE stop send failed");
			}

			// Stop listen
			if (0 != vieAndroidAPI.VoE_StopListen(voiceChannel))
			{
				Log.d(TAG, "VoE stop listen failed");
			}

			// Stop playout
			if (0 != vieAndroidAPI.VoE_StopPlayout(voiceChannel))
			{
				Log.d(TAG, "VoE stop playout failed");
			}

			if (0 != vieAndroidAPI.VoE_DeleteChannel(voiceChannel))
			{
				Log.d(TAG, "VoE delete channel failed");
			}
			voiceChannel = -1;

			// Terminate
			if (0 != vieAndroidAPI.VoE_Terminate())
			{
				Log.d(TAG, "VoE terminate failed");
			}
		}

		private int setupVoE()
		{
			// Create VoiceEngine
			// Error logging is done in native API wrapper
			vieAndroidAPI.VoE_Create(ApplicationContext);

			// Initialize
			if (0 != vieAndroidAPI.VoE_Init(enableTrace))
			{
				Log.d(TAG, "VoE init failed");
				return -1;
			}

			// Suggest to use the voice call audio stream for hardware volume controls
			VolumeControlStream = AudioManager.STREAM_VOICE_CALL;
			return 0;
		}

		private int startVoiceEngine()
		{
			// Create channel
			voiceChannel = vieAndroidAPI.VoE_CreateChannel();
			if (0 > voiceChannel)
			{
				Log.d(TAG, "VoE create channel failed");
				return -1;
			}

			// Set local receiver
			if (0 != vieAndroidAPI.VoE_SetLocalReceiver(voiceChannel, receivePortVoice))
			{
				Log.d(TAG, "VoE set local receiver failed");
			}

			if (0 != vieAndroidAPI.VoE_StartListen(voiceChannel))
			{
				Log.d(TAG, "VoE start listen failed");
			}

			// Route audio
			routeAudio(cbEnableSpeaker.Checked);

			// set volume to default value
			if (0 != vieAndroidAPI.VoE_SetSpeakerVolume(volumeLevel))
			{
				Log.d(TAG, "VoE set speaker volume failed");
			}

			// Start playout
			if (0 != vieAndroidAPI.VoE_StartPlayout(voiceChannel))
			{
				Log.d(TAG, "VoE start playout failed");
			}

			if (0 != vieAndroidAPI.VoE_SetSendDestination(voiceChannel, destinationPortVoice, RemoteIPString))
			{
				Log.d(TAG, "VoE set send  destination failed");
			}

			if (0 != vieAndroidAPI.VoE_SetSendCodec(voiceChannel, voiceCodecType))
			{
				Log.d(TAG, "VoE set send codec failed");
			}

			if (0 != vieAndroidAPI.VoE_SetECStatus(enableAECM))
			{
				Log.d(TAG, "VoE set EC Status failed");
			}

			if (0 != vieAndroidAPI.VoE_SetAGCStatus(enableAGC))
			{
				Log.d(TAG, "VoE set AGC Status failed");
			}

			if (0 != vieAndroidAPI.VoE_SetNSStatus(enableNS))
			{
				Log.d(TAG, "VoE set NS Status failed");
			}

			if (0 != vieAndroidAPI.VoE_StartSend(voiceChannel))
			{
				Log.d(TAG, "VoE start send failed");
			}

			voERunning = true;
			return 0;
		}

		private void routeAudio(bool enableSpeaker)
		{
			if (0 != vieAndroidAPI.VoE_SetLoudspeakerStatus(enableSpeaker))
			{
				Log.d(TAG, "VoE set louspeaker status failed");
			}
		}

		private void startOrStop()
		{
			readSettings();
			if (viERunning || voERunning)
			{
				stopAll();
				startMain();
				btStartStopCall.Text = R.@string.startCall;
			}
			else if (enableVoice || enableVideo)
			{
				++numCalls;
				startCall();
				btStartStopCall.Text = R.@string.stopCall;
			}
			if (AUTO_CALL_RESTART_DELAY_MS > 0)
			{
				handler.postDelayed(startOrStopCallback, AUTO_CALL_RESTART_DELAY_MS);
			}
		}

		public virtual void onClick(View arg0)
		{
			switch (arg0.Id)
			{
				case R.id.btSwitchCamera:
					if (usingFrontCamera)
					{
						btSwitchCamera.Text = R.@string.frontCamera;
					}
					else
					{
						btSwitchCamera.Text = R.@string.backCamera;
					}
					usingFrontCamera = !usingFrontCamera;

					if (viERunning)
					{
						vieAndroidAPI.StopCamera(cameraId);
						mLlLocalSurface.removeView(svLocal);

						vieAndroidAPI.StartCamera(channel, usingFrontCamera ? 1 : 0);
						mLlLocalSurface.addView(svLocal);
						compensateCameraRotation();
					}
					break;
				case R.id.btStartStopCall:
				  startOrStop();
				  break;
				case R.id.btExit:
					stopAll();
					finish();
					break;
				case R.id.cbLoopback:
					loopbackMode = cbLoopback.Checked;
					if (loopbackMode)
					{
						remoteIp = LOOPBACK_IP;
						etRemoteIp.Text = LOOPBACK_IP;
					}
					else
					{
						LocalIpAddress;
						etRemoteIp.Text = remoteIp;
					}
					break;
				case R.id.etRemoteIp:
					remoteIp = etRemoteIp.Text.ToString();
					break;
				case R.id.cbStats:
					isStatsOn = cbStats.Checked;
					if (isStatsOn)
					{
						addStatusView();
					}
					else
					{
						removeStatusView();
					}
					break;
				case R.id.radio_surface:
					renderType = RenderType.SURFACE;
					break;
				case R.id.radio_opengl:
					renderType = RenderType.OPENGL;
					break;
				case R.id.radio_mediacodec:
					renderType = RenderType.MEDIACODEC;
					break;
				case R.id.cbNack:
					enableNack = cbEnableNack.Checked;
					if (viERunning)
					{
						vieAndroidAPI.EnableNACK(channel, enableNack);
					}
					break;
				case R.id.cbSpeaker:
					if (voERunning)
					{
						routeAudio(cbEnableSpeaker.Checked);
					}
					break;
				case R.id.cbDebugRecording:
					if (voERunning && webrtcDebugDir != null)
					{
						if (cbEnableDebugAPM.Checked)
						{
							vieAndroidAPI.VoE_StartDebugRecording(webrtcDebugDir + string.Format("/apm_{0:D}.dat", DateTimeHelperClass.CurrentUnixTimeMillis()));
						}
						else
						{
							vieAndroidAPI.VoE_StopDebugRecording();
						}
					}
					break;
				case R.id.cbVoiceRTPDump:
					if (voERunning && webrtcDebugDir != null)
					{
						if (cbEnableVoiceRTPDump.Checked)
						{
							vieAndroidAPI.VoE_StartIncomingRTPDump(channel, webrtcDebugDir + string.Format("/voe_{0:D}.rtp", DateTimeHelperClass.CurrentUnixTimeMillis()));
						}
						else
						{
							vieAndroidAPI.VoE_StopIncomingRTPDump(channel);
						}
					}
					break;
				case R.id.cbVideoRTPDump:
					if (viERunning && webrtcDebugDir != null)
					{
						if (cbEnableVideoRTPDump.Checked)
						{
							vieAndroidAPI.StartIncomingRTPDump(channel, webrtcDebugDir + string.Format("/vie_{0:D}.rtp", DateTimeHelperClass.CurrentUnixTimeMillis()));
						}
						else
						{
							vieAndroidAPI.StopIncomingRTPDump(channel);
						}
					}
					break;
				case R.id.cbAutoGainControl:
					enableAGC = cbEnableAGC.Checked;
					if (voERunning)
					{
						vieAndroidAPI.VoE_SetAGCStatus(enableAGC);
					}
					break;
				case R.id.cbNoiseSuppression:
					enableNS = cbEnableNS.Checked;
					if (voERunning)
					{
						vieAndroidAPI.VoE_SetNSStatus(enableNS);
					}
					break;
				case R.id.cbAECM:
					enableAECM = cbEnableAECM.Checked;
					if (voERunning)
					{
						vieAndroidAPI.VoE_SetECStatus(enableAECM);
					}
					break;
			}
		}

		private void readSettings()
		{
			codecType = spCodecType.SelectedItemPosition;
			voiceCodecType = spVoiceCodecType.SelectedItemPosition;

			string sCodecSize = spCodecSize.SelectedItem.ToString();
			string[] aCodecSize = sCodecSize.Split("x", true);
			codecSizeWidth = Convert.ToInt32(aCodecSize[0]);
			codecSizeHeight = Convert.ToInt32(aCodecSize[1]);

			loopbackMode = cbLoopback.Checked;
			enableVoice = cbVoice.Checked;
			enableVideoSend = cbVideoSend.Checked;
			enableVideoReceive = cbVideoReceive.Checked;
			enableVideo = enableVideoSend || enableVideoReceive;

			destinationPortVideo = Convert.ToInt32(etVTxPort.Text.ToString());
			receivePortVideo = Convert.ToInt32(etVRxPort.Text.ToString());
			destinationPortVoice = Convert.ToInt32(etATxPort.Text.ToString());
			receivePortVoice = Convert.ToInt32(etARxPort.Text.ToString());

			enableNack = cbEnableNack.Checked;
			enableAGC = cbEnableAGC.Checked;
			enableAECM = cbEnableAECM.Checked;
			enableNS = cbEnableNS.Checked;
		}

		public virtual void onItemSelected<T1>(AdapterView<T1> adapterView, View view, int position, long id)
		{
			if ((adapterView == spCodecType || adapterView == spCodecSize) && viERunning)
			{
				readSettings();
				// change the codectype
				if (enableVideoReceive)
				{
					if (0 != vieAndroidAPI.SetReceiveCodec(channel, codecType, INIT_BITRATE, codecSizeWidth, codecSizeHeight, RECEIVE_CODEC_FRAMERATE))
					{
						Log.d(TAG, "ViE set receive codec failed");
					}
				}
				if (enableVideoSend)
				{
					if (0 != vieAndroidAPI.SetSendCodec(channel, codecType, INIT_BITRATE, codecSizeWidth, codecSizeHeight, SEND_CODEC_FRAMERATE))
					{
						Log.d(TAG, "ViE set send codec failed");
					}
				}
			}
			else if ((adapterView == spVoiceCodecType) && voERunning)
			{
				// change voice engine codec
				readSettings();
				if (0 != vieAndroidAPI.VoE_SetSendCodec(voiceChannel, voiceCodecType))
				{
					Log.d(TAG, "VoE set send codec failed");
				}
			}
		}

		public virtual void onNothingSelected<T1>(AdapterView<T1> arg0)
		{
			Log.d(TAG, "No setting selected");
		}

		public virtual int UpdateStats(int inFrameRateI, int inBitRateI, int inPacketLoss, int inFrameRateO, int inBitRateO)
		{
			frameRateI = inFrameRateI;
			bitRateI = inBitRateI;
			packetLoss = inPacketLoss;
			frameRateO = inFrameRateO;
			bitRateO = inBitRateO;
			return 0;
		}

		public virtual int NewIncomingResolution(int width, int height)
		{
			widthI = width;
			heightI = height;
			return 0;
		}

		private void addStatusView()
		{
			if (statsView != null)
			{
				return;
			}
			statsView = new StatsView(this, this);
			WindowManager.LayoutParams @params = new WindowManager.LayoutParams(WindowManager.LayoutParams.MATCH_PARENT, WindowManager.LayoutParams.WRAP_CONTENT, WindowManager.LayoutParams.TYPE_SYSTEM_OVERLAY, WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE | WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE, PixelFormat.TRANSLUCENT);
			@params.gravity = Gravity.RIGHT | Gravity.TOP;
			@params.Title = "Load Average";
			mTabHost.addView(statsView, @params);
			statsView.BackgroundColor = 0;
		}

		private void removeStatusView()
		{
			mTabHost.removeView(statsView);
			statsView = null;
		}

	}

}