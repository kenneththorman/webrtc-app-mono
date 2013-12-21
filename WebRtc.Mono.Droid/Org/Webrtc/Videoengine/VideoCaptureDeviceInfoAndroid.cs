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

namespace org.webrtc.videoengine
{


	using Context = android.content.Context;
	using CameraInfo = android.hardware.Camera.CameraInfo;
	using Parameters = android.hardware.Camera.Parameters;
	using Size = android.hardware.Camera.Size;
	using Camera = android.hardware.Camera;
	using Log = android.util.Log;

	using JSONArray = org.json.JSONArray;
	using JSONException = org.json.JSONException;
	using JSONObject = org.json.JSONObject;

	public class VideoCaptureDeviceInfoAndroid
	{
	  private const string TAG = "WEBRTC-JC";

	  private static bool isFrontFacing(CameraInfo info)
	  {
		return info.facing == CameraInfo.CAMERA_FACING_FRONT;
	  }

	  private static string deviceUniqueName(int index, CameraInfo info)
	  {
		return "Camera " + index + ", Facing " + (isFrontFacing(info) ? "front" : "back") + ", Orientation " + info.orientation;
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
				CameraInfo info = new CameraInfo();
				Camera.getCameraInfo(i, info);
				string uniqueName = deviceUniqueName(i, info);
				JSONObject cameraDict = new JSONObject();
				devices.put(cameraDict);
				IList<Size> supportedSizes;
				IList<int[]> supportedFpsRanges;
				try
				{
				  Camera camera = Camera.open(i);
				  Parameters parameters = camera.Parameters;
				  supportedSizes = parameters.SupportedPreviewSizes;
				  supportedFpsRanges = parameters.SupportedPreviewFpsRange;
				  camera.release();
				  Log.d(TAG, uniqueName);
				}
				catch (Exception e)
				{
				  Log.e(TAG, "Failed to open " + uniqueName + ", skipping");
				  continue;
				}
				JSONArray sizes = new JSONArray();
				foreach (Size supportedSize in supportedSizes)
				{
				  JSONObject size = new JSONObject();
				  size.put("width", supportedSize.width);
				  size.put("height", supportedSize.height);
				  sizes.put(size);
				}
				// Android SDK deals in integral "milliframes per second"
				// (i.e. fps*1000, instead of floating-point frames-per-second) so we
				// preserve that through the Java->C++->Java round-trip.
				int[] mfps = supportedFpsRanges[supportedFpsRanges.Count - 1];
				cameraDict.put("name", uniqueName);
				cameraDict.put("front_facing", isFrontFacing(info)).put("orientation", info.orientation).put("sizes", sizes).put("min_mfps", mfps[Parameters.PREVIEW_FPS_MIN_INDEX]).put("max_mfps", mfps[Parameters.PREVIEW_FPS_MAX_INDEX]);
			  }
			  string ret = devices.ToString(2);
			  return ret;
			}
			catch (JSONException e)
			{
			  throw new Exception(e);
			}
		  }
	  }
	}

}