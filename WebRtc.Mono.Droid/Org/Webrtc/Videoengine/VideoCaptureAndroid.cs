using System;
using System.Runtime.InteropServices;
/*
 *  Copyright (c) 2012 The WebRTC project authors. All Rights Reserved.
 *
 *  Use of this source code is governed by a BSD-style license
 *  that can be found in the LICENSE file in the root of the source
 *  tree. An additional intellectual property rights grant can be found
 *  in the file PATENTS.  All contributing project authors may
 *  be found in the AUTHORS file in the root of the source tree.
 */
using Android.Graphics;
using Android.Util;
using Android.Views;
using Java.IO;
using Java.Lang;
using Camera = Android.Hardware.Camera;
using Exception = Java.Lang.Exception;

namespace WebRtc.Org.Webrtc.Videoengine
{
	// Wrapper for android Camera, with support for direct local preview rendering.
	// Threading notes: this class is called from ViE C++ code, and from Camera &
	// SurfaceHolder Java callbacks.  Since these calls happen on different threads,
	// the entry points to this class are all synchronized.  This shouldn't present
	// a performance bottleneck because only onPreviewFrame() is called more than
	// once (and is called serially on a single thread), so the lock should be
	// uncontended.
	public class VideoCaptureAndroid : Java.Lang.Object, Camera.IPreviewCallback, ISurfaceHolderCallback
	{
	  private const string TAG = "WEBRTC-JC";

	  private Camera camera; // Only non-null while capturing.
	  private readonly int id;
	  private readonly Camera.CameraInfo info;
	  private readonly long native_capturer; // |VideoCaptureAndroid*| in C++.
	  private ISurfaceHolder localPreview;
	  private SurfaceTexture dummySurfaceTexture;
	  // Arbitrary queue depth.  Higher number means more memory allocated & held,
	  // lower number means more sensitivity to processing time in the client (and
	  // potentially stalling the capturer if it runs out of buffers to write to).
	  private readonly int numCaptureBuffers = 3;

	  public VideoCaptureAndroid(int id, long native_capturer)
	  {
		this.id = id;
		this.native_capturer = native_capturer;
		this.info = new Camera.CameraInfo();
		Camera.GetCameraInfo(id, info);
	  }

	  // Called by native code.  Returns true if capturer is started.
	  //
	  // Note that this actually opens the camera, which can be a slow operation and
	  // thus might be done on a background thread, but ViE API needs a
	  // synchronous success return value so we can't do that.
	  private bool startCapture(int width, int height, int min_mfps, int max_mfps)
	  {
		  lock (this)
		  {
			Log.Debug(TAG, "startCapture: " + width + "x" + height + "@" + min_mfps + ":" + max_mfps);
			Exception error = null;
			try
			{
			  camera = Camera.Open(id);
        
			  localPreview = ViERenderer.GetLocalRenderer();
			  if (localPreview != null)
			  {
				localPreview.AddCallback(this);
				if (localPreview.Surface != null && localPreview.Surface.IsValid)
				{
				  camera.SetPreviewDisplay(localPreview);
				}
			  }
			  else
			  {
				// No local renderer (we only care about onPreviewFrame() buffers, not a
				// directly-displayed UI element).  Camera won't capture without
				// setPreview{Texture,Display}, so we create a dummy SurfaceTexture and
				// hand it over to Camera, but never listen for frame-ready callbacks,
				// and never call updateTexImage on it.
				try
				{
				  // "42" because http://goo.gl/KaEn8
				  dummySurfaceTexture = new SurfaceTexture(42);
				  camera.SetPreviewTexture(dummySurfaceTexture);
				}
				catch (IOException e)
				{
				  throw new Exception("Error",e);
				}
			  }
        
			  Camera.Parameters parameters = camera.GetParameters();
			  Log.Debug(TAG, "isVideoStabilizationSupported: " + parameters.IsVideoStabilizationSupported);
			  if (parameters.IsVideoStabilizationSupported)
			  {
				parameters.VideoStabilization = true;
			  }
			  parameters.SetPreviewSize(width, height);
			  parameters.SetPreviewFpsRange(min_mfps, max_mfps);
			  var format = ImageFormatType.Nv21;
			  parameters.PreviewFormat = format;
			  camera.SetParameters(parameters);
			  int bufSize = width * height * ImageFormat.GetBitsPerPixel(format) / 8;
			  for (int i = 0; i < numCaptureBuffers; i++)
			  {
				camera.AddCallbackBuffer(new byte[bufSize]);
			  }
			  camera.SetPreviewCallbackWithBuffer(this);
			  camera.StartPreview();
			  return true;
			}
			catch (IOException e)
			{
			  error = e;
			}
			catch (Exception e)
			{
			  error = e;
			}
			Log.Error(TAG, "startCapture failed", error);
			if (camera != null)
			{
			  stopCapture();
			}
			return false;
		  }
	  }

