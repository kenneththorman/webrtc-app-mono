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
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Util;
using Java.Lang;
using Java.Nio;
using Java.Util.Concurrent.Locks;
using Exception = System.Exception;

namespace WebRtc.Org.Webrtc.Voiceengine
{
	internal class WebRTCAudioDevice
	{
		private AudioTrack _audioTrack = null;
		private AudioRecord _audioRecord = null;

		private Context _context;
		private AudioManager _audioManager;

		private ByteBuffer _playBuffer;
		private ByteBuffer _recBuffer;
		private byte[] _tempBufPlay;
		private byte[] _tempBufRec;

		private readonly ReentrantLock _playLock = new ReentrantLock();
		private readonly ReentrantLock _recLock = new ReentrantLock();

		private bool _doPlayInit = true;
		private bool _doRecInit = true;
		private bool _isRecording = false;
		private bool _isPlaying = false;

		private int _bufferedRecSamples = 0;
		private int _bufferedPlaySamples = 0;
		private int _playPosition = 0;

		internal WebRTCAudioDevice()
		{
			try
			{
				_playBuffer = ByteBuffer.AllocateDirect(2 * 480); // Max 10 ms @ 48
																  // kHz
				_recBuffer = ByteBuffer.AllocateDirect(2 * 480); // Max 10 ms @ 48
																 // kHz
			}
			catch (Exception e)
			{
				DoLog(e.Message);
			}

			_tempBufPlay = new byte[2 * 480];
			_tempBufRec = new byte[2 * 480];
		}

		private int InitRecording(AudioSource audioSource, int sampleRate)
		{
			// get the minimum buffer size that can be used
			int minRecBufSize = AudioRecord.GetMinBufferSize(sampleRate, ChannelIn.Mono, Encoding.Pcm16bit);

			// DoLog("min rec buf size is " + minRecBufSize);

			// double size to be more safe
			int recBufSize = minRecBufSize * 2;
			_bufferedRecSamples = (5 * sampleRate) / 200;
			// DoLog("rough rec delay set to " + _bufferedRecSamples);

			// release the object
			if (_audioRecord != null)
			{
				_audioRecord.Release();
				_audioRecord = null;
			}

			try
			{
				_audioRecord = new AudioRecord(audioSource, sampleRate, ChannelIn.Mono, Encoding.Pcm16bit, recBufSize);

			}
			catch (Exception e)
			{
				DoLog(e.Message);
				return -1;
			}

			// check that the audioRecord is ready to be used
			if (_audioRecord.State != State.Initialized)
			{
				// DoLog("rec not initialized " + sampleRate);
				return -1;
			}

			// DoLog("rec sample rate set to " + sampleRate);

			return _bufferedRecSamples;
		}

		private int StartRecording()
		{
			if (_isPlaying == false)
			{
				SetAudioMode(true);
			}

			// start recording
			try
			{
				_audioRecord.StartRecording();

			}
			catch (IllegalStateException e)
			{
				Console.WriteLine(e.ToString());
				Console.Write(e.StackTrace);
				return -1;
			}

			_isRecording = true;
			return 0;
		}

		private int InitPlayback(int sampleRate)
		{
			// get the minimum buffer size that can be used
			int minPlayBufSize = AudioTrack.GetMinBufferSize(sampleRate, ChannelOut.Mono, Encoding.Pcm16bit);

			// DoLog("min play buf size is " + minPlayBufSize);

			int playBufSize = minPlayBufSize;
			if (playBufSize < 6000)
			{
				playBufSize *= 2;
			}
			_bufferedPlaySamples = 0;
			// DoLog("play buf size is " + playBufSize);

			// release the object
			if (_audioTrack != null)
			{
				_audioTrack.Release();
				_audioTrack = null;
			}

			try
			{
				_audioTrack = new AudioTrack(Stream.VoiceCall, sampleRate, ChannelConfiguration.Mono, Encoding.Pcm16bit, playBufSize, AudioTrackMode.Stream);
			}
			catch (Exception e)
			{
				DoLog(e.Message);
				return -1;
			}

			// check that the audioRecord is ready to be used
			if (_audioTrack.State != AudioTrackState.Initialized)
			{
				// DoLog("play not initialized " + sampleRate);
				return -1;
			}

			// DoLog("play sample rate set to " + sampleRate);

			if (_audioManager == null && _context != null)
			{
				_audioManager = (AudioManager) _context.GetSystemService(Context.AudioService);
			}

			// Return max playout volume
			if (_audioManager == null)
			{
				// Don't know the max volume but still init is OK for playout,
				// so we should not return error.
				return 0;
			}
			return _audioManager.GetStreamMaxVolume(Stream.VoiceCall);
		}

