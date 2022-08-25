using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace GmTileHistory {
	public static class Liner {
		static BasicEffect m_basicEffect;
		static List<VertexPositionColor> m_vertices = new();
		static int m_lineCount = 0;
		public static void Init(Game game) {
			m_basicEffect = new BasicEffect(game.GraphicsDevice);
			m_basicEffect.VertexColorEnabled = true;
			
			m_basicEffect.Projection = Matrix.CreateOrthographicOffCenter
			(0, game.GraphicsDevice.Viewport.Width,     // left, right
			game.GraphicsDevice.Viewport.Height, 0,    // bottom, top
			0, 1);
		}
		public static void PushLine(float x1, float y1, float x2, float y2, Color colour) {
			PushLine(x1, y1, x2, y2, colour, colour);
		}
		public static void PushLine(float x1, float y1, float x2, float y2, Color colour1, Color colour2) {
			m_vertices.Add(new VertexPositionColor(new Vector3(x1, y1, 0), colour1));
			m_vertices.Add(new VertexPositionColor(new Vector3(x2, y2, 0), colour2));
			m_lineCount++;
		}

		public static void PushRect(float x, float y, float width, float height, Color colour) {
			PushLine(x, y, x + width, y, colour);
			PushLine(x + width, y, x + width, y + height, colour);
			PushLine(x + width, y + height, x, y + height, colour);
			PushLine(x, y + height, x, y, colour);
		}

		public static void Flush(Game game) {
			m_basicEffect.CurrentTechnique.Passes[0].Apply();
			game.GraphicsDevice.DrawUserPrimitives<VertexPositionColor>(PrimitiveType.LineList, m_vertices.ToArray(), 0, m_lineCount);
			m_lineCount = 0;
			m_vertices.Clear();
		}
	}
}