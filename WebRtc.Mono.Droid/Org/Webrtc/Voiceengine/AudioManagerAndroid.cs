using System;

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

namespace org.webrtc.voiceengine
{

	using Context = android.content.Context;
	using PackageManager = android.content.pm.PackageManager;
	using AudioManager = android.media.AudioManager;

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


//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @SuppressWarnings("unused") private AudioManagerAndroid(android.content.Context context)
	  private AudioManagerAndroid(Context context)
	  {
		AudioManager audioManager = (AudioManager) context.getSystemService(Context.AUDIO_SERVICE);

		mNativeOutputSampleRate = DEFAULT_SAMPLING_RATE;
		mAudioLowLatencyOutputFrameSize = DEFAULT_FRAMES_PER_BUFFER;
		if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.JELLY_BEAN_MR1)
		{
		  string sampleRateString = audioManager.getProperty(AudioManager.PROPERTY_OUTPUT_SAMPLE_RATE);
		  if (sampleRateString != null)
		  {
			mNativeOutputSampleRate = Convert.ToInt32(sampleRateString);
		  }
		  string framesPerBuffer = audioManager.getProperty(AudioManager.PROPERTY_OUTPUT_FRAMES_PER_BUFFER);
		  if (framesPerBuffer != null)
		  {
			  mAudioLowLatencyOutputFrameSize = Convert.ToInt32(framesPerBuffer);
		  }
		}
		mAudioLowLatencySupported = context.PackageManager.hasSystemFeature(PackageManager.FEATURE_AUDIO_LOW_LATENCY);
	  }

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @SuppressWarnings("unused") private int getNativeOutputSampleRate()
		private int NativeOutputSampleRate
		{
			get
			{
			  return mNativeOutputSampleRate;
			}
		}

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @SuppressWarnings("unused") private boolean isAudioLowLatencySupported()
		private bool AudioLowLatencySupported
		{
			get
			{
				return mAudioLowLatencySupported;
			}
		}

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @SuppressWarnings("unused") private int getAudioLowLatencyOutputFrameSize()
		private int AudioLowLatencyOutputFrameSize
		{
			get
			{
				return mAudioLowLatencyOutputFrameSize;
			}
		}
	}
}