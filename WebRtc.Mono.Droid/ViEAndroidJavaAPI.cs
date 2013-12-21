/*
 *  Copyright (c) 2012 The WebRTC project authors. All Rights Reserved.
 *
 *  Use of this source code is governed by a BSD-style license
 *  that can be found in the LICENSE file in the root of the source
 *  tree. An additional intellectual property rights grant can be found
 *  in the file PATENTS.  All contributing project authors may
 *  be found in the AUTHORS file in the root of the source tree.
 */

using System.Runtime.InteropServices;
using Android.Content;
using Android.Util;
using Java.Lang;
using Encoding = System.Text.Encoding;
using Exception = System.Exception;
using String = System.String;

namespace WebRtc
{
	public class ViEAndroidJavaAPI
	{

		public ViEAndroidJavaAPI(Context context)
		{
			Log.Debug("*WEBRTCJ*", "Loading ViEAndroidJavaAPI...");
			Log.Debug("*WEBRTCJ*", "Calling native init...");
			JavaSystem.LoadLibrary("webrtc-video-demo-jni");

			if (!Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_NativeInit(context))
			{
				Log.Error("*WEBRTCJ*", "Native init failed");
				throw new Exception("Native init failed");
			}
			else
			{
				Log.Debug("*WEBRTCJ*", "Native init successful");
			}
			string a = "";
			Encoding.Default.GetBytes(a);
		}

		// API Native

		[DllImport("libwebrtc-video-demo-jni.so")]
		private static extern bool Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_NativeInit(Context context);

		// Video Engine API
		// Initialization and Termination functions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_GetVideoEngine();

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_Init(bool enableTrace);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_Terminate();


		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StartSend(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StopRender(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StopSend(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StartReceive(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StopReceive(int channel);
		
		// Channel functions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_CreateChannel(int voiceChannel);
		
		// Receiver & Destination functions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_SetLocalReceiver(int channel, int port);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_SetSendDestination(int channel, int port, string ipaddr);
		
		// Codec
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern String[] Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_GetCodecs();

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_SetReceiveCodec(int channel, int codecNum, int intbitRate, int width, int height, int frameRate);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_SetSendCodec(int channel, int codecNum, int intbitRate, int width, int height, int frameRate);
		
		// Rendering
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_AddRemoteRenderer(int channel, object glSurface);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_RemoveRemoteRenderer(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StartRender(int channel);

		// Capture
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StartCamera(int channel, int cameraNum);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StopCamera(int cameraId);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_GetCameraOrientation(int cameraNum);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_SetRotation(int cameraId, int degrees);

		// External Codec
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_SetExternalMediaCodecDecoderRenderer(int channel, object glSurface);

		// NACK
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_EnableNACK(int channel, bool enable);

		// PLI
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_EnablePLI(int channel, bool enable);

		// Enable stats callback
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_SetCallback(int channel, IViEAndroidCallback callback);


		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StartIncomingRTPDump(int channel, string file);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_StopIncomingRTPDump(int channel);

		// Voice Engine API
		// Create and Delete functions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern bool Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_Create(Context context);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern bool Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_Delete();

		// Initialization and Termination functions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_Init(bool enableTrace);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_Terminate();

		// Channel functions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_CreateChannel();

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_DeleteChannel(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_ViE_DeleteChannel(int channel);

		// Receiver & Destination functions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetLocalReceiver(int channel, int port);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetSendDestination(int channel, int port, string ipaddr);

		// Media functions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StartListen(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StartPlayout(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StartSend(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StopListen(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StopPlayout(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StopSend(int channel);

		// Volume
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetSpeakerVolume(int volume);

		// Hardware
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetLoudspeakerStatus(bool enable);

		// Playout file locally
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StartPlayingFileLocally(int channel, string fileName, bool loop);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StopPlayingFileLocally(int channel);

		// Play file as microphone
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StartPlayingFileAsMicrophone(int channel, string fileName, bool loop);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StopPlayingFileAsMicrophone(int channel);

		// Codec-setting functions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_NumOfCodecs();

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern String[] Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_GetCodecs();

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetSendCodec(int channel, int index);

		//VoiceEngine funtions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetECStatus(bool enable);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetAGCStatus(bool enable);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_SetNSStatus(bool enable);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StartDebugRecording(string file);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StopDebugRecording();

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StartIncomingRTPDump(int channel, string file);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public static extern int Java_org_webrtc_videoengineapp_ViEAndroidJavaAPI_VoE_StopIncomingRTPDump(int channel);
	}

}