		private int StartPlayback()
		{
			if (_isRecording == false)
			{
				SetAudioMode(true);
			}

			// start playout
			try
			{
				_audioTrack.Play();

			}
			catch (IllegalStateException e)
			{
				Console.WriteLine(e.ToString());
				Console.Write(e.StackTrace);
				return -1;
			}

			_isPlaying = true;
			return 0;
		}

		private int StopRecording()
		{
			_recLock.Lock();
			try
			{
				// only stop if we are recording
				if (_audioRecord.RecordingState == RecordState.Recording)
				{
					// stop recording
					try
					{
						_audioRecord.Stop();
					}
					catch (IllegalStateException e)
					{
						Console.WriteLine(e.ToString());
						Console.Write(e.StackTrace);
						return -1;
					}
				}

				// release the object
				_audioRecord.Release();
				_audioRecord = null;

			}
			finally
			{
				// Ensure we always unlock, both for success, exception or error
				// return.
				_doRecInit = true;
				_recLock.Unlock();
			}

			if (_isPlaying == false)
			{
				SetAudioMode(false);
			}

			_isRecording = false;
			return 0;
		}

		private int StopPlayback()
		{
			_playLock.Lock();
			try
			{
				// only stop if we are playing
				if (_audioTrack.PlayState == PlayState.Playing)
				{
					// stop playout
					try
					{
						_audioTrack.Stop();
					}
					catch (IllegalStateException e)
					{
						Console.WriteLine(e.ToString());
						Console.Write(e.StackTrace);
						return -1;
					}

					// flush the buffers
					_audioTrack.Flush();
				}

				// release the object
				_audioTrack.Release();
				_audioTrack = null;

			}
			finally
			{
				// Ensure we always unlock, both for success, exception or error
				// return.
				_doPlayInit = true;
				_playLock.Unlock();
			}

			if (_isRecording == false)
			{
				SetAudioMode(false);
			}

			_isPlaying = false;
			return 0;
		}

		private int PlayAudio(int lengthInBytes)
		{

			int bufferedSamples = 0;

			_playLock.Lock();
			try
			{
				if (_audioTrack == null)
				{
					return -2; // We have probably closed down while waiting for
							   // play lock
				}

				// Set priority, only do once
				if (_doPlayInit == true)
				{
					try
					{
						Android.OS.Process.SetThreadPriority(ThreadPriority.UrgentAudio);
					}
					catch (Exception e)
					{
						DoLog("Set play thread priority failed: " + e.Message);
					}
					_doPlayInit = false;
				}

				int written = 0;
				_playBuffer.Get(_tempBufPlay);
				written = _audioTrack.Write(_tempBufPlay, 0, lengthInBytes);
				_playBuffer.Rewind(); // Reset the position to start of buffer

				// DoLog("Wrote data to sndCard");

				// increase by number of written samples
				_bufferedPlaySamples += (written >> 1);

				// decrease by number of played samples
				int pos = _audioTrack.PlaybackHeadPosition;
				if (pos < _playPosition) // wrap or reset by driver
				{
					_playPosition = 0; // reset
				}
				_bufferedPlaySamples -= (pos - _playPosition);
				_playPosition = pos;

				if (!_isRecording)
				{
					bufferedSamples = _bufferedPlaySamples;
				}

				if (written != lengthInBytes)
				{
					// DoLog("Could not write all data to sc (written = " + written
					// + ", length = " + lengthInBytes + ")");
					return -1;
				}

			}
			finally
			{
				// Ensure we always unlock, both for success, exception or error
				// return.
				_playLock.Unlock();
			}

			return bufferedSamples;
		}

		private int RecordAudio(int lengthInBytes)
		{
			_recLock.Lock();

			try
			{
				if (_audioRecord == null)
				{
					return -2; // We have probably closed down while waiting for rec
							   // lock
				}

				// Set priority, only do once
				if (_doRecInit)
				{
					try
					{
						Android.OS.Process.SetThreadPriority(ThreadPriority.UrgentAudio);
					}
					catch (Exception e)
					{
						DoLog("Set rec thread priority failed: " + e.Message);
					}
					_doRecInit = false;
				}

				int readBytes = 0;
				_recBuffer.Rewind(); // Reset the position to start of buffer
				readBytes = _audioRecord.Read(_tempBufRec, 0, lengthInBytes);
				// DoLog("read " + readBytes + "from SC");
				_recBuffer.Put(_tempBufRec);

				if (readBytes != lengthInBytes)
				{
					// DoLog("Could not read all data from sc (read = " + readBytes
					// + ", length = " + lengthInBytes + ")");
					return -1;
				}

			}
			catch (Exception e)
			{
				DoLogErr("RecordAudio try failed: " + e.Message);

			}
			finally
			{
				// Ensure we always unlock, both for success, exception or error
				// return.
				_recLock.Unlock();
			}

			return (_bufferedPlaySamples);
		}

