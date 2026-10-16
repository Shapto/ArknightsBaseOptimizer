using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;

namespace ArknightsBaseOptimizer.Data
{
	public static class AvatarCache
	{
		private const string AvatarUrlTemplate = "https://raw.githubusercontent.com/PuppiizSunniiz/Arknight-Images/main/avatars/{0}.png";
		private static readonly HttpClient httpClient = new();
		private static readonly string cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArknightsBaseOptimizer", "Avatars");

		/// <summary>
		/// Returns the operator's square avatar, downloading it first if it isn't cached yet. Returns null if offline and not cached.
		/// </summary>
		public static async Task<BitmapImage?> GetAvatarAsync(string charId)
		{
			string filePath = Path.Combine(cacheFolder, charId + ".png");

			if (!File.Exists(filePath))
			{
				try
				{
					Directory.CreateDirectory(cacheFolder);
					byte[] imageBytes = await httpClient.GetByteArrayAsync(string.Format(AvatarUrlTemplate, charId));
					await File.WriteAllBytesAsync(filePath, imageBytes);
				}
				catch (Exception exception) when (exception is HttpRequestException or IOException)
				{
					return null;
				}
			}

			return LoadFromFile(filePath);
		}

		/// <summary>
		/// Loads an image file fully into memory so the file isn't kept locked, and freezes it so any thread can use it.
		/// </summary>
		private static BitmapImage LoadFromFile(string filePath)
		{
			BitmapImage image = new();
			image.BeginInit();
			image.CacheOption = BitmapCacheOption.OnLoad;
			image.UriSource = new Uri(filePath);
			image.EndInit();
			image.Freeze();
			return image;
		}
	}
}
