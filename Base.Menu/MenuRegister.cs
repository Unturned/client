using System;
using UnityEngine;

public class MenuRegister : MonoBehaviour
{
	public readonly static int ERROR_TIMEOUT;

	public static Transform lerpTo;

	public static SleekWindow window;

	public static SleekContainer container;

	public static SleekBox boxConnection;

	public static SleekImage iconConnection;

	public static SleekButton buttonCancel;

	public static SleekImage iconCancel;

	public static SleekButton buttonQuit;

	public static SleekImage iconQuit;

	private static float startedError;

	private static bool lerped;

	private Vector3 position;

	private Quaternion rotation;

	static MenuRegister()
	{
		MenuRegister.ERROR_TIMEOUT = 2;
		MenuRegister.startedError = Single.MaxValue;
	}

	public MenuRegister()
	{
	}

	public void Awake() {
		if (!MenuRegister.lerped) {
			MenuRegister.lerped = true;
			base.transform.position = base.transform.parent.FindChild("viewStart").position;
			base.transform.rotation = base.transform.parent.FindChild("viewStart").rotation;
			MenuRegister.lerpTo = base.transform.parent.FindChild("viewStart");
		}
		
		AudioListener.volume = GameSettings.volume;
		NetworkEvents.clear();
		
		MenuRegister.window = new SleekWindow();
		MenuRegister.container = new SleekContainer() {
			size = new Coord2(0, 0, 1f, 1f)
		};
		
		MenuRegister.window.addFrame(MenuRegister.container);
		MenuRegister.boxConnection = new SleekBox() {
			position = new Coord2(-200, -20, 1.5f, 0.5f),
			size = new Coord2(310, 40, 0f, 0f)
		};
		
		MenuRegister.window.addFrame(MenuRegister.boxConnection);
		MenuRegister.iconConnection = new SleekImage() {
			position = new Coord2(4, 4, 0f, 0f),
			size = new Coord2(32, 32, 0f, 0f)
		};
		
		MenuRegister.boxConnection.addFrame(MenuRegister.iconConnection);
		MenuRegister.buttonCancel = new SleekButton() {
			position = new Coord2(0, 10, 0f, 1f),
			size = new Coord2(0, 40, 1f, 0f),
			text = "Cancel"
		};
		
		MenuRegister.buttonCancel.onUsed += new SleekDelegate(MenuRegister.usedCancel);
		MenuRegister.boxConnection.addFrame(MenuRegister.buttonCancel);
		MenuRegister.buttonCancel.visible = false;
		MenuRegister.iconCancel = new SleekImage() {
			position = new Coord2(4, 4, 0f, 0f),
			size = new Coord2(32, 32, 0f, 0f)
		};
		
		MenuRegister.iconCancel.setImage("Textures/Icons/back");
		MenuRegister.buttonCancel.addFrame(MenuRegister.iconCancel);
		MenuRegister.buttonQuit = new SleekButton() {
			position = new Coord2(10, -50, 0f, 1f),
			size = new Coord2(200, 40, 0f, 0f),
			text = Texts.LABEL_QUIT
		};
		
		MenuRegister.buttonQuit.onUsed += new SleekDelegate(MenuRegister.usedQuit);
		MenuRegister.container.addFrame(MenuRegister.buttonQuit);
		MenuRegister.iconQuit = new SleekImage() {
			position = new Coord2(4, 4, 0f, 0f),
			size = new Coord2(32, 32, 0f, 0f)
		};
		
		MenuRegister.iconQuit.setImage("Textures/Icons/quit");
		MenuRegister.buttonQuit.addFrame(MenuRegister.iconQuit);
		MenuRegister.open();
		
		if (PlayerSettings.id != string.Empty) {
			MenuRegister.close();
		}
		
		MenuGraphics.load();
		Sun.tool.cycle();
		
		if (GameSettings.music) {
			base.audio.Play();
		}
		
		// Here's the message from the server...
		if (NetworkChat.notification != string.Empty) {
			MenuRegister.openError(NetworkChat.notification, "Textures/Icons/error");
			NetworkChat.notification = string.Empty;
		}
	}

	public static void close()
	{
		MenuRegister.container.position = Coord2.ZERO;
		MenuRegister.container.lerp(new Coord2(0, 0, -1f, 0f), MenuRegister.container.size, 4f);
		MenuTitle menuTitle = new MenuTitle();
		MenuTitle.open();
	}

