using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using System.Text;
using System.Runtime.CompilerServices;

namespace GmTileHistory {
	public enum EventType : uint {
		Colour,
		CreationCode,
		ScaledRect,
		IdDiscrepancy,
		UvBleed,
		ObjectRotation
	}
	public class EventManager {
		Texture2D m_texture;

		const int FONTSTARTX = 128;
		const int FONTSTARTY = 48;

		const int RECTSAGOX = 128;
		const int RECTSAGOY = 64;
		const int RECTSAGOWIDTH = 51;
		const int RECTSAGOHEIGHT = 12;

		const int TEXTWIDTH = 112;
		const int TEXTHEIGHT = 48;
		const int TEXTSTARTX = 0;
		const int TEXTSTARTY = 0;

		public Vector2 m_eventFeedPos;

		public class Event {
			public EventType m_type; //What kind of event
			public int m_index; //Index of tile/object
			public double height = 0d; //for funny anim
			public double age = 0d; //lifetime
		}
		public List<Event> m_eventList = new();
		public EventManager(in GraphicsDeviceManager gdm, Vector2 feedPos) {
			Util.LoadTexture(in gdm, out m_texture, "EventText.png");
			m_eventFeedPos = feedPos;
		}
		public void AddEvent(EventType evType, int index) {
			m_eventList.Add(new Event() { m_type = evType, m_index = index});
		}
		public void Update(in GameTime gameTime, in SpriteBatch spriteBatch, int indexCurrent) {
			double heightOffset = 0;
			for (var i = m_eventList.Count - 1; i >= 0; i--) {
				var evt = m_eventList[i];
				if(evt.age >= 5d && evt.height <= 0d) { //clear old enough entries with their animations also done
					m_eventList.RemoveAt(i);
					continue;
				}
				spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, SamplerState.PointWrap, null, null, null, Matrix.CreateScale(1, (float)evt.height / TEXTHEIGHT, 1) * Matrix.CreateTranslation(m_eventFeedPos.X, m_eventFeedPos.Y + (float)heightOffset, 0));
				spriteBatch.Draw(m_texture, Vector2.Zero, new Rectangle(TEXTSTARTX, TEXTSTARTY + TEXTHEIGHT * (int)evt.m_type, TEXTWIDTH, TEXTHEIGHT), Color.White);

				if(evt.m_index != indexCurrent) {
					DrawNumbers(in spriteBatch, indexCurrent - evt.m_index, TEXTWIDTH, 0);
					spriteBatch.Draw(m_texture, new Vector2(TEXTWIDTH, 12), new Rectangle(RECTSAGOX, RECTSAGOY, RECTSAGOWIDTH, RECTSAGOHEIGHT), Color.White);
				}

				heightOffset += evt.height;
				if(evt.age >= 5d) {
					evt.height -= TEXTHEIGHT * (float)gameTime.ElapsedGameTime.TotalSeconds; //a second to hide text
				} else {
					evt.height = Math.Min(evt.height + TEXTHEIGHT*2 * gameTime.ElapsedGameTime.TotalSeconds, TEXTHEIGHT); //half a second to show text
					evt.age += gameTime.ElapsedGameTime.TotalSeconds;
				}
				spriteBatch.End();
			}
		}
		public void DrawNumbers(in SpriteBatch spriteBatch, int number, float x, float y) {
			string numberString = number.ToString();
			int width = 0;
			int i = 0;
			//spriteBatch.Begin();
			for(i = 0; i < numberString.Length; i++) {
				var rect = new Rectangle(FONTSTARTX + 8 * ((byte)(numberString[i]) - 48), FONTSTARTY, 8, 10);
				if (i == 0) {
					if(numberString[0] == '-') {
						rect.X = FONTSTARTX - 8; //minus sign is here
					}
				}
				spriteBatch.Draw(m_texture, new Vector2(x + width, y), rect, Color.White);
				width += 8;
			}
			//spriteBatch.End();
			
		}
	}
}
