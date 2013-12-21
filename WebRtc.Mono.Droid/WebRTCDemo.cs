/*
 *  Copyright (c) 2012 The WebRTC project authors. All Rights Reserved.
 *
 *  Use of this source code is governed by a BSD-style license
 *  that can be found in the LICENSE file in the root of the source
 *  tree. An additional intellectual property rights grant can be found
 *  in the file PATENTS.  All contributing project authors may
 *  be found in the AUTHORS file in the root of the source tree.
 */
using System;
using System.Collections.Generic;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Hardware;
using Android.Media;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.IO;
using Java.Lang;
using Java.Net;
using WebRtc.Org.Webrtc.Videoengine;
using Camera = Android.Hardware.Camera;
using Environment = System.Environment;
using Math = System.Math;

namespace WebRtc.Mono.Droid
{
	[Activity(Label = "WebRtc.Mono.Droid", MainLauncher = true, Icon = "@drawable/logo")]
	public class WebRTCDemo : TabActivity, IViEAndroidCallback, View.IOnClickListener, AdapterView.IOnItemSelectedListener
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
		private IRunnable startOrStopCallback; 

		public WebRTCDemo()
		{
			startOrStopCallback = new RunnableAnonymousInnerClassHelper(this);
		}

		private class RunnableAnonymousInnerClassHelper : IRunnable
		{
			private readonly WebRTCDemo _webRtcDemo;

			public RunnableAnonymousInnerClassHelper(WebRTCDemo webRtcDemo)
			{
				_webRtcDemo = webRtcDemo;
			}

			public void Run()
			{
				_webRtcDemo.StartOrStop();
			}

			public void Dispose()
			{
				throw new NotImplementedException();
			}

