using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;

namespace GmTileHistory {
	public static class Util {
		static string exeDir = AppDomain.CurrentDomain.BaseDirectory;
		public static void LoadTexture(in GraphicsDeviceManager gdm, out Texture2D texture, string path) {
			FileStream fs = new FileStream($"{exeDir}\\{path}", FileMode.Open);
			texture = Texture2D.FromStream(gdm.GraphicsDevice, fs);
			fs.Dispose();
			fs.Close();
		}
	}
}
