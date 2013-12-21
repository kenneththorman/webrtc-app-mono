using System;

/*
 *  Copyright (c) 2012 The WebRTC project authors. All Rights Reserved.
 *
 *  Use of this source code is governed by a BSD-style license
 *  that can be found in the LICENSE file in the root of the source
 *  tree. An additional intellectual property rights grant can be found
 *  in the file PATENTS.  All contributing project authors may
 *  be found in the AUTHORS file in the root of the source tree.
 */

namespace org.webrtc.voiceengine
{


	using Context = android.content.Context;
	using AudioFormat = android.media.AudioFormat;
	using AudioManager = android.media.AudioManager;
	using AudioRecord = android.media.AudioRecord;
	using AudioTrack = android.media.AudioTrack;
	using Log = android.util.Log;

	internal class WebRTCAudioDevice
	{
		private AudioTrack _audioTrack = null;
		private AudioRecord _audioRecord = null;

		private Context _context;
		private AudioManager _audioManager;

		private ByteBuffer _playBuffer;
		private ByteBuffer _recBuffer;
		private sbyte[] _tempBufPlay;
		private sbyte[] _tempBufRec;

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
				_playBuffer = ByteBuffer.allocateDirect(2 * 480); // Max 10 ms @ 48
																  // kHz
				_recBuffer = ByteBuffer.allocateDirect(2 * 480); // Max 10 ms @ 48
																 // kHz
			}
			catch (Exception e)
			{
				DoLog(e.Message);
			}

