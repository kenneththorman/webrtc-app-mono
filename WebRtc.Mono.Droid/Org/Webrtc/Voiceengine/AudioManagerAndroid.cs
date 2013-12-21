/*
 *  Copyright (c) 2013 The WebRTC project authors. All Rights Reserved.
 *
 *  Use of this source code is governed by a BSD-style license
 *  that can be found in the LICENSE file in the root of the source
 *  tree. An additional intellectual property rights grant can be found
 *  in the file PATENTS.  All contributing project authors may
 *  be found in the AUTHORS file in the root of the source tree.
 */

// The functions in this file are called from native code. They can still be
// accessed even though they are declared private.
using System;
using Android.Content;
using Android.Content.PM;
using Android.Media;

namespace WebRtc.Org.Webrtc.Voiceengine
{
	internal class AudioManagerAndroid
	{
	  // Most of Google lead devices use 44.1K as the default sampling rate, 44.1K
	  // is also widely used on other android devices.
	  private const int DEFAULT_SAMPLING_RATE = 44100;
	  // Randomly picked up frame size which is close to return value on N4.
	  // Return this default value when
	  // getProperty(PROPERTY_OUTPUT_FRAMES_PER_BUFFER) fails.
	  private const int DEFAULT_FRAMES_PER_BUFFER = 256;

	  private int mNativeOutputSampleRate;
	  private bool mAudioLowLatencySupported;
	  private int mAudioLowLatencyOutputFrameSize;


	  private AudioManagerAndroid(Context context)
	  {
		AudioManager audioManager = (AudioManager) context.GetSystemService(Context.AudioService);

		mNativeOutputSampleRate = DEFAULT_SAMPLING_RATE;
		mAudioLowLatencyOutputFrameSize = DEFAULT_FRAMES_PER_BUFFER;
		if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.JellyBeanMr1)
		{
		  string sampleRateString = audioManager.GetProperty(AudioManager.PropertyOutputSampleRate);
		  if (sampleRateString != null)
		  {
			mNativeOutputSampleRate = Convert.ToInt32(sampleRateString);
		  }
		  string framesPerBuffer = audioManager.GetProperty(AudioManager.PropertyOutputFramesPerBuffer);
		  if (framesPerBuffer != null)
		  {
			  mAudioLowLatencyOutputFrameSize = Convert.ToInt32(framesPerBuffer);
		  }
		}
		mAudioLowLatencySupported = context.PackageManager.HasSystemFeature(PackageManager.FeatureAudioLowLatency);
	  }

		private int NativeOutputSampleRate
		{
			get
			{
			  return mNativeOutputSampleRate;
			}
		}

		private bool AudioLowLatencySupported
		{
			get
			{
				return mAudioLowLatencySupported;
			}
		}

		private int AudioLowLatencyOutputFrameSize
		{
			get
			{
				return mAudioLowLatencyOutputFrameSize;
			}
		}
	}
}