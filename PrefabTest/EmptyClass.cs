using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using NUnit.Framework;

		public class EmptyClass
		{
			public static void Main(String[] args) {
				Stream stream = File.Open("player.prefab", FileMode.OpenOrCreate);
						BinaryFormatter bFormatter = new BinaryFormatter();
						UnityEngine.Object obj = (UnityEngine.Object)bFormatter.Deserialize(stream);
						stream.Close();
				}
		}
