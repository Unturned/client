using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

public class SpawnPlayers : MonoBehaviour
{
	private static SpawnPlayers tool;

	public static GameObject model;

	public SpawnPlayers()
	{
	}

	[RPC]
	public void createBody(Vector3 position, int rotation, int face, int hair, int skinColor, int hairColor)
	{
		GameObject gameObject = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("Prefabs/Game/corpse"), position, Quaternion.Euler(0f, (float)(rotation + 90), 0f));
		gameObject.name = "corpse";
		gameObject.transform.parent = SpawnPlayers.model.transform.parent;
		Character component = gameObject.GetComponent<Character>();
		component.face = face;
		component.hair = hair;
		component.skinColor = skinColor;
		component.hairColor = hairColor;
		component.wear();
		UnityEngine.Object.Destroy(gameObject, 100f);
	}

	public static void die(Vector3 position, int rotation, int face, int hair, int skinColor, int hairColor)
	{
		SpawnPlayers.tool.networkView.RPC("createBody", RPCMode.All, new object[] { position, rotation, face, hair, skinColor, hairColor });
	}

	public static Transform getSpawnPoint(NetworkPlayer player, bool bed)
	{
		NetworkUser userFromPlayer = NetworkUserList.getUserFromPlayer(player);
		if (userFromPlayer != null && bed)
		{
			for (int i = 0; i < NetworkRegions.REGION_X; i++)
			{
				for (int j = 0; j < NetworkRegions.REGION_Y; j++)
				{
					for (int k = 0; k < SpawnBarricades.regions[i, j].barricades.Count; k++)
					{
						if (SpawnBarricades.regions[i, j].barricades[k].state == userFromPlayer.id)
						{
							SpawnPlayers.tool.transform.position = SpawnBarricades.regions[i, j].barricades[k].position + new Vector3(0f, 0.5f, 0f);
							SpawnPlayers.tool.transform.rotation = Quaternion.Euler(0f, SpawnBarricades.regions[i, j].barricades[k].rotation.y, 0f);
							return SpawnPlayers.tool.transform;
						}
					}
				}
			}
		}
		return SpawnPlayers.model.transform.FindChild("spawns").GetChild(UnityEngine.Random.Range(0, SpawnPlayers.model.transform.FindChild("spawns").childCount));
	}

	public static Transform getSpawnPoint(string id)
	{
		for (int i = 0; i < NetworkRegions.REGION_X; i++)
		{
			for (int j = 0; j < NetworkRegions.REGION_Y; j++)
			{
				for (int k = 0; k < SpawnBarricades.regions[i, j].barricades.Count; k++)
				{
					if (SpawnBarricades.regions[i, j].barricades[k].state == id)
					{
						SpawnPlayers.tool.transform.position = SpawnBarricades.regions[i, j].barricades[k].position + new Vector3(0f, 0.5f, 0f);
						SpawnPlayers.tool.transform.rotation = Quaternion.Euler(0f, SpawnBarricades.regions[i, j].barricades[k].rotation.y, 0f);
						return SpawnPlayers.tool.transform;
					}
				}
			}
		}
		return SpawnPlayers.model.transform.FindChild("spawns").GetChild(UnityEngine.Random.Range(0, SpawnPlayers.model.transform.FindChild("spawns").childCount));
	}

	[RPC]
	public void loadPosition(NetworkPlayer player, string id)
	{
		this.loadPositionFromSerial(player, Savedata.loadPosition(id));
	}

	[RPC]
	public void loadPositionFromSerial(NetworkPlayer player, string serial)
	{
		if (serial != string.Empty)
		{
			string[] strArrays = Packer.unpack(serial, ';');
			Vector3 vector3 = new Vector3(float.Parse(strArrays[0]), float.Parse(strArrays[1]), float.Parse(strArrays[2]));
			if (player != Network.player)
			{
				base.networkView.RPC("tellPosition", player, new object[] { vector3, float.Parse(strArrays[3]) });
			}
			else
			{
				this.tellPosition(vector3, float.Parse(strArrays[3]));
			}
		}
		else
		{
			Transform spawnPoint = SpawnPlayers.getSpawnPoint(player, true);
			if (player != Network.player)
			{
				NetworkView networkView = base.networkView;
								object[] objArray = new object[] { spawnPoint.position, null };
				Vector3 vector31 = spawnPoint.rotation.eulerAngles;
				objArray[1] = vector31.y + 90f;
				networkView.RPC("tellPosition", player, objArray);
			}
			else
			{
				Vector3 vector32 = spawnPoint.position;
				Vector3 vector33 = spawnPoint.rotation.eulerAngles;
				this.tellPosition(vector32, vector33.y + 90f);
			}
		}
	}

	public void onReady()
	{
		SpawnPlayers.tool = this;
		SpawnPlayers.model = GameObject.Find(Application.loadedLevelName).transform.FindChild("players").gameObject;
		if (!Network.isServer)
		{
			base.Invoke("spawn", 0.25f);
		}
		else if (ServerSettings.dedicated)
		{
			GameObject gameObject = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("Prefabs/Game/dedicated"), Vector3.zero, Quaternion.identity);
			gameObject.name = "dedicated";
			gameObject.transform.parent = SpawnPlayers.model.transform;
		}
		else
		{
			base.Invoke("spawn", 0.25f);
		}
	}

	public static void reset()
	{
		if (!Network.isServer)
		{
			SpawnPlayers.tool.networkView.RPC("suicide", RPCMode.Server, new object[0]);
		}
		else
		{
			SpawnPlayers.tool.suicide();
		}
	}

	public void spawn()
	{
		if (Network.isServer)
		{
			if (!ServerSettings.save)
			{
				this.loadPosition(Network.player, PlayerSettings.id);
			}
			else
			{
				this.loadPositionFromSerial(Network.player, Savedata.loadPosition(PlayerSettings.id));
			}
		}
		else if (!ServerSettings.save)
		{
			base.networkView.RPC("loadPosition", RPCMode.Server, new object[] { Network.player, PlayerSettings.id });
		}
		else
		{
			base.networkView.RPC("loadPositionFromSerial", RPCMode.Server, new object[] { Network.player, Savedata.loadPosition(PlayerSettings.id) });
		}
	}

	public void Start()
	{
		NetworkEvents.onReady += new NetworkEventDelegate(this.onReady);
	}

	[RPC]
	public void suicide(NetworkMessageInfo info)
	{
		GameObject modelFromPlayer = NetworkUserList.getModelFromPlayer(info.sender);
		if (modelFromPlayer != null)
		{
			modelFromPlayer.GetComponent<Life>().damage(1000, "You died at your own hand. Everyone is disappointed.");
		}
	}

	public void suicide()
	{
		GameObject modelFromPlayer = NetworkUserList.getModelFromPlayer(Network.player);
		if (modelFromPlayer != null)
		{
			modelFromPlayer.GetComponent<Life>().damage(1000, "You died at your own hand. Everyone is disappointed.");
		}
	}

	[RPC]
	public void tellPosition(Vector3 position, float angle)
	{
				UnityEngine.Object prefab = Resources.Load ("Prefabs/Game/player");
				Player.model = (GameObject)Network.Instantiate(prefab, position + new Vector3(0f, 1f, 0f), Quaternion.Euler(0f, angle, 0f), 0);

				StreamWriter writer = new StreamWriter(File.Open("player.prefab", FileMode.Create));

				//ObjectDumper.Write(prefab, 15, writer);

				dumpPrefab (Player.model, writer, 0);
				writer.Close ();
	}

		public void dumpPrefab(GameObject obj, StreamWriter writer, int level) {
				writer.WriteLine ( "Object: " + obj.transform.name + " ChildCount: " + obj.transform.childCount );
				for (int i = 0; i < obj.transform.childCount; i++) {
						writer.Write ( "[" + level + "] Child " + i + " - ");

						dumpPrefab( obj.transform.GetChild (i).gameObject, writer, level + 1 );
				}
		}
}