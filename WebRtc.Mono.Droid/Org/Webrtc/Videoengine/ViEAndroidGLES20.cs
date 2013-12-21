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
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Opengl;
using Android.Util;
using Java.Util.Concurrent.Locks;
using Javax.Microedition.Khronos.Egl;
using Javax.Microedition.Khronos.Opengles;
using EGLConfig = Javax.Microedition.Khronos.Egl.EGLConfig;
using EGLContext = Javax.Microedition.Khronos.Egl.EGLContext;
using EGLDisplay = Javax.Microedition.Khronos.Egl.EGLDisplay;

namespace WebRtc.Org.Webrtc.Videoengine
{
	public class ViEAndroidGLES20 : GLSurfaceView, GLSurfaceView.IRenderer
	{
		private static string TAG = "WEBRTC-JR";
		private const bool DEBUG = false;
		// True if onSurfaceCreated has been called.
		private bool surfaceCreated = false;
		private bool openGLCreated = false;
		// True if NativeFunctionsRegistered has been called.
		private bool nativeFunctionsRegisted = false;
		private ReentrantLock nativeFunctionLock = new ReentrantLock();
		// Address of Native object that will do the drawing.
		private long nativeObject = 0;
		private int viewWidth = 0;
		private int viewHeight = 0;

		public static bool UseOpenGL2(object renderWindow)
		{
			return typeof(ViEAndroidGLES20).IsInstanceOfType(renderWindow);
		}

		public ViEAndroidGLES20(Context context) : base(context)
		{
			init(false, 0, 0);
		}

		public ViEAndroidGLES20(Context context, bool translucent, int depth, int stencil) : base(context)
		{
			init(translucent, depth, stencil);
		}

		private void init(bool translucent, int depth, int stencil)
		{

			// By default, GLSurfaceView() creates a RGB_565 opaque surface.
			// If we want a translucent one, we should change the surface's
			// format here, using PixelFormat.TRANSLUCENT for GL Surfaces
			// is interpreted as any 32-bit surface with alpha by SurfaceFlinger.
			if (translucent)
			{
				Holder.SetFormat(Format.Translucent);
			}

			// Setup the context factory for 2.0 rendering.
			// See ContextFactory class definition below
			SetEGLContextFactory(new ContextFactory());

			// We need to choose an EGLConfig that matches the format of
			// our surface exactly. This is going to be done in our
			// custom config chooser. See ConfigChooser class definition
			// below.
			SetEGLConfigChooser(translucent ? 
				new ConfigChooser(8, 8, 8, 8, depth, stencil) : 
				new ConfigChooser(5, 6, 5, 0, depth, stencil));

			// Set the renderer responsible for frame rendering
			SetRenderer(this);
			RenderMode = Rendermode.WhenDirty;
		}

		private class ContextFactory : GLSurfaceView.IEGLContextFactory
		{
			internal static int EGL_CONTEXT_CLIENT_VERSION = 0x3098;
			public virtual EGLContext CreateContext(EGL10 egl, EGLDisplay display, EGLConfig eglConfig)
			{
				Log.Warn(TAG, "creating OpenGL ES 2.0 context");
				checkEglError("Before eglCreateContext", egl);
				int[] attrib_list = new int[] {EGL_CONTEXT_CLIENT_VERSION, 2, EGL10.EglNone};
				EGLContext context = egl.eglCreateContext(display, eglConfig, EGL10.EglNoContext, attrib_list);
				checkEglError("After eglCreateContext", egl);
				return context;
			}

			public virtual void DestroyContext(EGL10 egl, EGLDisplay display, EGLContext context)
			{
				egl.eglDestroyContext(display, context);
			}
		}

		private static void checkEglError(string prompt, EGL10 egl)
		{
			int error;
			while ((error = egl.eglGetError()) != EGL10.EglSuccess)
			{
				Log.Error(TAG, string.Format("{0}: EGL error: 0x{1:x}", prompt, error));
			}
		}

		private class ConfigChooser : IEGLConfigChooser
		{

			public ConfigChooser(int r, int g, int b, int a, int depth, int stencil)
			{
				mRedSize = r;
				mGreenSize = g;
				mBlueSize = b;
				mAlphaSize = a;
				mDepthSize = depth;
				mStencilSize = stencil;
			}