		private int SetPlayoutSpeaker(bool loudspeakerOn)
		{
			// create audio manager if needed
			if (_audioManager == null && _context != null)
			{
				_audioManager = (AudioManager) _context.GetSystemService(Context.AudioService);
			}

			if (_audioManager == null)
			{
				DoLogErr("Could not change audio routing - no audio manager");
				return -1;
			}

			var apiLevel = Build.VERSION.SdkInt;

			if ((BuildVersionCodes.Cupcake == apiLevel) || (BuildVersionCodes.Donut == apiLevel))
			{
				// 1.5 and 1.6 devices
				if (loudspeakerOn)
				{
					// route audio to back speaker
					_audioManager.Mode = Mode.Normal;
				}
				else
				{
					// route audio to earpiece
					_audioManager.Mode = Mode.InCall;
				}
			}
			else
			{
				// 2.x devices
				if ((Build.Brand.Equals("Samsung") || Build.Brand.Equals("samsung")) && ((BuildVersionCodes.Eclair == apiLevel) || (BuildVersionCodes.Eclair01 == apiLevel) || (BuildVersionCodes.EclairMr1 == apiLevel)))
				{
					// Samsung 2.0, 2.0.1 and 2.1 devices
					if (loudspeakerOn)
					{
						// route audio to back speaker
						_audioManager.Mode = Mode.InCall;
						_audioManager.SpeakerphoneOn = loudspeakerOn;
					}
					else
					{
						// route audio to earpiece
						_audioManager.SpeakerphoneOn = loudspeakerOn;
						_audioManager.Mode = Mode.Normal;
					}
				}
				else
				{
					// Non-Samsung and Samsung 2.2 and up devices
					_audioManager.SpeakerphoneOn = loudspeakerOn;
				}
			}

			return 0;
		}

		private int SetPlayoutVolume(int level)
		{

			// create audio manager if needed
			if (_audioManager == null && _context != null)
			{
				_audioManager = (AudioManager) _context.GetSystemService(Context.AudioService);
			}

			int retVal = -1;

			if (_audioManager != null)
			{
				_audioManager.SetStreamVolume(Stream.VoiceCall, level, 0);
				retVal = 0;
			}

			return retVal;
		}

		private int GetPlayoutVolume()
		{

			// create audio manager if needed
			if (_audioManager == null && _context != null)
			{
				_audioManager = (AudioManager) _context.GetSystemService(Context.AudioService);
			}

			int level = -1;

			if (_audioManager != null)
			{
				level = _audioManager.GetStreamVolume(Stream.VoiceCall);
			}

			return level;
		}

		private void SetAudioMode(bool startCall)
		{
			var apiLevel = Build.VERSION.SdkInt;

			if (_audioManager == null && _context != null)
			{
				_audioManager = (AudioManager) _context.GetSystemService(Context.AudioService);
			}

			if (_audioManager == null)
			{
				DoLogErr("Could not set audio mode - no audio manager");
				return;
			}

			// ***IMPORTANT*** When the API level for honeycomb (H) has been
			// decided,
			// the condition should be changed to include API level 8 to H-1.
			if ((Build.Brand.Equals("Samsung") || Build.Brand.Equals("samsung")) && (BuildVersionCodes.Froyo == apiLevel))
			{
				// Set Samsung specific VoIP mode for 2.2 devices
				// 4 is VoIP mode
				var mode = (startCall ? Mode.InCall : Mode.Normal);
				_audioManager.Mode = mode;
				if (_audioManager.Mode != mode)
				{
					DoLogErr("Could not set audio mode for Samsung device");
				}
			}
		}

		internal readonly string logTag = "WebRTC AD java";

		private void DoLog(string msg)
		{
			Log.Debug(logTag, msg);
		}

		private void DoLogErr(string msg)
		{
			Log.Error(logTag, msg);
		}
	}

}