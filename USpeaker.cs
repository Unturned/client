using MoPhoGames.USpeak.Codec;
using MoPhoGames.USpeak.Core;
using MoPhoGames.USpeak.Core.Utils;
using MoPhoGames.USpeak.Interface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

[AddComponentMenu("USpeak/USpeaker")]
public class USpeaker : MonoBehaviour
{
	public static float RemoteGain;

	public static float LocalGain;

	public static bool MuteAll;

	public static List<USpeaker> USpeakerList;

	private static int InputDeviceID;

	private static string InputDeviceName;

	public SpeakerMode SpeakerMode;

	public BandMode BandwidthMode;

	public float SendRate = 16f;

	public SendBehavior SendingMode;

	public bool UseVAD;

	public ThreeDMode _3DMode;

	public bool DebugPlayback;

	public bool AskPermission = true;

	public bool Mute;

	public float SpeakerVolume = 1f;

	public float VolumeThreshold = 0.01f;

	public int Codec;

	private USpeakCodecManager codecMgr;

	private AudioClip recording;

	private int recFreq;

	private int lastReadPos;

	private float sendTimer;

	private float sendt = 1f;

	private string[] micDeviceList;

	private float lastDeviceUpdate;

	private List<USpeakFrameContainer> sendBuffer = new List<USpeakFrameContainer>();

	private List<byte> tempSendBytes = new List<byte>();

	private ISpeechDataHandler audioHandler;

	private IUSpeakTalkController talkController;

	private int overlap;

	private USpeakSettingsData settings;

	private string currentDeviceName = string.Empty;

	private float talkTimer;

	private float vadHangover = 0.5f;

	private float lastVTime;

	private List<float[]> pendingEncode = new List<float[]>();

	private double played;

	private int index;

	private double received;

	private float[] receivedData;

	private float playDelay;

	private bool shouldPlay;

	private float lastTime;

	private BandMode lastBandMode;

	private int lastCodec;

	private ThreeDMode last3DMode;

	private int recordedChunkCount;

	private int micFoundDelay;

	private bool waitingToStartRec;

	private int audioFrequency
	{
		get
		{
			if (this.recFreq == 0)
			{
				switch (this.BandwidthMode)
				{
					case BandMode.Narrow:
					{
						this.recFreq = 8000;
						break;
					}
					case BandMode.Wide:
					{
						this.recFreq = 16000;
						break;
					}
					case BandMode.UltraWide:
					{
						this.recFreq = 32000;
						break;
					}
					default:
					{
						this.recFreq = 8000;
						break;
					}
				}
			}
			return this.recFreq;
		}
	}

	[Obsolete("Use USpeaker._3DMode instead")]
	public bool Is3D
	{
		get
		{
			return this._3DMode == ThreeDMode.SpeakerPan;
		}
		set
		{
			if (!value)
			{
				this._3DMode = ThreeDMode.None;
			}
			else
			{
				this._3DMode = ThreeDMode.SpeakerPan;
			}
		}
	}

	public bool IsTalking
	{
		get
		{
			return this.talkTimer > 0f;
		}
	}

	static USpeaker()
	{
		USpeaker.RemoteGain = 1f;
		USpeaker.LocalGain = 1f;
		USpeaker.MuteAll = false;
		USpeaker.USpeakerList = new List<USpeaker>();
		USpeaker.InputDeviceID = 0;
		USpeaker.InputDeviceName = string.Empty;
	}

	public USpeaker()
	{
	}

	private float amplitude(float[] x)
	{
		float single = 0f;
		for (int i = 0; i < (int)x.Length; i++)
		{
			single = Mathf.Max(single, Mathf.Abs(x[i]));
		}
		return single;
	}

	private void Awake()
	{
		USpeaker.USpeakerList.Add(this);
		if (base.audio == null)
		{
			base.gameObject.AddComponent<AudioSource>();
		}
		base.audio.clip = AudioClip.Create("vc", this.audioFrequency * 10, 1, this.audioFrequency, this._3DMode == ThreeDMode.Full3D, false);
		base.audio.loop = true;
		this.receivedData = new float[this.audioFrequency * 10];
		this.codecMgr = USpeakCodecManager.Instance;
		this.lastBandMode = this.BandwidthMode;
		this.lastCodec = this.Codec;
		this.last3DMode = this._3DMode;
	}

	private int CalculateSamplesRead(int readPos)
	{
		if (readPos >= this.lastReadPos)
		{
			return readPos - this.lastReadPos;
		}
		return this.audioFrequency * 10 - this.lastReadPos + readPos;
	}