			// This EGL config specification is used to specify 2.0 rendering.
			// We use a minimum size of 4 bits for red/green/blue, but will
			// perform actual matching in chooseConfig() below.
			internal static int EGL_OPENGL_ES2_BIT = 4;
			internal static int[] s_configAttribs2 = new int[] {EGL10.EglRedSize, 4, EGL10.EglGreenSize, 4, EGL10.EglBlueSize, 4, EGL10.EglRenderableType, EGL_OPENGL_ES2_BIT, EGL10.EglNone};

			public virtual EGLConfig ChooseConfig(EGL10 egl, EGLDisplay display)
			{

				// Get the number of minimally matching EGL configurations
				int[] num_config = new int[1];
				egl.eglChooseConfig(display, s_configAttribs2, null, 0, num_config);

				int numConfigs = num_config[0];

				if (numConfigs <= 0)
				{
					throw new System.ArgumentException("No configs match configSpec");
				}

				// Allocate then read the array of minimally matching EGL configs
				EGLConfig[] configs = new EGLConfig[numConfigs];
				egl.eglChooseConfig(display, s_configAttribs2, configs, numConfigs, num_config);

				if (DEBUG)
				{
					PrintConfigs(egl, display, configs);
				}
				// Now return the "best" one
				return chooseConfig(egl, display, configs);
			}

			public virtual EGLConfig chooseConfig(EGL10 egl, EGLDisplay display, EGLConfig[] configs)
			{
				foreach (EGLConfig config in configs)
				{
					int d = FindConfigAttrib(egl, display, config, EGL10.EglDepthSize, 0);
					int s = FindConfigAttrib(egl, display, config, EGL10.EglStencilSize, 0);

					// We need at least mDepthSize and mStencilSize bits
					if (d < mDepthSize || s < mStencilSize)
					{
						continue;
					}

					// We want an *exact* match for red/green/blue/alpha
					int r = FindConfigAttrib(egl, display, config, EGL10.EglRedSize, 0);
					int g = FindConfigAttrib(egl, display, config, EGL10.EglGreenSize, 0);
					int b = FindConfigAttrib(egl, display, config, EGL10.EglBlueSize, 0);
					int a = FindConfigAttrib(egl, display, config, EGL10.EglAlphaSize, 0);

					if (r == mRedSize && g == mGreenSize && b == mBlueSize && a == mAlphaSize)
					{
						return config;
					}
				}
				return null;
			}

			internal virtual int FindConfigAttrib(EGL10 egl, EGLDisplay display, EGLConfig config, int attribute, int defaultValue)
			{

				if (egl.eglGetConfigAttrib(display, config, attribute, mValue))
				{
					return mValue[0];
				}
				return defaultValue;
			}

			internal virtual void PrintConfigs(EGL10 egl, EGLDisplay display, EGLConfig[] configs)
			{
				int numConfigs = configs.Length;
				Log.Warn(TAG, string.Format("{0:D} configurations", numConfigs));
				for (int i = 0; i < numConfigs; i++)
				{
					Log.Warn(TAG, string.Format("Configuration {0:D}:\n", i));
					PrintConfig(egl, display, configs[i]);
				}
			}

