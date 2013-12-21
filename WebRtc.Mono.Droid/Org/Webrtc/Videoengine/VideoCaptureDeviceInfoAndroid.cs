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
using Android.Hardware;
using Android.Util;
using Org.Json;

namespace WebRtc.Org.Webrtc.Videoengine
{
	public class VideoCaptureDeviceInfoAndroid
	{
	  private const string TAG = "WEBRTC-JC";

	  private static bool isFrontFacing(Camera.CameraInfo info)
	  {
		return info.Facing == CameraFacing.Front;
	  }

	  private static string deviceUniqueName(int index, Camera.CameraInfo info)
	  {
		return "Camera " + index + ", Facing " + (isFrontFacing(info) ? "front" : "back") + ", Orientation " + info.Orientation;
	  }

	  // Returns information about all cameras on the device as a serialized JSON
	  // array of dictionaries encoding information about a single device.  Since
	  // this reflects static information about the hardware present, there is no
	  // need to call this function more than once in a single process.  It is
	  // marked "private" as it is only called by native code.
	  private static string DeviceInfo
	  {
		  get
		  {
			try
			{
			  JSONArray devices = new JSONArray();
			  for (int i = 0; i < Camera.NumberOfCameras; ++i)
			  {
				Camera.CameraInfo info = new Camera.CameraInfo();
				Camera.GetCameraInfo(i, info);
				string uniqueName = deviceUniqueName(i, info);
				JSONObject cameraDict = new JSONObject();
				devices.Put(cameraDict);
				IList<Camera.Size> supportedSizes;
				IList<int[]> supportedFpsRanges;
				try
				{
				  Camera camera = Camera.Open(i);
				  Camera.Parameters parameters = camera.GetParameters();
				  supportedSizes = parameters.SupportedPreviewSizes;
				  supportedFpsRanges = parameters.SupportedPreviewFpsRange;
				  camera.Release();
				  Log.Debug(TAG, uniqueName);
				}
				catch (Exception e)
				{
				  Log.Error(TAG, "Failed to open " + uniqueName + ", skipping");
				  continue;
				}
				JSONArray sizes = new JSONArray();
				foreach (Camera.Size supportedSize in supportedSizes)
				{
				  JSONObject size = new JSONObject();
				  size.Put("width", supportedSize.Width);
				  size.Put("height", supportedSize.Height);
				  sizes.Put(size);
				}
				// Android SDK deals in integral "milliframes per second"
				// (i.e. fps*1000, instead of floating-point frames-per-second) so we
				// preserve that through the Java->C++->Java round-trip.
				int[] mfps = supportedFpsRanges[supportedFpsRanges.Count - 1];
				cameraDict.Put("name", uniqueName);
				cameraDict.Put("front_facing", isFrontFacing(info)).Put("orientation", info.Orientation).Put("sizes", sizes).Put("min_mfps", mfps[(int)Camera.Parameters.PreviewFpsMinIndex]).Put("max_mfps", mfps[(int)Camera.Parameters.PreviewFpsMaxIndex]);
			  }
			  string ret = devices.ToString(2);
			  return ret;
			}
			catch (JSONException e)
			{
			  throw new Exception("Error", e);
			}
		  }
	  }
	}

}