	private bool CheckVAD(float[] samples)
	{
		if (Time.realtimeSinceStartup < this.lastVTime + this.vadHangover)
		{
			return true;
		}
		float single = 0f;
		float[] singleArray = samples;
		for (int i = 0; i < (int)singleArray.Length; i++)
		{
			float single1 = (float)singleArray[i];
			single = Mathf.Max(single, Mathf.Abs(single1));
		}
		bool volumeThreshold = single >= this.VolumeThreshold;
		if (volumeThreshold)
		{
			this.lastVTime = Time.realtimeSinceStartup;
		}
		return volumeThreshold;
	}

	public void DrawTalkControllerUI()
	{
		if (this.talkController == null)
		{
			GUILayout.Label("No component available which implements IUSpeakTalkController\nReverting to default behavior - data is always sent", new GUILayoutOption[0]);
		}
		else
		{
			this.talkController.OnInspectorGUI();
		}
	}

	private Component FindInputHandler()
	{
		Component[] components = base.GetComponents<Component>();
		for (int i = 0; i < (int)components.Length; i++)
		{
			Component component = components[i];
			if (component is IUSpeakTalkController)
			{
				return component;
			}
		}
		return null;
	}

	private Component FindSpeechHandler()
	{
		Component[] components = base.GetComponents<Component>();
		for (int i = 0; i < (int)components.Length; i++)
		{
			Component component = components[i];
			if (component is ISpeechDataHandler)
			{
				return component;
			}
		}
		return null;
	}

	public static USpeaker Get(UnityEngine.Object source)
	{
		if (source is GameObject)
		{
			return (source as GameObject).GetComponent<USpeaker>();
		}
		if (source is Transform)
		{
			return (source as Transform).GetComponent<USpeaker>();
		}
		if (!(source is Component))
		{
			return null;
		}
		return (source as Component).GetComponent<USpeaker>();
	}

	public void GetInputHandler()
	{
		this.talkController = (IUSpeakTalkController)this.FindInputHandler();
	}

	public void InitializeSettings(int data)
	{
		this.settings = new USpeakSettingsData((byte)data);
		this.Codec = this.settings.Codec;
	}

	private float[] normalize(float[] samples, float magnitude)
	{
		float[] singleArray = new float[(int)samples.Length];
		for (int i = 0; i < (int)samples.Length; i++)
		{
			singleArray[i] = samples[i] / magnitude;
		}
		return singleArray;
	}

	private void OnAudioAvailable(float[] pcmData)
	{
		if (this.UseVAD && !this.CheckVAD(pcmData))
		{
			return;
		}
		foreach (float[] singleArray in this.SplitArray(pcmData, 1280))
		{
			this.pendingEncode.Add(singleArray);
		}
	}

	private void OnDestroy()
	{
		USpeaker.USpeakerList.Remove(this);
	}

	private void ProcessPendingEncode(float[] pcm)
	{
		int num;
		byte[] numArray = USpeakAudioClipCompressor.CompressAudioData(pcm, 1, out num, this.lastBandMode, this.codecMgr.Codecs[this.lastCodec], USpeaker.LocalGain);
		USpeakFrameContainer uSpeakFrameContainer = new USpeakFrameContainer()
		{
			Samples = (ushort)num,
			encodedData = numArray
		};
		this.sendBuffer.Add(uSpeakFrameContainer);
	}

	private void ProcessPendingEncodeBuffer()
	{
		float single = (float)100 / 1000f;
		float single1 = Time.realtimeSinceStartup;
		while (Time.realtimeSinceStartup <= single1 + single && this.pendingEncode.Count > 0)
		{
			float[] item = this.pendingEncode[0];
			this.pendingEncode.RemoveAt(0);
			this.ProcessPendingEncode(item);
		}
	}

	public void ReceiveAudio(byte[] data)
	{
		byte[] num = null;
		if (this.settings == null)
		{
			Debug.LogWarning("Trying to receive remote audio data without calling InitializeSettings!\nIncoming packet will be ignored");
			return;
		}
		if (USpeaker.MuteAll || this.Mute || this.SpeakerMode == SpeakerMode.Local && !this.DebugPlayback)
		{
			return;
		}
		if (this.SpeakerMode == SpeakerMode.Remote)
		{
			this.talkTimer = 1f;
		}
		for (int i = 0; i < (int)data.Length; i = i + (int)num.Length)
		{
			int num1 = BitConverter.ToInt32(data, i);
			num = USpeakPoolUtils.GetByte(num1 + 6);
			Array.Copy(data, i, num, 0, (int)num.Length);
			USpeakFrameContainer uSpeakFrameContainer = new USpeakFrameContainer();
			uSpeakFrameContainer.LoadFrom(num);
			USpeakPoolUtils.Return(num);
			float[] singleArray = USpeakAudioClipCompressor.DecompressAudio(uSpeakFrameContainer.encodedData, (int)uSpeakFrameContainer.Samples, 1, false, this.settings.bandMode, this.codecMgr.Codecs[this.Codec], USpeaker.RemoteGain);
			float length = (float)((int)singleArray.Length) / (float)this.audioFrequency;
			USpeaker uSpeaker = this;
			uSpeaker.received = uSpeaker.received + (double)length;
			Array.Copy(singleArray, 0, this.receivedData, this.index, (int)singleArray.Length);
			USpeakPoolUtils.Return(singleArray);
			USpeaker length1 = this;
			length1.index = length1.index + (int)singleArray.Length;
			if (this.index >= base.audio.clip.samples)
			{
				this.index = 0;
			}
			base.audio.clip.SetData(this.receivedData, 0);
			if (!base.audio.isPlaying)
			{
				this.shouldPlay = true;
				if (this.playDelay <= 0f)
				{
					this.playDelay = length * 5f;
				}
			}
		}
	}