			public IntPtr Handle { get; private set; }
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
		internal int currentDeviceOrientation = OrientationEventListener.OrientationUnknown;

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
				Camera.GetCameraInfo(i, info);
				if (cameraOrientations[(int)info.Facing] != -1)
				{
					continue;
				}
				cameraOrientations[(int)info.Facing] = info.Orientation;
			}
		}

		// Return the |CameraInfo.facing| value appropriate for |usingFrontCamera|.
		private static CameraFacing facingOf(bool usingFrontCamera)
		{
			return usingFrontCamera ? Camera.CameraInfo.CameraFacingFront: Camera.CameraInfo.CameraFacingBack;
		}

		// This function ensures that egress streams always send real world up
		// streams.
		// Note: There are two components of the camera rotation. The rotation of
		// the capturer relative to the device. I.e. up for the camera might not be
		// device up. When rotating the device the camera is also rotated.
		// The former is called orientation and the second is called rotation here.
		public virtual void compensateCameraRotation()
		{
			int cameraOrientation = cameraOrientations[(int)facingOf(usingFrontCamera)];
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
			ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_SetRotation(cameraId, totalCameraRotation);
		}

		// Called when the activity is first created.
		protected override void OnCreate(Bundle savedInstanceState)
		{
			Log.Debug(TAG, "onCreate");

			base.OnCreate(savedInstanceState);
			RequestWindowFeature(WindowFeatures.NoTitle);
			Window.AddFlags(WindowManagerFlags.Fullscreen);
			Window.AddFlags(WindowManagerFlags.KeepScreenOn);
			RequestedOrientation = ScreenOrientation.Landscape;

			populateCameraOrientations();

			SetContentView(Resource.Layout.tabhost);

			IntentFilter receiverFilter = new IntentFilter(Intent.ActionHeadsetPlug);

			receiver = new BroadcastReceiverAnonymousInnerClassHelper(this);
			RegisterReceiver(receiver, receiverFilter);

			mTabHost = TabHost;

			// Main tab
			mTabSpecVideo = mTabHost.NewTabSpec("tab_video");
			mTabSpecVideo.SetIndicator("Main");
			mTabSpecVideo.SetContent(Resource.Id.tab_video);
			mTabHost.AddTab(mTabSpecVideo);

			// Shared config tab
			mTabHost = TabHost;
			mTabSpecConfig = mTabHost.NewTabSpec("tab_config");
			mTabSpecConfig.SetIndicator("Settings");
			mTabSpecConfig.SetContent(Resource.Id.tab_config);
			mTabHost.AddTab(mTabSpecConfig);

			TabHost.TabSpec mTabv;
			mTabv = mTabHost.NewTabSpec("tab_vconfig");
			mTabv.SetIndicator("Video");
			mTabv.SetContent(Resource.Id.tab_vconfig);
			mTabHost.AddTab(mTabv);
			TabHost.TabSpec mTaba;
			mTaba = mTabHost.NewTabSpec("tab_aconfig");
			mTaba.SetIndicator("Audio");
			mTaba.SetContent(Resource.Id.tab_aconfig);
			mTabHost.AddTab(mTaba);

			int childCount = mTabHost.TabWidget.ChildCount;
			for (int i = 0; i < childCount; i++)
			{
				mTabHost.TabWidget.GetChildAt(i).LayoutParameters.Height = 50;
			}
			orientationListener = new OrientationEventListenerAnonymousInnerClassHelper(this, SensorDelay.Ui);
			orientationListener.Enable();

			// Create a folder named webrtc in /scard for debugging
			webrtcDebugDir = Android.OS.Environment.ExternalStorageDirectory.ToString() + webrtcName;
			File webrtcDir = new File(webrtcDebugDir);
			if (!webrtcDir.Exists() && webrtcDir.Mkdir() == false)
			{
				Log.Verbose(TAG, "Failed to create " + webrtcDebugDir);
			}
			else if (!webrtcDir.IsAbsolute)
			{
				Log.Verbose(TAG, webrtcDebugDir + " exists but not a folder");
				webrtcDebugDir = null;
			}

			startMain();

			if (AUTO_CALL_RESTART_DELAY_MS > 0)
			{
				StartOrStop();
			}
		}

		private class BroadcastReceiverAnonymousInnerClassHelper : BroadcastReceiver
		{
			private readonly WebRTCDemo outerInstance;

			public BroadcastReceiverAnonymousInnerClassHelper(WebRTCDemo outerInstance)
			{
				this.outerInstance = outerInstance;
			}

			public override void OnReceive(Context context, Intent intent)
			{
				if (intent.Action.CompareTo(Intent.ActionHeadsetPlug) == 0)
				{
					int state = intent.GetIntExtra("state", 0);
					Log.Verbose(TAG, "Intent.ACTION_HEADSET_PLUG state: " + state + " microphone: " + intent.GetIntExtra("microphone", 0));
					if (outerInstance.voERunning)
					{
						outerInstance.RouteAudio(state == 0 && outerInstance.cbEnableSpeaker.Checked);
					}
				}
			}
		}

		private class OrientationEventListenerAnonymousInnerClassHelper : OrientationEventListener
		{
			private readonly WebRTCDemo outerInstance;

			public OrientationEventListenerAnonymousInnerClassHelper(WebRTCDemo outerInstance, SensorDelay sensorDelayUI)
				: base(outerInstance, SensorDelay.Ui)
			{
				this.outerInstance = outerInstance;
			}

			public override void OnOrientationChanged(int orientation)
			{
				if (orientation != OrientationUnknown)
				{
					outerInstance.currentDeviceOrientation = orientation;
					outerInstance.compensateCameraRotation();
				}
			}
		}

		// Called before the activity is destroyed.
		protected override void OnDestroy()
		{
			Log.Debug(TAG, "onDestroy");
			handler.RemoveCallbacks(startOrStopCallback);
			UnregisterReceiver(receiver);
			base.OnDestroy();
		}

		private class StatsView : View
		{
			private readonly WebRTCDemo outerInstance;

			public StatsView(WebRTCDemo outerInstance, Context context) : base(context)
			{
				this.outerInstance = outerInstance;
			}

			protected override void OnDraw(Canvas canvas)
			{
				base.OnDraw(canvas);
				// Only draw Stats in Main tab.
				if (outerInstance.mTabHost.CurrentTabTag == "tab_video")
				{
					Paint loadPaint = new Paint();
					loadPaint.AntiAlias = true;
					loadPaint.TextSize = 16;
					loadPaint.SetARGB(255, 255, 255, 255);

					canvas.DrawText("#calls " + outerInstance.numCalls, 4, 222, loadPaint);

					string loadText;
					loadText = "> " + outerInstance.frameRateI + " fps/" + outerInstance.bitRateI / 1024 + " kbps/ " + outerInstance.packetLoss;
					canvas.DrawText(loadText, 4, 242, loadPaint);
					loadText = "< " + outerInstance.frameRateO + " fps/ " + outerInstance.bitRateO / 1024 + " kbps";
					canvas.DrawText(loadText, 4, 262, loadPaint);
					loadText = "Incoming resolution " + outerInstance.widthI + "x" + outerInstance.heightI;
					canvas.DrawText(loadText, 4, 282, loadPaint);
				}
				UpdateDisplay();
			}

			internal virtual void UpdateDisplay()
			{
				Invalidate();
			}
		}

		private string LocalIpAddress
		{
			get
			{
				string localIPs = "";
				try
				{
					Java.Util.IEnumeration networkInterfaces = NetworkInterface.NetworkInterfaces;
					while (networkInterfaces.HasMoreElements)
					{
						var netInterface = (NetworkInterface)networkInterfaces.NextElement();
						while (netInterface.InetAddresses.HasMoreElements)
						{
							var inetAddress = (InetAddress)netInterface.InetAddresses.NextElement();
							if (inetAddress != InetAddress.LoopbackAddress)
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
					Log.Error(TAG, ex.ToString());
				}
				return localIPs;
			}
		}

		public override bool OnKeyDown(Keycode keyCode, KeyEvent e)
		{
			if (keyCode == Keycode.Back)
			{
				if (viERunning)
				{
					StopAll();
					startMain();
				}
				Finish();
				return true;
			}
			return base.OnKeyDown(keyCode, e);
		}

		private void StopAll()
		{
			Log.Debug(TAG, "stopAll");

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
					ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StopRender(channel);
					ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StopReceive(channel);
					ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StopSend(channel);
					ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_RemoveRemoteRenderer(channel);
					ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_ViE_DeleteChannel(channel);
					channel = -1;
					ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StopCamera(cameraId);
					ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_Terminate();
					mLlRemoteSurface.RemoveView(remoteSurfaceView);
					mLlLocalSurface.RemoveView(svLocal);
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

			public override View GetDropDownView(int position, View convertView, ViewGroup parent)
			{
				return GetCustomView(position, convertView, parent);
			}

			public override View GetView(int position, View convertView, ViewGroup parent)
			{
				return GetCustomView(position, convertView, parent);
			}

			public virtual View GetCustomView(int position, View convertView, ViewGroup parent)
			{
				LayoutInflater inflater = outerInstance.LayoutInflater;
				View row = inflater.Inflate(Resource.Layout.row, parent, false);
				TextView label = (TextView) row.FindViewById(Resource.Id.spinner_row);
				label.Text = mCodecString[position];
				return row;
			}
		}

		private void startMain()
		{
			mTabHost.CurrentTab = 0;

			mLlRemoteSurface = (LinearLayout) FindViewById(Resource.Id.llRemoteView);
			mLlLocalSurface = (LinearLayout) FindViewById(Resource.Id.llLocalView);

			if (null == vieAndroidAPI)
			{
				vieAndroidAPI = new ViEAndroidJavaAPI(this);
			}
			if (0 > setupVoE() || 0 > ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_GetVideoEngine() || 0 > ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_Init(enableTrace))
			{
				// Show dialog
				AlertDialog alertDialog = (new AlertDialog.Builder(this)).Create();
				alertDialog.SetTitle("WebRTC Error");
				alertDialog.SetMessage("Can not init video engine.");
				alertDialog.SetButton((int)DialogInterface.ButtonPositive, "OK", new OnClickListenerAnonymousInnerClassHelper(this));
				alertDialog.Show();
			}

			btSwitchCamera = (Button) FindViewById(Resource.Id.btSwitchCamera);
			if (cameraOrientations[0] != -1 && cameraOrientations[1] != -1)
			{
				btSwitchCamera.SetOnClickListener(this);
			}
			else
			{
				btSwitchCamera.Enabled = false;
			}
			btStartStopCall = (Button) FindViewById(Resource.Id.btStartStopCall);
			btStartStopCall.SetOnClickListener(this);
			FindViewById(Resource.Id.btExit).SetOnClickListener(this);

			// cleaning
			remoteSurfaceView = null;
			svLocal = null;

			// Video codec
			mVideoCodecsStrings = ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_GetCodecs();
			spCodecType = (Spinner) FindViewById(Resource.Id.spCodecType);
			spCodecType.OnItemSelectedListener = this;
			spCodecType.Adapter = new SpinnerAdapter(this, this, Resource.Layout.row, mVideoCodecsStrings);
			spCodecType.SetSelection(0);

			// Video Codec size
			spCodecSize = (Spinner) FindViewById(Resource.Id.spCodecSize);
			spCodecSize.OnItemSelectedListener = this;
			spCodecSize.Adapter = new SpinnerAdapter(this, this, Resource.Layout.row, mVideoCodecsSizeStrings);
			spCodecSize.SetSelection(mVideoCodecsSizeStrings.Length - 1);

			// Voice codec
			mVoiceCodecsStrings = ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_GetCodecs();
			spVoiceCodecType = (Spinner) FindViewById(Resource.Id.spVoiceCodecType);
			spVoiceCodecType.OnItemSelectedListener = this;
			spVoiceCodecType.Adapter = new SpinnerAdapter(this, this, Resource.Layout.row, mVoiceCodecsStrings);
			spVoiceCodecType.SetSelection(0);
			// Find ISAC and use it
			for (int i = 0; i < mVoiceCodecsStrings.Length; ++i)
			{
				if (mVoiceCodecsStrings[i].Contains("ISAC"))
				{
					spVoiceCodecType.SetSelection(i);
					break;
				}
			}

			RadioGroup radioGroup = (RadioGroup) FindViewById(Resource.Id.radio_group1);
			radioGroup.ClearCheck();
			if (renderType == RenderType.OPENGL)
			{
				radioGroup.Check(Resource.Id.radio_opengl);
			}
			else if (renderType == RenderType.SURFACE)
			{
				radioGroup.Check(Resource.Id.radio_surface);
			}
			else if (renderType == RenderType.MEDIACODEC)
			{
				radioGroup.Check(Resource.Id.radio_mediacodec);
			}

			etRemoteIp = (EditText) FindViewById(Resource.Id.etRemoteIp);
			etRemoteIp.Text = remoteIp;

			cbLoopback = (CheckBox) FindViewById(Resource.Id.cbLoopback);
			cbLoopback.Checked = loopbackMode;

			cbStats = (CheckBox) FindViewById(Resource.Id.cbStats);
			cbStats.Checked = isStatsOn;

			cbVoice = (CheckBox) FindViewById(Resource.Id.cbVoice);
			cbVoice.Checked = enableVoice;

			cbVideoSend = (CheckBox) FindViewById(Resource.Id.cbVideoSend);
			cbVideoSend.Checked = enableVideoSend;
			cbVideoReceive = (CheckBox) FindViewById(Resource.Id.cbVideoReceive);
			cbVideoReceive.Checked = enableVideoReceive;

			etVTxPort = (EditText) FindViewById(Resource.Id.etVTxPort);
			etVTxPort.Text = Convert.ToString(destinationPortVideo);

			etVRxPort = (EditText) FindViewById(Resource.Id.etVRxPort);
			etVRxPort.Text = Convert.ToString(receivePortVideo);

			etATxPort = (EditText) FindViewById(Resource.Id.etATxPort);
			etATxPort.Text = Convert.ToString(destinationPortVoice);

			etARxPort = (EditText) FindViewById(Resource.Id.etARxPort);
			etARxPort.Text = Convert.ToString(receivePortVoice);

			cbEnableNack = (CheckBox) FindViewById(Resource.Id.cbNack);
			cbEnableNack.Checked = enableNack;

			cbEnableSpeaker = (CheckBox) FindViewById(Resource.Id.cbSpeaker);
			cbEnableAGC = (CheckBox) FindViewById(Resource.Id.cbAutoGainControl);
			cbEnableAGC.Checked = enableAGC;
			cbEnableAECM = (CheckBox) FindViewById(Resource.Id.cbAECM);
			cbEnableAECM.Checked = enableAECM;
			cbEnableNS = (CheckBox) FindViewById(Resource.Id.cbNoiseSuppression);
			cbEnableNS.Checked = enableNS;

			cbEnableDebugAPM = (CheckBox) FindViewById(Resource.Id.cbDebugRecording);
			cbEnableDebugAPM.Checked = false; // Disable APM debugging by default

			cbEnableVideoRTPDump = (CheckBox) FindViewById(Resource.Id.cbVideoRTPDump);
			cbEnableVideoRTPDump.Checked = false; // Disable Video RTP Dump

			cbEnableVoiceRTPDump = (CheckBox) FindViewById(Resource.Id.cbVoiceRTPDump);
			cbEnableVoiceRTPDump.Checked = false; // Disable Voice RTP Dump

			etRemoteIp.SetOnClickListener(this);
			cbLoopback.SetOnClickListener(this);
			cbStats.SetOnClickListener(this);
			cbEnableNack.SetOnClickListener(this);
			cbEnableSpeaker.SetOnClickListener(this);
			cbEnableAECM.SetOnClickListener(this);
			cbEnableAGC.SetOnClickListener(this);
			cbEnableNS.SetOnClickListener(this);
			cbEnableDebugAPM.SetOnClickListener(this);
			cbEnableVideoRTPDump.SetOnClickListener(this);
			cbEnableVoiceRTPDump.SetOnClickListener(this);

			if (loopbackMode)
			{
				remoteIp = LOOPBACK_IP;
				etRemoteIp.Text = remoteIp;
			}
			else
			{
				remoteIp = LocalIpAddress;
				etRemoteIp.Text = remoteIp;
			}

			// Read settings to refresh each configuration
			readSettings();
		}

		private class OnClickListenerAnonymousInnerClassHelper : IDialogInterfaceOnClickListener
		{
			private readonly WebRTCDemo outerInstance;

			public OnClickListenerAnonymousInnerClassHelper(WebRTCDemo outerInstance)
			{
				this.outerInstance = outerInstance;
			}

			public void OnClick(IDialogInterface dialog, int which)
			{
				return;
			}

			public void Dispose()
			{
				throw new NotImplementedException();
			}

			public IntPtr Handle { get; private set; }
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

				channel = ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_CreateChannel(voiceChannel);
				ret = ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_SetLocalReceiver(channel, receivePortVideo);
				ret = ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_SetSendDestination(channel, destinationPortVideo, RemoteIPString);

				if (enableVideoReceive)
				{
					if (renderType == RenderType.OPENGL)
					{
						Log.Verbose(TAG, "Create OpenGL Render");
						remoteSurfaceView = ViERenderer.CreateRenderer(this, true);
					}
					else if (renderType == RenderType.SURFACE)
					{
						Log.Verbose(TAG, "Create SurfaceView Render");
						remoteSurfaceView = ViERenderer.CreateRenderer(this, false);
					}
					else if (renderType == RenderType.MEDIACODEC)
					{
						Log.Verbose(TAG, "Create MediaCodec Decoder/Renderer");
						remoteSurfaceView = new SurfaceView(this);
					}

					if (mLlRemoteSurface != null)
					{
						mLlRemoteSurface.AddView(remoteSurfaceView);
					}

					if (renderType == RenderType.MEDIACODEC)
					{
						ret = ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_SetExternalMediaCodecDecoderRenderer(channel, remoteSurfaceView);
					}
					else
					{
						ret = ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_AddRemoteRenderer(channel, remoteSurfaceView);
					}

					ret = ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_SetReceiveCodec(channel, codecType, INIT_BITRATE, codecSizeWidth, codecSizeHeight, RECEIVE_CODEC_FRAMERATE);
					ret = ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StartRender(channel);
					ret = ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StartReceive(channel);
				}

				if (enableVideoSend)
				{
					ret = ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_SetSendCodec(channel, codecType, INIT_BITRATE, codecSizeWidth, codecSizeHeight, SEND_CODEC_FRAMERATE);
					int camId = ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StartCamera(channel, usingFrontCamera ? 1 : 0);

					if (camId >= 0)
					{
						cameraId = camId;
						compensateCameraRotation();
					}
					else
					{
						ret = camId;
					}
					ret = ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StartSend(channel);
				}

				// TODO(leozwang): Add more options besides PLI, currently use pli
				// as the default. Also check return value.
				ret = ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_EnablePLI(channel, true);
				ret = ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_EnableNACK(channel, enableNack);
				ret = ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_SetCallback(channel, this);

				if (enableVideoSend)
				{
					if (mLlLocalSurface != null)
					{
						mLlLocalSurface.AddView(svLocal);
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
			if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StopSend(voiceChannel))
			{
				Log.Debug(TAG, "VoE stop send failed");
			}

			// Stop listen
			if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StopListen(voiceChannel))
			{
				Log.Debug(TAG, "VoE stop listen failed");
			}

			// Stop playout
			if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StopPlayout(voiceChannel))
			{
				Log.Debug(TAG, "VoE stop playout failed");
			}

			if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_DeleteChannel(voiceChannel))
			{
				Log.Debug(TAG, "VoE delete channel failed");
			}
			voiceChannel = -1;

			// Terminate
			if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_Terminate())
			{
				Log.Debug(TAG, "VoE terminate failed");
			}
		}

		private int setupVoE()
		{
			// Create VoiceEngine
			// Error logging is done in native API wrapper
			ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_Create(ApplicationContext);

			// Initialize
			if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_Init(enableTrace))
			{
				Log.Debug(TAG, "VoE init failed");
				return -1;
			}

			// Suggest to use the voice call audio stream for hardware volume controls
			VolumeControlStream = Stream.VoiceCall;
			return 0;
		}

		private int startVoiceEngine()
		{
			// Create channel
			voiceChannel = ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_CreateChannel();
			if (0 > voiceChannel)
			{
				Log.Debug(TAG, "VoE create channel failed");
				return -1;
			}

			// Set local receiver
			if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetLocalReceiver(voiceChannel, receivePortVoice))
			{
				Log.Debug(TAG, "VoE set local receiver failed");
			}

			if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StartListen(voiceChannel))
			{
				Log.Debug(TAG, "VoE start listen failed");
			}

			// Route audio
			RouteAudio(cbEnableSpeaker.Checked);

			// set volume to default value
			if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetSpeakerVolume(volumeLevel))
			{
				Log.Debug(TAG, "VoE set speaker volume failed");
			}

			// Start playout
			if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StartPlayout(voiceChannel))
			{
				Log.Debug(TAG, "VoE start playout failed");
			}

			if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetSendDestination(voiceChannel, destinationPortVoice, RemoteIPString))
			{
				Log.Debug(TAG, "VoE set send  destination failed");
			}

			if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetSendCodec(voiceChannel, voiceCodecType))
			{
				Log.Debug(TAG, "VoE set send codec failed");
			}

			if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetECStatus(enableAECM))
			{
				Log.Debug(TAG, "VoE set EC Status failed");
			}

			if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetAGCStatus(enableAGC))
			{
				Log.Debug(TAG, "VoE set AGC Status failed");
			}

			if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetNSStatus(enableNS))
			{
				Log.Debug(TAG, "VoE set NS Status failed");
			}

			if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StartSend(voiceChannel))
			{
				Log.Debug(TAG, "VoE start send failed");
			}

			voERunning = true;
			return 0;
		}

		private void RouteAudio(bool enableSpeaker)
		{
			if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetLoudspeakerStatus(enableSpeaker))
			{
				Log.Debug(TAG, "VoE set louspeaker status failed");
			}
		}

		internal void StartOrStop()
		{
			readSettings();
			if (viERunning || voERunning)
			{
				StopAll();
				startMain();
				btStartStopCall.Text = Resources.GetString(Resource.String.startCall);
			}
			else if (enableVoice || enableVideo)
			{
				++numCalls;
				startCall();
				btStartStopCall.Text = Resources.GetString(Resource.String.stopCall);
			}
			if (AUTO_CALL_RESTART_DELAY_MS > 0)
			{
				handler.PostDelayed(startOrStopCallback, AUTO_CALL_RESTART_DELAY_MS);
			}
		}

		public virtual void OnClick(View arg0)
		{
			switch (arg0.Id)
			{
				case Resource.Id.btSwitchCamera:
					if (usingFrontCamera)
					{
						btSwitchCamera.Text = Resources.GetString(Resource.String.frontCamera);
					}
					else
					{
						btSwitchCamera.Text = Resources.GetString(Resource.String.backCamera);
					}
					usingFrontCamera = !usingFrontCamera;

					if (viERunning)
					{
						ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StopCamera(cameraId);
						mLlLocalSurface.RemoveView(svLocal);

						ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StartCamera(channel, usingFrontCamera ? 1 : 0);
						mLlLocalSurface.AddView(svLocal);
						compensateCameraRotation();
					}
					break;
				case Resource.Id.btStartStopCall:
				  StartOrStop();
				  break;
				case Resource.Id.btExit:
					StopAll();
					Finish();
					break;
				case Resource.Id.cbLoopback:
					loopbackMode = cbLoopback.Checked;
					if (loopbackMode)
					{
						remoteIp = LOOPBACK_IP;
						etRemoteIp.Text = LOOPBACK_IP;
					}
					else
					{
						remoteIp = LocalIpAddress;
						etRemoteIp.Text = remoteIp;
					}
					break;
				case Resource.Id.etRemoteIp:
					remoteIp = etRemoteIp.Text.ToString();
					break;
				case Resource.Id.cbStats:
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
				case Resource.Id.radio_surface:
					renderType = RenderType.SURFACE;
					break;
				case Resource.Id.radio_opengl:
					renderType = RenderType.OPENGL;
					break;
				case Resource.Id.radio_mediacodec:
					renderType = RenderType.MEDIACODEC;
					break;
				case Resource.Id.cbNack:
					enableNack = cbEnableNack.Checked;
					if (viERunning)
					{
						ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_EnableNACK(channel, enableNack);
					}
					break;
				case Resource.Id.cbSpeaker:
					if (voERunning)
					{
						RouteAudio(cbEnableSpeaker.Checked);
					}
					break;
				case Resource.Id.cbDebugRecording:
					if (voERunning && webrtcDebugDir != null)
					{
						if (cbEnableDebugAPM.Checked)
						{
							ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StartDebugRecording(webrtcDebugDir + string.Format("/apm_{0:D}.dat", DateTimeHelperClass.CurrentUnixTimeMillis()));
						}
						else
						{
							ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StopDebugRecording();
						}
					}
					break;
				case Resource.Id.cbVoiceRTPDump:
					if (voERunning && webrtcDebugDir != null)
					{
						if (cbEnableVoiceRTPDump.Checked)
						{
							ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StartIncomingRTPDump(channel, webrtcDebugDir + string.Format("/voe_{0:D}.rtp", DateTimeHelperClass.CurrentUnixTimeMillis()));
						}
						else
						{
							ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StopIncomingRTPDump(channel);
						}
					}
					break;
				case Resource.Id.cbVideoRTPDump:
					if (viERunning && webrtcDebugDir != null)
					{
						if (cbEnableVideoRTPDump.Checked)
						{
							ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StartIncomingRTPDump(channel, webrtcDebugDir + string.Format("/vie_{0:D}.rtp", DateTimeHelperClass.CurrentUnixTimeMillis()));
						}
						else
						{
							ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StopIncomingRTPDump(channel);
						}
					}
					break;
				case Resource.Id.cbAutoGainControl:
					enableAGC = cbEnableAGC.Checked;
					if (voERunning)
					{
						ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetAGCStatus(enableAGC);
					}
					break;
				case Resource.Id.cbNoiseSuppression:
					enableNS = cbEnableNS.Checked;
					if (voERunning)
					{
						ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetNSStatus(enableNS);
					}
					break;
				case Resource.Id.cbAECM:
					enableAECM = cbEnableAECM.Checked;
					if (voERunning)
					{
						ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetECStatus(enableAECM);
					}
					break;
			}
		}

		private void readSettings()
		{
			codecType = spCodecType.SelectedItemPosition;
			voiceCodecType = spVoiceCodecType.SelectedItemPosition;

			string sCodecSize = spCodecSize.SelectedItem.ToString();
			string[] aCodecSize = sCodecSize.Split(new[]{'x'}, 1);
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

		public virtual void OnItemSelected(AdapterView adapterView, View view, int position, long id)
		{
			if ((adapterView == spCodecType || adapterView == spCodecSize) && viERunning)
			{
				readSettings();
				// change the codectype
				if (enableVideoReceive)
				{
					if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_SetReceiveCodec(channel, codecType, INIT_BITRATE, codecSizeWidth, codecSizeHeight, RECEIVE_CODEC_FRAMERATE))
					{
						Log.Debug(TAG, "ViE set receive codec failed");
					}
				}
				if (enableVideoSend)
				{
					if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_SetSendCodec(channel, codecType, INIT_BITRATE, codecSizeWidth, codecSizeHeight, SEND_CODEC_FRAMERATE))
					{
						Log.Debug(TAG, "ViE set send codec failed");
					}
				}
			}
			else if ((adapterView == spVoiceCodecType) && voERunning)
			{
				// change voice engine codec
				readSettings();
				if (0 != ViEAndroidJavaAPI.Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetSendCodec(voiceChannel, voiceCodecType))
				{
					Log.Debug(TAG, "VoE set send codec failed");
				}
			}
		}

		public virtual void OnNothingSelected(AdapterView arg0)
		{
			Log.Debug(TAG, "No setting selected");
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
			WindowManagerLayoutParams layoutParams = new WindowManagerLayoutParams(WindowManagerLayoutParams.MatchParent, WindowManagerLayoutParams.WrapContent, WindowManagerTypes.SystemOverlay, WindowManagerFlags.NotFocusable | WindowManagerFlags.NotTouchable, Format.Translucent);
			layoutParams.Gravity = GravityFlags.Right| GravityFlags.Top;
			layoutParams.Title = "Load Average";
			mTabHost.AddView(statsView, layoutParams);
			statsView.SetBackgroundColor(Color.Black);
		}

		private void removeStatusView()
		{
			mTabHost.RemoveView(statsView);
			statsView = null;
		}

	}

}