			_tempBufPlay = new sbyte[2 * 480];
			_tempBufRec = new sbyte[2 * 480];
		}

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @SuppressWarnings("unused") private int InitRecording(int audioSource, int sampleRate)
		private int InitRecording(int audioSource, int sampleRate)
		{
			// get the minimum buffer size that can be used
			int minRecBufSize = AudioRecord.getMinBufferSize(sampleRate, AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT);

			// DoLog("min rec buf size is " + minRecBufSize);

			// double size to be more safe
			int recBufSize = minRecBufSize * 2;
			_bufferedRecSamples = (5 * sampleRate) / 200;
			// DoLog("rough rec delay set to " + _bufferedRecSamples);

			// release the object
			if (_audioRecord != null)
			{
				_audioRecord.release();
				_audioRecord = null;
			}

			try
			{
				_audioRecord = new AudioRecord(audioSource, sampleRate, AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT, recBufSize);

			}
			catch (Exception e)
			{
				DoLog(e.Message);
				return -1;
			}

			// check that the audioRecord is ready to be used
			if (_audioRecord.State != AudioRecord.STATE_INITIALIZED)
			{
				// DoLog("rec not initialized " + sampleRate);
				return -1;
			}

			// DoLog("rec sample rate set to " + sampleRate);

			return _bufferedRecSamples;
		}

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @SuppressWarnings("unused") private int StartRecording()
		private int StartRecording()
		{
			if (_isPlaying == false)
			{
				SetAudioMode(true);
			}

			// start recording
			try
			{
				_audioRecord.startRecording();

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

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @SuppressWarnings("unused") private int InitPlayback(int sampleRate)
		private int InitPlayback(int sampleRate)
		{
			// get the minimum buffer size that can be used
			int minPlayBufSize = AudioTrack.getMinBufferSize(sampleRate, AudioFormat.CHANNEL_OUT_MONO, AudioFormat.ENCODING_PCM_16BIT);

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
				_audioTrack.release();
				_audioTrack = null;
			}

			try
			{
				_audioTrack = new AudioTrack(AudioManager.STREAM_VOICE_CALL, sampleRate, AudioFormat.CHANNEL_OUT_MONO, AudioFormat.ENCODING_PCM_16BIT, playBufSize, AudioTrack.MODE_STREAM);
			}
			catch (Exception e)
			{
				DoLog(e.Message);
				return -1;
			}

			// check that the audioRecord is ready to be used
			if (_audioTrack.State != AudioTrack.STATE_INITIALIZED)
			{
				// DoLog("play not initialized " + sampleRate);
				return -1;
			}

			// DoLog("play sample rate set to " + sampleRate);

			if (_audioManager == null && _context != null)
			{
				_audioManager = (AudioManager) _context.getSystemService(Context.AUDIO_SERVICE);
			}

			// Return max playout volume
			if (_audioManager == null)
			{
				// Don't know the max volume but still init is OK for playout,
				// so we should not return error.
				return 0;
			}
			return _audioManager.getStreamMaxVolume(AudioManager.STREAM_VOICE_CALL);
		}

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @SuppressWarnings("unused") private int StartPlayback()
		private int StartPlayback()
		{
			if (_isRecording == false)
			{
				SetAudioMode(true);
			}

			// start playout
			try
			{
				_audioTrack.play();

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

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @SuppressWarnings("unused") private int StopRecording()
		private int StopRecording()
		{
			_recLock.@lock();
			try
			{
				// only stop if we are recording
				if (_audioRecord.RecordingState == AudioRecord.RECORDSTATE_RECORDING)
				{
					// stop recording
					try
					{
						_audioRecord.stop();
					}
					catch (IllegalStateException e)
					{
						Console.WriteLine(e.ToString());
						Console.Write(e.StackTrace);
						return -1;
					}
				}

				// release the object
				_audioRecord.release();
				_audioRecord = null;

			}
			finally
			{
				// Ensure we always unlock, both for success, exception or error
				// return.
				_doRecInit = true;
				_recLock.unlock();
			}

			if (_isPlaying == false)
			{
				SetAudioMode(false);
			}

			_isRecording = false;
			return 0;
		}

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @SuppressWarnings("unused") private int StopPlayback()
		private int StopPlayback()
		{
			_playLock.@lock();
			try
			{
				// only stop if we are playing
				if (_audioTrack.PlayState == AudioTrack.PLAYSTATE_PLAYING)
				{
					// stop playout
					try
					{
						_audioTrack.stop();
					}
					catch (IllegalStateException e)
					{
						Console.WriteLine(e.ToString());
						Console.Write(e.StackTrace);
						return -1;
					}

					// flush the buffers
					_audioTrack.flush();
				}

				// release the object
				_audioTrack.release();
				_audioTrack = null;

			}
			finally
			{
				// Ensure we always unlock, both for success, exception or error
				// return.
				_doPlayInit = true;
				_playLock.unlock();
			}

			if (_isRecording == false)
			{
				SetAudioMode(false);
			}

			_isPlaying = false;
			return 0;
		}

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @SuppressWarnings("unused") private int PlayAudio(int lengthInBytes)
		private int PlayAudio(int lengthInBytes)
		{

			int bufferedSamples = 0;

			_playLock.@lock();
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
						android.os.Process.ThreadPriority = android.os.Process.THREAD_PRIORITY_URGENT_AUDIO;
					}
					catch (Exception e)
					{
						DoLog("Set play thread priority failed: " + e.Message);
					}
					_doPlayInit = false;
				}

				int written = 0;
				_playBuffer.get(_tempBufPlay);
				written = _audioTrack.write(_tempBufPlay, 0, lengthInBytes);
				_playBuffer.rewind(); // Reset the position to start of buffer

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
				_playLock.unlock();
			}

			return bufferedSamples;
		}

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @SuppressWarnings("unused") private int RecordAudio(int lengthInBytes)
		private int RecordAudio(int lengthInBytes)
		{
			_recLock.@lock();

			try
			{
				if (_audioRecord == null)
				{
					return -2; // We have probably closed down while waiting for rec
							   // lock
				}

				// Set priority, only do once
				if (_doRecInit == true)
				{
					try
					{
						android.os.Process.ThreadPriority = android.os.Process.THREAD_PRIORITY_URGENT_AUDIO;
					}
					catch (Exception e)
					{
						DoLog("Set rec thread priority failed: " + e.Message);
					}
					_doRecInit = false;
				}

				int readBytes = 0;
				_recBuffer.rewind(); // Reset the position to start of buffer
				readBytes = _audioRecord.read(_tempBufRec, 0, lengthInBytes);
				// DoLog("read " + readBytes + "from SC");
				_recBuffer.put(_tempBufRec);

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
				_recLock.unlock();
			}

			return (_bufferedPlaySamples);
		}

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @SuppressWarnings("unused") private int SetPlayoutSpeaker(boolean loudspeakerOn)
		private int SetPlayoutSpeaker(bool loudspeakerOn)
		{
			// create audio manager if needed
			if (_audioManager == null && _context != null)
			{
				_audioManager = (AudioManager) _context.getSystemService(Context.AUDIO_SERVICE);
			}

			if (_audioManager == null)
			{
				DoLogErr("Could not change audio routing - no audio manager");
				return -1;
			}

			int apiLevel = android.os.Build.VERSION.SDK_INT;

			if ((3 == apiLevel) || (4 == apiLevel))
			{
				// 1.5 and 1.6 devices
				if (loudspeakerOn)
				{
					// route audio to back speaker
					_audioManager.Mode = AudioManager.MODE_NORMAL;
				}
				else
				{
					// route audio to earpiece
					_audioManager.Mode = AudioManager.MODE_IN_CALL;
				}
			}
			else
			{
				// 2.x devices
				if ((android.os.Build.BRAND.Equals("Samsung") || android.os.Build.BRAND.Equals("samsung")) && ((5 == apiLevel) || (6 == apiLevel) || (7 == apiLevel)))
				{
					// Samsung 2.0, 2.0.1 and 2.1 devices
					if (loudspeakerOn)
					{
						// route audio to back speaker
						_audioManager.Mode = AudioManager.MODE_IN_CALL;
						_audioManager.SpeakerphoneOn = loudspeakerOn;
					}
					else
					{
						// route audio to earpiece
						_audioManager.SpeakerphoneOn = loudspeakerOn;
						_audioManager.Mode = AudioManager.MODE_NORMAL;
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

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @SuppressWarnings("unused") private int SetPlayoutVolume(int level)
		private int SetPlayoutVolume(int level)
		{

			// create audio manager if needed
			if (_audioManager == null && _context != null)
			{
				_audioManager = (AudioManager) _context.getSystemService(Context.AUDIO_SERVICE);
			}

			int retVal = -1;

			if (_audioManager != null)
			{
				_audioManager.setStreamVolume(AudioManager.STREAM_VOICE_CALL, level, 0);
				retVal = 0;
			}

			return retVal;
		}

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @SuppressWarnings("unused") private int GetPlayoutVolume()
		private int GetPlayoutVolume()
		{

			// create audio manager if needed
			if (_audioManager == null && _context != null)
			{
				_audioManager = (AudioManager) _context.getSystemService(Context.AUDIO_SERVICE);
			}

			int level = -1;

			if (_audioManager != null)
			{
				level = _audioManager.getStreamVolume(AudioManager.STREAM_VOICE_CALL);
			}

			return level;
		}

		private void SetAudioMode(bool startCall)
		{
			int apiLevel = android.os.Build.VERSION.SDK_INT;

			if (_audioManager == null && _context != null)
			{
				_audioManager = (AudioManager) _context.getSystemService(Context.AUDIO_SERVICE);
			}

			if (_audioManager == null)
			{
				DoLogErr("Could not set audio mode - no audio manager");
				return;
			}

			// ***IMPORTANT*** When the API level for honeycomb (H) has been
			// decided,
			// the condition should be changed to include API level 8 to H-1.
			if ((android.os.Build.BRAND.Equals("Samsung") || android.os.Build.BRAND.Equals("samsung")) && (8 == apiLevel))
			{
				// Set Samsung specific VoIP mode for 2.2 devices
				// 4 is VoIP mode
				int mode = (startCall ? 4 : AudioManager.MODE_NORMAL);
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
			Log.d(logTag, msg);
		}

		private void DoLogErr(string msg)
		{
			Log.e(logTag, msg);
		}
	}

}