			internal virtual void PrintConfig(EGL10 egl, EGLDisplay display, EGLConfig config)
			{
				int[] attributes = new int[] {EGL10.EglBufferSize, EGL10.EglAlphaSize, EGL10.EglBlueSize, EGL10.EglGreenSize, EGL10.EglRedSize, EGL10.EglDepthSize, EGL10.EglStencilSize, EGL10.EglConfigCaveat, EGL10.EglConfigId, EGL10.EglLevel, EGL10.EglMaxPbufferHeight, EGL10.EglMaxPbufferPixels, EGL10.EglMaxPbufferWidth, EGL10.EglNativeRenderable, EGL10.EglNativeVisualId, EGL10.EglNativeVisualType, 0x3030, EGL10.EglSamples, EGL10.EglSampleBuffers, EGL10.EglSurfaceType, EGL10.EglTransparentType, EGL10.EglTransparentRedValue, EGL10.EglTransparentGreenValue, EGL10.EglTransparentBlueValue, 0x3039, 0x303A, 0x303B, 0x303C, EGL10.EglLuminanceSize, EGL10.EglAlphaMaskSize, EGL10.EglColorBufferType, EGL10.EglRenderableType, 0x3042};
				string[] names = new string[] {"EGL_BUFFER_SIZE", "EGL_ALPHA_SIZE", "EGL_BLUE_SIZE", "EGL_GREEN_SIZE", "EGL_RED_SIZE", "EGL_DEPTH_SIZE", "EGL_STENCIL_SIZE", "EGL_CONFIG_CAVEAT", "EGL_CONFIG_ID", "EGL_LEVEL", "EGL_MAX_PBUFFER_HEIGHT", "EGL_MAX_PBUFFER_PIXELS", "EGL_MAX_PBUFFER_WIDTH", "EGL_NATIVE_RENDERABLE", "EGL_NATIVE_VISUAL_ID", "EGL_NATIVE_VISUAL_TYPE", "EGL_PRESERVED_RESOURCES", "EGL_SAMPLES", "EGL_SAMPLE_BUFFERS", "EGL_SURFACE_TYPE", "EGL_TRANSPARENT_TYPE", "EGL_TRANSPARENT_RED_VALUE", "EGL_TRANSPARENT_GREEN_VALUE", "EGL_TRANSPARENT_BLUE_VALUE", "EGL_BIND_TO_TEXTURE_RGB", "EGL_BIND_TO_TEXTURE_RGBA", "EGL_MIN_SWAP_INTERVAL", "EGL_MAX_SWAP_INTERVAL", "EGL_LUMINANCE_SIZE", "EGL_ALPHA_MASK_SIZE", "EGL_COLOR_BUFFER_TYPE", "EGL_RENDERABLE_TYPE", "EGL_CONFORMANT"};
				int[] value = new int[1];
				for (int i = 0; i < attributes.Length; i++)
				{
					int attribute = attributes[i];
					string name = names[i];
					if (egl.eglGetConfigAttrib(display, config, attribute, value))
					{
						Log.Warn(TAG, string.Format("  {0}: {1:D}\n", name, value[0]));
					}
					else
					{
						// Log.Warn(TAG, String.format("  %s: failed\n", name));
						while (egl.eglGetError() != EGL10.EglSuccess);
					}
				}
			}

			// Subclasses can adjust these values:
			protected internal int mRedSize;
			protected internal int mGreenSize;
			protected internal int mBlueSize;
			protected internal int mAlphaSize;
			protected internal int mDepthSize;
			protected internal int mStencilSize;
			internal int[] mValue = new int[1];
		}

		// IsSupported
		// Return true if this device support Open GL ES 2.0 rendering.
		public static bool IsSupported(Context context)
		{
			ActivityManager am = (ActivityManager) context.GetSystemService(Context.ActivityService);
			ConfigurationInfo info = am.DeviceConfigurationInfo;
			if (info.ReqGlEsVersion >= 0x20000)
			{
				// Open GL ES 2.0 is supported.
				return true;
			}
			return false;
		}

		public virtual void OnDrawFrame(GL10 gl)
		{
			nativeFunctionLock.Lock();
			if (!nativeFunctionsRegisted || !surfaceCreated)
			{
				nativeFunctionLock.Unlock();
				return;
			}

			if (!openGLCreated)
			{
				if (0 != CreateOpenGLNative(nativeObject, viewWidth, viewHeight))
				{
					return; // Failed to create OpenGL
				}
				openGLCreated = true; // Created OpenGL successfully
			}
			DrawNative(nativeObject); // Draw the new frame
			nativeFunctionLock.Unlock();
		}

		public virtual void OnSurfaceChanged(GL10 gl, int width, int height)
		{
			surfaceCreated = true;
			viewWidth = width;
			viewHeight = height;

			nativeFunctionLock.Lock();
			if (nativeFunctionsRegisted)
			{
				if (CreateOpenGLNative(nativeObject,width,height) == 0)
				{
					openGLCreated = true;
				}
			}
			nativeFunctionLock.Unlock();
		}

		public virtual void OnSurfaceCreated(GL10 gl, EGLConfig config)
		{
		}

		public virtual void RegisterNativeObject(long nativeObject)
		{
			nativeFunctionLock.Lock();
			this.nativeObject = nativeObject;
			nativeFunctionsRegisted = true;
			nativeFunctionLock.Unlock();
		}

		public virtual void DeRegisterNativeObject()
		{
			nativeFunctionLock.Lock();
			nativeFunctionsRegisted = false;
			openGLCreated = false;
			this.nativeObject = 0;
			nativeFunctionLock.Unlock();
		}

		public virtual void ReDraw()
		{
			if (surfaceCreated)
			{
				// Request the renderer to redraw using the render thread context.
				RequestRender();
			}
		}


		[DllImport("libwebrtc-video-demo-jni.so")]
		private extern int CreateOpenGLNative(long nativeObject, int width, int height);

		[DllImport("libwebrtc-video-demo-jni.so")]
		private extern void DrawNative(long nativeObject);

	}

}