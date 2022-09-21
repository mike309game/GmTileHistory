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

		const int ROOMSAGOX = 128;
		const int ROOMSAGOY = 80;
		const int ROOMSAGOWIDTH = 59;
		const int ROOMSAGOHEIGHT = 12;

		const int TEXTWIDTH = 112;
		const int TEXTHEIGHT = 48;
		const int TEXTSTARTX = 0;
		const int TEXTSTARTY = 0;

		const double MAXAGE = 1d * 60d; //1 min

		public class Event {
			public EventType type; //What kind of event
			public int index; //Index of tile/object
			public double height = 0d; //for funny anim
			public double age = 0d; //lifetime
			public int room;
		}
		public List<Event> m_eventList = new();
		public EventManager(in GraphicsDeviceManager gdm) {
			Util.LoadTexture(in gdm, out m_texture, "EventText.png");
		}
		public void AddEvent(EventType evType, int index, int room) {
			m_eventList.Add(new Event() { type = evType, index = index, room = room});
		}
		public void Update(in GameTime gameTime, in SpriteBatch spriteBatch, int indexCurrent, int roomCurrent, Vector2 pos) {
			double heightOffset = 0;
			for (var i = m_eventList.Count - 1; i >= 0; i--) {
				var evt = m_eventList[i];
				if(evt.age >= MAXAGE && evt.height <= 0d) { //clear old enough entries with their animations also done
					m_eventList.RemoveAt(i);
					continue;
				}
				spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, SamplerState.PointWrap, null, null, null, Matrix.CreateScale(1, (float)evt.height / TEXTHEIGHT, 1) * Matrix.CreateTranslation(pos.X, pos.Y + (float)heightOffset, 0));
				spriteBatch.Draw(m_texture, Vector2.Zero, new Rectangle(TEXTSTARTX, TEXTSTARTY + TEXTHEIGHT * (int)evt.type, TEXTWIDTH, TEXTHEIGHT), Color.White);

				if(evt.room == roomCurrent) {
					if (evt.index != indexCurrent) {
						DrawNumbers(in spriteBatch, indexCurrent - evt.index, TEXTWIDTH, 0);
						spriteBatch.Draw(m_texture, new Vector2(TEXTWIDTH, 12), new Rectangle(RECTSAGOX, RECTSAGOY, RECTSAGOWIDTH, RECTSAGOHEIGHT), Color.White);
					}
				} else {
					DrawNumbers(in spriteBatch, roomCurrent - evt.room, TEXTWIDTH, 0);
					spriteBatch.Draw(m_texture, new Vector2(TEXTWIDTH, 12), new Rectangle(ROOMSAGOX, ROOMSAGOY, ROOMSAGOWIDTH, ROOMSAGOHEIGHT), Color.White);
				}

				heightOffset += evt.height;
				if(evt.age >= MAXAGE) {
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
			//spriteBatch.Begin();
			for(int i = 0; i < numberString.Length; i++) {
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
