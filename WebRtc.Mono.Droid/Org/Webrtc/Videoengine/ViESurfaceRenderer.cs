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
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Java.IO;
using Java.Nio;

namespace WebRtc.Org.Webrtc.Videoengine
{

	// The following four imports are needed saveBitmapToJPEG which
	// is for debug only
	
	public class ViESurfaceRenderer : ISurfaceHolderCallback
	{

		private const string TAG = "WEBRTC";

		// the bitmap used for drawing.
		private Bitmap bitmap = null;
		private ByteBuffer byteBuffer = null;
		private ISurfaceHolder surfaceHolder;
		// Rect of the source bitmap to draw
		private Rect srcRect = new Rect();
		// Rect of the destination canvas to draw to
		private Rect dstRect = new Rect();
		private float dstTopScale = 0;
		private float dstBottomScale = 1;
		private float dstLeftScale = 0;
		private float dstRightScale = 1;

		public ViESurfaceRenderer(SurfaceView view)
		{
			surfaceHolder = view.Holder;
			if (surfaceHolder == null)
			{
				return;
			}
			surfaceHolder.AddCallback(this);
		}

		// surfaceChanged and surfaceCreated share this function
		private void changeDestRect(int dstWidth, int dstHeight)
		{
			dstRect.Right = (int)(dstRect.Left + dstRightScale * dstWidth);
			dstRect.Bottom = (int)(dstRect.Top + dstBottomScale * dstHeight);
		}

		public virtual void SurfaceChanged(ISurfaceHolder holder, Format format, int in_width, int in_height)
		{
			Log.Debug(TAG, "ViESurfaceRender::surfaceChanged");

			changeDestRect(in_width, in_height);

			Log.Debug(TAG, "ViESurfaceRender::surfaceChanged" + " in_width:" + in_width + " in_height:" + in_height + " srcRect.Left:" + srcRect.Left + " srcRect.Top:" + srcRect.Top + " srcRect.Right:" + srcRect.Right + " srcRect.Bottom:" + srcRect.Bottom + " dstRect.Left:" + dstRect.Left + " dstRect.Top:" + dstRect.Top + " dstRect.Right:" + dstRect.Right + " dstRect.Bottom:" + dstRect.Bottom);
		}

		public virtual void SurfaceCreated(ISurfaceHolder holder)
		{
			Canvas canvas = surfaceHolder.LockCanvas();
			if (canvas != null)
			{
				Rect dst = surfaceHolder.SurfaceFrame;
				if (dst != null)
				{
					changeDestRect(dst.Right - dst.Left, dst.Bottom - dst.Top);
					Log.Debug(TAG, "ViESurfaceRender::surfaceCreated" + " dst.Left:" + dst.Left + " dst.Top:" + dst.Top + " dst.Right:" + dst.Right + " dst.Bottom:" + dst.Bottom + " srcRect.Left:" + srcRect.Left + " srcRect.Top:" + srcRect.Top + " srcRect.Right:" + srcRect.Right + " srcRect.Bottom:" + srcRect.Bottom + " dstRect.Left:" + dstRect.Left + " dstRect.Top:" + dstRect.Top + " dstRect.Right:" + dstRect.Right + " dstRect.Bottom:" + dstRect.Bottom);
				}
				surfaceHolder.UnlockCanvasAndPost(canvas);
			}
		}

		public virtual void SurfaceDestroyed(ISurfaceHolder holder)
		{
			Log.Debug(TAG, "ViESurfaceRenderer::surfaceDestroyed");
			bitmap = null;
			byteBuffer = null;
		}

		public virtual Bitmap CreateBitmap(int width, int height)
		{
			Log.Debug(TAG, "CreateByteBitmap " + width + ":" + height);
			if (bitmap == null)
			{
				try
				{
					Process.SetThreadPriority(ThreadPriority.Display);
				}
				catch (Exception)
				{
				}
			}
			bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Rgb565);
			srcRect.Left = 0;
			srcRect.Top = 0;
			srcRect.Bottom = height;
			srcRect.Right = width;
			return bitmap;
		}

		public virtual ByteBuffer CreateByteBuffer(int width, int height)
		{
			Log.Debug(TAG, "CreateByteBuffer " + width + ":" + height);
			if (bitmap == null)
			{
				bitmap = CreateBitmap(width, height);
				byteBuffer = ByteBuffer.AllocateDirect(width * height * 2);
			}
			return byteBuffer;
		}

		public virtual void SetCoordinates(float left, float top, float right, float bottom)
		{
			Log.Debug(TAG, "SetCoordinates " + left + "," + top + ":" + right + "," + bottom);
			dstLeftScale = left;
			dstTopScale = top;
			dstRightScale = right;
			dstBottomScale = bottom;
		}

		public virtual void DrawByteBuffer()
		{
			if (byteBuffer == null)
			{
				return;
			}
			byteBuffer.Rewind();
			bitmap.CopyPixelsFromBuffer(byteBuffer);
			DrawBitmap();
		}

		public virtual void DrawBitmap()
		{
			if (bitmap == null)
			{
				return;
			}

			Canvas canvas = surfaceHolder.LockCanvas();
			if (canvas != null)
			{
				// The follow line is for debug only
				// saveBitmapToJPEG(srcRect.Right - srcRect.Left,
				//                  srcRect.Bottom - srcRect.Top);
				canvas.DrawBitmap(bitmap, srcRect, dstRect, null);
				surfaceHolder.UnlockCanvasAndPost(canvas);
			}
		}

		public void Dispose()
		{
			throw new NotImplementedException();
		}

		public IntPtr Handle { get; private set; }
	}

}