	public static void closeError()
	{
		MenuRegister.boxConnection.position = new Coord2(-155, -20, 0.5f, 0.5f);
		MenuRegister.boxConnection.lerp(new Coord2(-155, -20, -0.5f, 0.5f), MenuRegister.boxConnection.size, 4f);
		MenuRegister.container.visible = true;
	}

	public static void closeInfo()
	{
		MenuRegister.boxConnection.position = new Coord2(-155, -20, 0.5f, 0.5f);
		MenuRegister.boxConnection.lerp(new Coord2(-155, -20, -0.5f, 0.5f), MenuRegister.boxConnection.size, 4f);
	}

	public void OnGUI()
	{
		if (!LoadingScreen.loading) {
			MenuRegister.window.drawFrame();
			if (MenuKeys.binding != -1)
			{
				if (Event.current.type == EventType.KeyDown)
				{
					if (Event.current.keyCode != KeyCode.Escape)
					{
						MenuKeys.bind(Event.current.keyCode);
					}
					else
					{
						MenuKeys.bind(KeyCode.None);
					}
				}
				else if (Event.current.type == EventType.MouseDown)
				{
					if (Event.current.button == 0)
					{
						MenuKeys.bind(KeyCode.Mouse0);
					}
					if (Event.current.button == 1)
					{
						MenuKeys.bind(KeyCode.Mouse1);
					}
					if (Event.current.button == 2)
					{
						MenuKeys.bind(KeyCode.Mouse2);
					}
					if (Event.current.button == 3)
					{
						MenuKeys.bind(KeyCode.Mouse3);
					}
					if (Event.current.button == 4)
					{
						MenuKeys.bind(KeyCode.Mouse4);
					}
					if (Event.current.button == 5)
					{
						MenuKeys.bind(KeyCode.Mouse5);
					}
					if (Event.current.button == 6)
					{
						MenuKeys.bind(KeyCode.Mouse6);
					}
				}
				else if (Event.current.shift)
				{
					MenuKeys.bind(KeyCode.LeftShift);
				}
			}
		}
	}

	public static void open() {
		MenuRegister.container.position = new Coord2(0, 0, 1f, 0f);
		MenuRegister.container.lerp(Coord2.ZERO, MenuRegister.container.size, 4f);
	}

	public static void openError(string text, string icon) {
		MenuRegister.startedError = Time.realtimeSinceStartup;
		MenuRegister.boxConnection.text = text;
		MenuRegister.iconConnection.setImage(icon);
		MenuRegister.boxConnection.position = new Coord2(-155, -20, -0.5f, 0.5f);
		MenuRegister.boxConnection.lerp(new Coord2(-155, -20, 0.5f, 0.5f), MenuRegister.boxConnection.size, 4f);
		MenuRegister.container.visible = false;
	}

	public static void openInfo(string text, string icon) {
		MenuRegister.boxConnection.text = text;
		MenuRegister.iconConnection.setImage(icon);
		MenuRegister.boxConnection.position = new Coord2(-155, -20, -0.5f, 0.5f);
		MenuRegister.boxConnection.lerp(new Coord2(-155, -20, 0.5f, 0.5f), MenuRegister.boxConnection.size, 4f);
	}

	public void Update() {
		if (PlayerSettings.id != string.Empty && MenuRegister.lerpTo != null) {
			base.transform.position = Vector3.Lerp(base.transform.position, MenuRegister.lerpTo.position, Time.deltaTime * 0.66f);
			base.transform.rotation = Quaternion.Lerp(base.transform.rotation, MenuRegister.lerpTo.rotation, Time.deltaTime * 0.66f);
		}
		
		if (Time.realtimeSinceStartup - MenuRegister.startedError > (float)MenuRegister.ERROR_TIMEOUT) {
			MenuRegister.startedError = Single.MaxValue;
			MenuRegister.closeError();
		}
		
		if (Time.realtimeSinceStartup - MenuGraphics.lastRes > 1f && !Input.GetMouseButtonDown(0)) {
			Debug.Log(":o");
			MenuGraphics.lastRes = Single.MaxValue;
			GraphicsSettings.apply();
		}
		
		if (MenuTitle.labelNews != null) {
			MenuTitle.labelNews.text = string.Concat(new string[] { "Version: ", Texts.VERSION_ID, "    Time: ", Sun.getTime(), "\n", Texts.NEWS });
		}
		
		Screen.lockCursor = false;
	}

	public static void usedCancel(SleekFrame frame) {
		NetworkTools.disconnect();
		MenuPlay.open();
		MenuRegister.closeError();
	}

	public static void usedQuit(SleekFrame frame) {
		Application.Quit();
	}
}