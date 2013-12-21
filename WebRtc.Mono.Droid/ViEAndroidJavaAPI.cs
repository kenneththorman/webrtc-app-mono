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
using System.Runtime.InteropServices;
using Android.Content;
using Android.Util;
using Encoding = System.Text.Encoding;

namespace WebRtc
{
	public class ViEAndroidJavaAPI
	{

		public ViEAndroidJavaAPI(Context context)
		{
			Log.Debug("*WEBRTCJ*", "Loading ViEAndroidJavaAPI...");
			Log.Debug("*WEBRTCJ*", "Calling native init...");
			if (!NativeInit(context))
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
		private extern bool NativeInit(Context context);

		// Video Engine API
		// Initialization and Termination functions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int GetVideoEngine();

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int Init(bool enableTrace);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int Terminate();


		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int StartSend(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int StopRender(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int StopSend(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int StartReceive(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int StopReceive(int channel);
		
		// Channel functions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int CreateChannel(int voiceChannel);
		
		// Receiver & Destination functions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int SetLocalReceiver(int channel, int port);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int SetSendDestination(int channel, int port, string ipaddr);
		
		// Codec
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern String[] GetCodecs();

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int SetReceiveCodec(int channel, int codecNum, int intbitRate, int width, int height, int frameRate);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int SetSendCodec(int channel, int codecNum, int intbitRate, int width, int height, int frameRate);
		
		// Rendering
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int AddRemoteRenderer(int channel, object glSurface);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int RemoveRemoteRenderer(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int StartRender(int channel);

		// Capture
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int StartCamera(int channel, int cameraNum);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int StopCamera(int cameraId);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int GetCameraOrientation(int cameraNum);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int SetRotation(int cameraId, int degrees);

		// External Codec
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int SetExternalMediaCodecDecoderRenderer(int channel, object glSurface);

		// NACK
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int EnableNACK(int channel, bool enable);

		// PLI
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int EnablePLI(int channel, bool enable);

		// Enable stats callback
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int SetCallback(int channel, IViEAndroidCallback callback);


		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int StartIncomingRTPDump(int channel, string file);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int StopIncomingRTPDump(int channel);

		// Voice Engine API
		// Create and Delete functions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern bool VoE_Create(Context context);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern bool VoE_Delete();

		// Initialization and Termination functions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_Init(bool enableTrace);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_Terminate();

		// Channel functions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_CreateChannel();

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_DeleteChannel(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int ViE_DeleteChannel(int channel);

		// Receiver & Destination functions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_SetLocalReceiver(int channel, int port);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_SetSendDestination(int channel, int port, string ipaddr);

		// Media functions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_StartListen(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_StartPlayout(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_StartSend(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_StopListen(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_StopPlayout(int channel);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_StopSend(int channel);

		// Volume
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_SetSpeakerVolume(int volume);

		// Hardware
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_SetLoudspeakerStatus(bool enable);

		// Playout file locally
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_StartPlayingFileLocally(int channel, string fileName, bool loop);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_StopPlayingFileLocally(int channel);

		// Play file as microphone
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_StartPlayingFileAsMicrophone(int channel, string fileName, bool loop);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_StopPlayingFileAsMicrophone(int channel);

		// Codec-setting functions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_NumOfCodecs();

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern String[] VoE_GetCodecs();

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_SetSendCodec(int channel, int index);

		//VoiceEngine funtions
		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_SetECStatus(bool enable);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_SetAGCStatus(bool enable);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_SetNSStatus(bool enable);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_StartDebugRecording(string file);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_StopDebugRecording();

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_StartIncomingRTPDump(int channel, string file);

		[DllImport("libwebrtc-video-demo-jni.so")]
		public extern int VoE_StopIncomingRTPDump(int channel);
	}

}