	  // Called by native code.  Returns true when camera is known to be stopped.
	  private bool stopCapture()
	  {
		  lock (this)
		  {
			Log.Debug(TAG, "stopCapture");
			if (camera == null)
			{
			  throw new Exception("Camera is already stopped!");
			}
			Exception error = null;
			try
			{
			  if (localPreview != null)
			  {
				localPreview.RemoveCallback(this);
				camera.SetPreviewDisplay(null);
			  }
			  else
			  {
				camera.SetPreviewTexture(null);
			  }
			  camera.SetPreviewCallbackWithBuffer(null);
			  camera.StopPreview();
			  camera.Release();
			  camera = null;
			  return true;
			}
			catch (IOException e)
			{
			  error = e;
			}
			catch (Exception e)
			{
			  error = e;
			}
			Log.Error(TAG, "Failed to stop camera", error);
			return false;
		  }
	  }

//JAVA TO C# CONVERTER TODO TASK: Replace 'unknown' with the appropriate dll name:
	  [DllImport("unknown")]
	  private static extern void ProvideCameraFrame(byte[] data, int length, long captureObject);

	  public virtual void OnPreviewFrame(byte[] data, Camera camera)
	  {
		  lock (this)
		  {
			ProvideCameraFrame(data, data.Length, native_capturer);
			camera.AddCallbackBuffer(data);
		  }
	  }

	  // Sets the rotation of the preview render window.
	  // Does not affect the captured video image.
	  // Called by native code.
	  private int PreviewRotation
	  {
		  set
		  {
			  lock (this)
			  {
				Log.Verbose(TAG, "setPreviewRotation:" + value);
            
				if (camera == null)
				{
				  return;
				}
            
				int resultRotation = 0;
				if (info.Facing == Camera.CameraInfo.CameraFacingFront)
				{
				  // This is a front facing camera.  SetDisplayOrientation will flip
				  // the image horizontally before doing the value.
				  resultRotation = (360 - value) % 360; // Compensate for the mirror.
				}
				else
				{
				  // Back-facing camera.
				  resultRotation = value;
				}
				camera.SetDisplayOrientation(resultRotation);
			  }
		  }
	  }

	  public virtual void SurfaceChanged(ISurfaceHolder holder, Format format, int width, int height)
	  {
		  lock (this)
		  {
			Log.Debug(TAG, "VideoCaptureAndroid::surfaceChanged ignored: " + format + ": " + width + "x" + height);
		  }
	  }

	  public virtual void SurfaceCreated(ISurfaceHolder holder)
	  {
		  lock (this)
		  {
			Log.Debug(TAG, "VideoCaptureAndroid::surfaceCreated");
			try
			{
			  if (camera != null)
			  {
				camera.SetPreviewDisplay(holder);
			  }
			}
			catch (IOException e)
			{
			  throw new Exception("Error", e);
			}
		  }
	  }

	  public virtual void SurfaceDestroyed(ISurfaceHolder holder)
	  {
		  lock (this)
		  {
			Log.Debug(TAG, "VideoCaptureAndroid::surfaceDestroyed");
			try
			{
			  if (camera != null)
			  {
				camera.SetPreviewDisplay(null);
			  }
			}
			catch (IOException e)
			{
			  throw new Exception("Error",e);
			}
		  }
	  }
	}

}