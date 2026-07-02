using System.IO;
using UnityEditor.Android;
using System.Text.RegularExpressions;

public class ApplicationBuildProcessor : IPostGenerateGradleAndroidProject
{
	public int callbackOrder => 0;

	public void OnPostGenerateGradleAndroidProject(string path)
	{
		try
		{
			var gradleRoot = Directory.GetParent(path)?.FullName ?? path;
			var sdk = System.Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
			var ndk = System.Environment.GetEnvironmentVariable("ANDROID_NDK_ROOT");
			if (!string.IsNullOrEmpty(sdk))
			{
				var properties = "sdk.dir=" + sdk.Replace('\\', '/') + System.Environment.NewLine;
				if (!string.IsNullOrEmpty(ndk))
					properties += "ndk.dir=" + ndk.Replace('\\', '/') + System.Environment.NewLine;
				File.WriteAllText(Path.Combine(gradleRoot, "local.properties"), properties);
			}

			var files = Directory.GetFiles(path, _androidManifest, SearchOption.AllDirectories);
			foreach (var filename in files)
			{
				var data = File.ReadAllText(filename);
				var result = Regex.Replace(data, "android:screenOrientation=\"\\w+\"", "android:screenOrientation=\"sensorLandscape\"");

				if (data != result)
					File.WriteAllText(filename, result);
			}
		}
		catch (System.Exception e)
		{
			UnityEngine.Debug.LogException(e);
		}
	}

	const string _androidManifest = "AndroidManifest.xml";
}