	public static void SetInputDevice(int deviceID)
	{
		USpeaker.InputDeviceID = deviceID;
		USpeaker.InputDeviceName = Microphone.devices[USpeaker.InputDeviceID];
	}

	private List<float[]> SplitArray(float[] array, int size)
	{
		float[] singleArray = null;
		List<float[]> singleArrays = new List<float[]>();
		for (int i = 0; i < (int)array.Length; i = i + (int)singleArray.Length)
		{
			singleArray = array.Skip<float>(i).Take<float>(size).ToArray<float>();
			singleArrays.Add(singleArray);
		}
		return singleArrays;
	}

	private IEnumerator Start() {
		//USpeaker.<Start>c__Iterator0 variable = null;
		return null;
	}

	private void StopPlaying()
	{
		base.audio.Stop();
		base.audio.time = 0f;
		this.index = 0;
		this.played = 0;
		this.received = 0;
		this.lastTime = 0f;
	}

	private void Update()
	{
		if (this.SpeakerMode == SpeakerMode.Local && Time.realtimeSinceStartup >= this.lastDeviceUpdate)
		{
			this.lastDeviceUpdate = Time.realtimeSinceStartup + 2f;
			this.micDeviceList = Microphone.devices;
		}
		USpeaker uSpeaker = this;
		uSpeaker.talkTimer = uSpeaker.talkTimer - Time.deltaTime;
		base.audio.volume = this.SpeakerVolume;
		if (this.last3DMode != this._3DMode)
		{
			this.last3DMode = this._3DMode;
			this.StopPlaying();
			base.audio.clip = AudioClip.Create("vc", this.audioFrequency * 10, 1, this.audioFrequency, this._3DMode == ThreeDMode.Full3D, false);
			base.audio.loop = true;
		}
		if (this._3DMode == ThreeDMode.SpeakerPan)
		{
			Transform transforms = Camera.main.transform;
			Vector3 vector3 = Vector3.Cross(transforms.up, transforms.forward);
			vector3.Normalize();
			float single = Vector3.Dot(base.transform.position - transforms.position, vector3);
			float single1 = Vector3.Dot(base.transform.position - transforms.position, transforms.forward);
			float single2 = Mathf.Sin(Mathf.Atan2(single, single1));
			base.audio.pan = single2;
		}
		if (base.audio.isPlaying)
		{
			if (this.lastTime > base.audio.time)
			{
				USpeaker uSpeaker1 = this;
				uSpeaker1.played = uSpeaker1.played + (double)base.audio.clip.length;
			}
			this.lastTime = base.audio.time;
			if (this.played + (double)base.audio.time >= this.received)
			{
				this.StopPlaying();
				this.shouldPlay = false;
			}
		}
		else if (this.shouldPlay)
		{
			USpeaker uSpeaker2 = this;
			uSpeaker2.playDelay = uSpeaker2.playDelay - Time.deltaTime;
			if (this.playDelay <= 0f)
			{
				base.audio.Play();
			}
		}
		if (this.SpeakerMode == SpeakerMode.Remote)
		{
			return;
		}
		if (this.audioHandler == null)
		{
			return;
		}
		if ((int)this.micDeviceList.Length == 0)
		{
			return;
		}
		if (string.IsNullOrEmpty(USpeaker.InputDeviceName))
		{
			USpeaker.InputDeviceName = this.currentDeviceName;
		}
		if (!string.IsNullOrEmpty(this.currentDeviceName))
		{
			if (USpeaker.InputDeviceName != this.currentDeviceName)
			{
				Microphone.End(this.currentDeviceName);
				MonoBehaviour.print(string.Concat("Using input device: ", USpeaker.InputDeviceName));
				this.currentDeviceName = USpeaker.InputDeviceName;
				this.recording = Microphone.Start(this.currentDeviceName, true, 5, this.audioFrequency);
				this.lastReadPos = 0;
				this.sendBuffer.Clear();
				this.recordedChunkCount = 0;
			}
			if (this.micDeviceList[Mathf.Min(USpeaker.InputDeviceID, (int)this.micDeviceList.Length - 1)] != this.currentDeviceName)
			{
				bool flag = false;
				for (int i = 0; i < (int)Microphone.devices.Length; i++)
				{
					if (this.micDeviceList[i] == this.currentDeviceName)
					{
						USpeaker.InputDeviceID = i;
						flag = true;
					}
				}
				if (!flag)
				{
					USpeaker.InputDeviceID = 0;
					USpeaker.InputDeviceName = this.micDeviceList[0];
					this.currentDeviceName = this.micDeviceList[0];
					MonoBehaviour.print(string.Concat("Device unplugged, switching to: ", this.currentDeviceName));
					this.recording = Microphone.Start(this.currentDeviceName, true, 5, this.audioFrequency);
					this.lastReadPos = 0;
					this.sendBuffer.Clear();
					this.recordedChunkCount = 0;
				}
			}
		}
		else if (!this.waitingToStartRec)
		{
			this.waitingToStartRec = true;
			this.micFoundDelay = 5;
		}
		else
		{
			USpeaker uSpeaker3 = this;
			uSpeaker3.micFoundDelay = uSpeaker3.micFoundDelay - 1;
			if (this.micFoundDelay <= 0)
			{
				this.micFoundDelay = 0;
				this.waitingToStartRec = false;
				MonoBehaviour.print(string.Concat("New device found: ", this.currentDeviceName));
				USpeaker.InputDeviceID = 0;
				USpeaker.InputDeviceName = this.micDeviceList[0];
				this.currentDeviceName = this.micDeviceList[0];
				this.recording = Microphone.Start(this.currentDeviceName, true, 5, this.audioFrequency);
				this.lastReadPos = 0;
				this.sendBuffer.Clear();
				this.recordedChunkCount = 0;
				this.UpdateSettings();
			}
		}
		if (this.lastBandMode != this.BandwidthMode || this.lastCodec != this.Codec)
		{
			this.UpdateSettings();
			this.lastBandMode = this.BandwidthMode;
			this.lastCodec = this.Codec;
		}
		if (this.recording == null)
		{
			return;
		}
		int position = Microphone.GetPosition(this.currentDeviceName);
		if (position + this.recording.samples * this.recordedChunkCount < this.lastReadPos)
		{
			USpeaker uSpeaker4 = this;
			uSpeaker4.recordedChunkCount = uSpeaker4.recordedChunkCount + 1;
		}
		position = position + this.recording.samples * this.recordedChunkCount;
		if (position <= this.overlap)
		{
			return;
		}
		bool flag1 = (this.talkController == null ? true : this.talkController.ShouldSend());
		try
		{
			int num = position - this.lastReadPos;
			int sampleSize = this.codecMgr.Codecs[this.Codec].GetSampleSize(this.audioFrequency);
			if (sampleSize == 0)
			{
				sampleSize = 100;
			}
			int num1 = this.lastReadPos;
			int num2 = Mathf.FloorToInt((float)(num / sampleSize));
			for (int j = 0; j < num2; j++)
			{
				float[] singleArray = USpeakPoolUtils.GetFloat(sampleSize);
				this.recording.GetData(singleArray, num1 % this.recording.samples);
				if (flag1)
				{
					this.talkTimer = 1f;
					this.OnAudioAvailable(singleArray);
				}
				USpeakPoolUtils.Return(singleArray);
				num1 = num1 + sampleSize;
			}
			this.lastReadPos = num1;
		}
		catch (Exception exception)
		{
		}
		this.ProcessPendingEncodeBuffer();
		bool flag2 = true;
		if (this.SendingMode == SendBehavior.RecordThenSend && this.talkController != null)
		{
			flag2 = !flag1;
		}
		USpeaker uSpeaker5 = this;
		uSpeaker5.sendTimer = uSpeaker5.sendTimer + Time.deltaTime;
		if (this.sendTimer >= this.sendt && flag2)
		{
			this.sendTimer = 0f;
			this.tempSendBytes.Clear();
			foreach (USpeakFrameContainer uSpeakFrameContainer in this.sendBuffer)
			{
				this.tempSendBytes.AddRange(uSpeakFrameContainer.ToByteArray());
			}
			this.sendBuffer.Clear();
			if (this.tempSendBytes.Count > 0)
			{
				this.audioHandler.USpeakOnSerializeAudio(this.tempSendBytes.ToArray());
			}
		}
	}

	private void UpdateSettings()
	{
		if (!Application.isPlaying)
		{
			return;
		}
		this.settings = new USpeakSettingsData()
		{
			bandMode = this.BandwidthMode,
			Codec = this.Codec
		};
		this.audioHandler.USpeakInitializeSettings((int)this.settings.ToByte());
	}
}