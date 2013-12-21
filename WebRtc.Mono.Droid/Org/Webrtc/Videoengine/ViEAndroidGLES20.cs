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

namespace org.webrtc.videoengine
{


	using ActivityManager = android.app.ActivityManager;
	using Context = android.content.Context;
	using ConfigurationInfo = android.content.pm.ConfigurationInfo;
	using PixelFormat = android.graphics.PixelFormat;
	using GLSurfaceView = android.opengl.GLSurfaceView;
	using Log = android.util.Log;

	public class ViEAndroidGLES20 : GLSurfaceView, GLSurfaceView.Renderer
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
				this.Holder.Format = PixelFormat.TRANSLUCENT;
			}

			// Setup the context factory for 2.0 rendering.
			// See ContextFactory class definition below
			EGLContextFactory = new ContextFactory();

			// We need to choose an EGLConfig that matches the format of
			// our surface exactly. This is going to be done in our
			// custom config chooser. See ConfigChooser class definition
			// below.
			EGLConfigChooser = translucent ? new ConfigChooser(8, 8, 8, 8, depth, stencil) : new ConfigChooser(5, 6, 5, 0, depth, stencil);

			// Set the renderer responsible for frame rendering
			this.Renderer = this;
			this.RenderMode = GLSurfaceView.RENDERMODE_WHEN_DIRTY;
		}

		private class ContextFactory : GLSurfaceView.EGLContextFactory
		{
			internal static int EGL_CONTEXT_CLIENT_VERSION = 0x3098;
			public virtual EGLContext createContext(EGL10 egl, EGLDisplay display, EGLConfig eglConfig)
			{
				Log.w(TAG, "creating OpenGL ES 2.0 context");
				checkEglError("Before eglCreateContext", egl);
				int[] attrib_list = new int[] {EGL_CONTEXT_CLIENT_VERSION, 2, EGL10.EGL_NONE};
				EGLContext context = egl.eglCreateContext(display, eglConfig, EGL10.EGL_NO_CONTEXT, attrib_list);
				checkEglError("After eglCreateContext", egl);
				return context;
			}

			public virtual void destroyContext(EGL10 egl, EGLDisplay display, EGLContext context)
			{
				egl.eglDestroyContext(display, context);
			}
		}

		private static void checkEglError(string prompt, EGL10 egl)
		{
			int error;
			while ((error = egl.eglGetError()) != EGL10.EGL_SUCCESS)
			{
				Log.e(TAG, string.Format("{0}: EGL error: 0x{1:x}", prompt, error));
			}
		}

		private class ConfigChooser : GLSurfaceView.EGLConfigChooser
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
			internal static int[] s_configAttribs2 = new int[] {EGL10.EGL_RED_SIZE, 4, EGL10.EGL_GREEN_SIZE, 4, EGL10.EGL_BLUE_SIZE, 4, EGL10.EGL_RENDERABLE_TYPE, EGL_OPENGL_ES2_BIT, EGL10.EGL_NONE};

			public virtual EGLConfig chooseConfig(EGL10 egl, EGLDisplay display)
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
					printConfigs(egl, display, configs);
				}
				// Now return the "best" one
				return chooseConfig(egl, display, configs);
			}

			public virtual EGLConfig chooseConfig(EGL10 egl, EGLDisplay display, EGLConfig[] configs)
			{
				foreach (EGLConfig config in configs)
				{
					int d = findConfigAttrib(egl, display, config, EGL10.EGL_DEPTH_SIZE, 0);
					int s = findConfigAttrib(egl, display, config, EGL10.EGL_STENCIL_SIZE, 0);

					// We need at least mDepthSize and mStencilSize bits
					if (d < mDepthSize || s < mStencilSize)
					{
						continue;
					}

					// We want an *exact* match for red/green/blue/alpha
					int r = findConfigAttrib(egl, display, config, EGL10.EGL_RED_SIZE, 0);
					int g = findConfigAttrib(egl, display, config, EGL10.EGL_GREEN_SIZE, 0);
					int b = findConfigAttrib(egl, display, config, EGL10.EGL_BLUE_SIZE, 0);
					int a = findConfigAttrib(egl, display, config, EGL10.EGL_ALPHA_SIZE, 0);

					if (r == mRedSize && g == mGreenSize && b == mBlueSize && a == mAlphaSize)
					{
						return config;
					}
				}
				return null;
			}

			internal virtual int findConfigAttrib(EGL10 egl, EGLDisplay display, EGLConfig config, int attribute, int defaultValue)
			{

				if (egl.eglGetConfigAttrib(display, config, attribute, mValue))
				{
					return mValue[0];
				}
				return defaultValue;
			}

			internal virtual void printConfigs(EGL10 egl, EGLDisplay display, EGLConfig[] configs)
			{
				int numConfigs = configs.Length;
				Log.w(TAG, string.Format("{0:D} configurations", numConfigs));
				for (int i = 0; i < numConfigs; i++)
				{
					Log.w(TAG, string.Format("Configuration {0:D}:\n", i));
					printConfig(egl, display, configs[i]);
				}
			}

			internal virtual void printConfig(EGL10 egl, EGLDisplay display, EGLConfig config)
			{
				int[] attributes = new int[] {EGL10.EGL_BUFFER_SIZE, EGL10.EGL_ALPHA_SIZE, EGL10.EGL_BLUE_SIZE, EGL10.EGL_GREEN_SIZE, EGL10.EGL_RED_SIZE, EGL10.EGL_DEPTH_SIZE, EGL10.EGL_STENCIL_SIZE, EGL10.EGL_CONFIG_CAVEAT, EGL10.EGL_CONFIG_ID, EGL10.EGL_LEVEL, EGL10.EGL_MAX_PBUFFER_HEIGHT, EGL10.EGL_MAX_PBUFFER_PIXELS, EGL10.EGL_MAX_PBUFFER_WIDTH, EGL10.EGL_NATIVE_RENDERABLE, EGL10.EGL_NATIVE_VISUAL_ID, EGL10.EGL_NATIVE_VISUAL_TYPE, 0x3030, EGL10.EGL_SAMPLES, EGL10.EGL_SAMPLE_BUFFERS, EGL10.EGL_SURFACE_TYPE, EGL10.EGL_TRANSPARENT_TYPE, EGL10.EGL_TRANSPARENT_RED_VALUE, EGL10.EGL_TRANSPARENT_GREEN_VALUE, EGL10.EGL_TRANSPARENT_BLUE_VALUE, 0x3039, 0x303A, 0x303B, 0x303C, EGL10.EGL_LUMINANCE_SIZE, EGL10.EGL_ALPHA_MASK_SIZE, EGL10.EGL_COLOR_BUFFER_TYPE, EGL10.EGL_RENDERABLE_TYPE, 0x3042};
				string[] names = new string[] {"EGL_BUFFER_SIZE", "EGL_ALPHA_SIZE", "EGL_BLUE_SIZE", "EGL_GREEN_SIZE", "EGL_RED_SIZE", "EGL_DEPTH_SIZE", "EGL_STENCIL_SIZE", "EGL_CONFIG_CAVEAT", "EGL_CONFIG_ID", "EGL_LEVEL", "EGL_MAX_PBUFFER_HEIGHT", "EGL_MAX_PBUFFER_PIXELS", "EGL_MAX_PBUFFER_WIDTH", "EGL_NATIVE_RENDERABLE", "EGL_NATIVE_VISUAL_ID", "EGL_NATIVE_VISUAL_TYPE", "EGL_PRESERVED_RESOURCES", "EGL_SAMPLES", "EGL_SAMPLE_BUFFERS", "EGL_SURFACE_TYPE", "EGL_TRANSPARENT_TYPE", "EGL_TRANSPARENT_RED_VALUE", "EGL_TRANSPARENT_GREEN_VALUE", "EGL_TRANSPARENT_BLUE_VALUE", "EGL_BIND_TO_TEXTURE_RGB", "EGL_BIND_TO_TEXTURE_RGBA", "EGL_MIN_SWAP_INTERVAL", "EGL_MAX_SWAP_INTERVAL", "EGL_LUMINANCE_SIZE", "EGL_ALPHA_MASK_SIZE", "EGL_COLOR_BUFFER_TYPE", "EGL_RENDERABLE_TYPE", "EGL_CONFORMANT"};
				int[] value = new int[1];
				for (int i = 0; i < attributes.Length; i++)
				{
					int attribute = attributes[i];
					string name = names[i];
					if (egl.eglGetConfigAttrib(display, config, attribute, value))
					{
						Log.w(TAG, string.Format("  {0}: {1:D}\n", name, value[0]));
					}
					else
					{
						// Log.w(TAG, String.format("  %s: failed\n", name));
						while (egl.eglGetError() != EGL10.EGL_SUCCESS);
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
			ActivityManager am = (ActivityManager) context.getSystemService(Context.ACTIVITY_SERVICE);
			ConfigurationInfo info = am.DeviceConfigurationInfo;
			if (info.reqGlEsVersion >= 0x20000)
			{
				// Open GL ES 2.0 is supported.
				return true;
			}
			return false;
		}

		public virtual void onDrawFrame(GL10 gl)
		{
			nativeFunctionLock.@lock();
			if (!nativeFunctionsRegisted || !surfaceCreated)
			{
				nativeFunctionLock.unlock();
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
			nativeFunctionLock.unlock();
		}

		public virtual void onSurfaceChanged(GL10 gl, int width, int height)
		{
			surfaceCreated = true;
			viewWidth = width;
			viewHeight = height;

			nativeFunctionLock.@lock();
			if (nativeFunctionsRegisted)
			{
				if (CreateOpenGLNative(nativeObject,width,height) == 0)
				{
					openGLCreated = true;
				}
			}
			nativeFunctionLock.unlock();
		}

		public virtual void onSurfaceCreated(GL10 gl, EGLConfig config)
		{
		}

		public virtual void RegisterNativeObject(long nativeObject)
		{
			nativeFunctionLock.@lock();
			this.nativeObject = nativeObject;
			nativeFunctionsRegisted = true;
			nativeFunctionLock.unlock();
		}

		public virtual void DeRegisterNativeObject()
		{
			nativeFunctionLock.@lock();
			nativeFunctionsRegisted = false;
			openGLCreated = false;
			this.nativeObject = 0;
			nativeFunctionLock.unlock();
		}

		public virtual void ReDraw()
		{
			if (surfaceCreated)
			{
				// Request the renderer to redraw using the render thread context.
				this.requestRender();
			}
		}

//JAVA TO C# CONVERTER TODO TASK: Replace 'unknown' with the appropriate dll name:
		[DllImport("unknown")]
		private extern int CreateOpenGLNative(long nativeObject, int width, int height);
//JAVA TO C# CONVERTER TODO TASK: Replace 'unknown' with the appropriate dll name:
		[DllImport("unknown")]
		private extern void DrawNative(long nativeObject);

	}

}