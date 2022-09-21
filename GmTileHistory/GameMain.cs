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
	[Flags]
	enum RoomViewerFlags : int {
		ClampTileRects = 1 << 0, //Clamp tile rects, because it's possible for them to go out of the background rect
		FollowBlacklist = 1 << 1, //Follow the user defined blacklist
		BuggyDepthSorting = 1 << 2, //Enable buggy depth sorting because xna's sorter is funny
		ShowObjects = 1 << 3, //Show objects or not
		UseSeparateSliderForObjects = 1 << 4, //Use a separate slider for controlling count of objects
		ShowTilePalette = 1 << 5, //Show tile palette of latest tile
		ShowRectSource = 1 << 6, //Show the source of the latest rect
		AutoResetAnim = 1 << 7, //Auto restart animation when pressing the button to begin animation
		FollowLatestRect = 1 << 8, //Follow the latest rect
		ShowOverlay = 1 << 9, //Show info overlay
		ShowMainWindow = 1 << 10, //Show main window lol

		IsAnimating = 1 << 20, //Is animation going on
		IsVideoMode = 1 << 21, //Auto advance rooms, with a delay between when advancing
		VideoModeDelaying = 1 << 22,
		Fullscreen = 1 << 31
	}

	[Flags]
	enum EventRecord {
		HasColour = 1 << 0,
		CreationCode = 1 << 1,
		ScaledRect = 1 << 2,
		IdDiscrepancy = 1 << 3,
		UvBleed = 1 << 4,
		Rotation = 1 << 5,
	}

	class GameMain : Game {
		GraphicsDeviceManager m_gdm;
		SpriteBatch m_spriteBatch;
		int gameResX = 1360;
		int gameResY = 860;

		GMDataReader m_dataReader;
		GMData m_data;
		string wadPath = "";

		List<Texture2D> m_textures = new();
		Dictionary<int, Texture2D> m_textureDict = new();

		Texture2D m_missingTexture;
		Texture2D m_backgroundTexture;
		Texture2D m_oobText;
		Texture2D m_filePathInstruction;
		IntPtr m_filePathInstructonImgui;

		//Cache
		GMUniquePointerList<GMTexturePage> m_texturePageChunk;
		GMUniquePointerList<GMBackground> m_backgroundChunk;
		GMUniquePointerList<GMRoom> m_roomChunk;
		GMPointerList<GMTextureItem> m_texItemChunk;
		GMUniquePointerList<GMObject> m_objectChunk;
		GMUniquePointerList<GMSprite> m_spriteChunk;
		GMUniquePointerList<GMCode> m_codeChunk;
		GMRoom m_currentRoom;
		GMUniquePointerList<GMRoom.Tile> m_currentRoomTiles;
		GMUniquePointerList<GMRoom.GameObject> m_currentRoomObjects;
		List<Rect> m_tileRects = new();
		List<Rect> m_objectRects = new();

		float m_tileCap = 99999999;
		int m_objectCap = 999999999;
		float m_animationSpeed = 10.0f; //Tiles per second
		double m_videoModeTimer = 0f;
		bool m_videoModeFlag = false; //if it's false then it's waiting to switch room, if it's true then it's waiting to begin the room's animation

		List<int> m_blacklistedBackgrounds = new();
		//For dumb stupid reasons csharp does not support static locals, so these had to be put here
		string m_blacklistSubjectName = "";
		int m_blacklistSelectedItem = 0;

		string[] m_roomNames;
		int m_selectedRoom;

		const RoomViewerFlags m_startFlags = (RoomViewerFlags.ClampTileRects |
			RoomViewerFlags.FollowBlacklist |
			RoomViewerFlags.ShowObjects |
			RoomViewerFlags.AutoResetAnim |
			RoomViewerFlags.ShowOverlay |
			RoomViewerFlags.ShowRectSource |
			RoomViewerFlags.ShowMainWindow);

		public RoomViewerFlags m_flags = m_startFlags;
		RoomViewerFlags m_flagBackup = m_startFlags;

		EventRecord m_eventsToRecord = (
			EventRecord.HasColour |
			EventRecord.CreationCode |
			EventRecord.IdDiscrepancy |
			EventRecord.Rotation |
			EventRecord.ScaledRect |
			EventRecord.UvBleed
		);

		ImGuiRenderer m_imRenderer;
		ImGuiIOPtr m_io;

		float m_scale = 1;
		float m_videoScale = 1;

		Vector2 m_tilemapTransform = new(0, 0);

		//Tile palette
		Vector2 m_tilePalettePos = new(0, 0); //position of tile palette
		Vector2 m_tilePaletteSize = new(200, 200); //width and height of visible palette rect
		Vector2 m_tilePaletteOffset = new(0, 0); //view offset of the tile palette

		Texture2D m_borderTexture;
		Vector2 m_borderSegmentSize = new(10, 10); //size of 9slice segments

		EventManager m_eventManager;

		void DrawBorder(float x, float y, float width, float height, Color colour, float xOffset, float yOffset) {
			m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointWrap, null, RasterizerState.CullNone, null, Matrix.CreateTranslation(x, y, 0));

			/*************************************************************************
			 * CORNERS
			 *************************************************************************/

			//top left
			m_spriteBatch.Draw(
				m_borderTexture, //Texture
				-m_borderSegmentSize, //Position
				new Rectangle(0, 0, (int)m_borderSegmentSize.X, (int)m_borderSegmentSize.Y), //Source rect
				colour, //Colour
				0, //Rot
				Vector2.Zero, //Origin
				1, //Scale
				SpriteEffects.None, //Sprite effect
				0 //Broken depth lol
			);
			//top right
			m_spriteBatch.Draw(
				m_borderTexture, //Texture
				new Vector2(width, -m_borderSegmentSize.Y), //Position
				new Rectangle(2 * (int)m_borderSegmentSize.X, 0, (int)m_borderSegmentSize.X, (int)m_borderSegmentSize.Y), //Source rect
				colour, //Colour
				0, //Rot
				Vector2.Zero, //Origin
				1, //Scale
				SpriteEffects.None, //Sprite effect
				0 //Broken depth lol
			);
			//bottom right
			m_spriteBatch.Draw(
				m_borderTexture, //Texture
				new Vector2(width, height), //Position
				new Rectangle(2 * (int)m_borderSegmentSize.X, 2 * (int)m_borderSegmentSize.X, (int)m_borderSegmentSize.X, (int)m_borderSegmentSize.Y), //Source rect
				colour, //Colour
				0, //Rot
				Vector2.Zero, //Origin
				1, //Scale
				SpriteEffects.None, //Sprite effect
				0 //Broken depth lol
			);
			//bottom left
			m_spriteBatch.Draw(
				m_borderTexture, //Texture
				new Vector2(-m_borderSegmentSize.X, height), //Position
				new Rectangle(0, 2 * (int)m_borderSegmentSize.X, (int)m_borderSegmentSize.X, (int)m_borderSegmentSize.Y), //Source rect
				colour, //Colour
				0, //Rot
				Vector2.Zero, //Origin
				1, //Scale
				SpriteEffects.None, //Sprite effect
				0 //Broken depth lol
			);

			/*************************************************************************
			 * lines
			 *************************************************************************/

			//left
			m_spriteBatch.Draw(
				m_borderTexture, //Texture
				new Rectangle((int)-m_borderSegmentSize.X, 0, (int)m_borderSegmentSize.X, (int)height),
				new Rectangle(0 * (int)m_borderSegmentSize.X, 1 * (int)m_borderSegmentSize.Y, (int)m_borderSegmentSize.X, (int)m_borderSegmentSize.Y), //Source rect
				colour, //Colour
				0, //Rot
				Vector2.Zero, //Origin
				SpriteEffects.None, //Sprite effect
				0 //Broken depth lol
			);
			//right
			m_spriteBatch.Draw(
				m_borderTexture, //Texture
				new Rectangle((int)width, 0, (int)m_borderSegmentSize.X, (int)height),
				new Rectangle(2 * (int)m_borderSegmentSize.X, 1 * (int)m_borderSegmentSize.Y, (int)m_borderSegmentSize.X, (int)m_borderSegmentSize.Y), //Source rect
				colour, //Colour
				0, //Rot
				Vector2.Zero, //Origin
				SpriteEffects.None, //Sprite effect
				0 //Broken depth lol
			);
			//up
			m_spriteBatch.Draw(
				m_borderTexture, //Texture
				new Rectangle(0, -(int)m_borderSegmentSize.Y, (int)width, (int)m_borderSegmentSize.Y),
				new Rectangle(1 * (int)m_borderSegmentSize.X, 0 * (int)m_borderSegmentSize.Y, (int)m_borderSegmentSize.X, (int)m_borderSegmentSize.Y), //Source rect
				colour, //Colour
				0, //Rot
				Vector2.Zero, //Origin
				SpriteEffects.None, //Sprite effect
				0 //Broken depth lol
			);
			//down
			m_spriteBatch.Draw(
				m_borderTexture, //Texture
				new Rectangle(0, (int)height, (int)width, (int)m_borderSegmentSize.Y),
				new Rectangle(1 * (int)m_borderSegmentSize.X, 2 * (int)m_borderSegmentSize.Y, (int)m_borderSegmentSize.X, (int)m_borderSegmentSize.Y), //Source rect
				colour, //Colour
				0, //Rot
				Vector2.Zero, //Origin
				SpriteEffects.None, //Sprite effect
				0 //Broken depth lol
			);

			m_spriteBatch.End();
		}

		#region Rect stuff
		class Rect {
			public Texture2D texture;
			public Vector2 position;
			public Vector2 imageSize;
			public Vector2 scale;
			public Vector2 pivot;
			public Vector2 textureOffset;
			public Vector2 textureSize;
			public float rot;
			public Color colour;
			public int depth;
			public Vector2 actualPosition { //position of actual graphic
				get {
					return position + textureOffset;
				}
			}
			public Vector2 localSourcePos; //only used for tiles for determining clamp limit
			public Rectangle graphicSource;
			public Vector2 texturePosition {
				get {
					return new Vector2(graphicSource.X - localSourcePos.X, graphicSource.Y - localSourcePos.Y);
				}
			}
			public Vector2 RealPosition(float scale, Vector2 transform) {
				return ((position - pivot * this.scale) * scale) + transform;
			}
			public Vector2 RealGraphicPosition(float scale, Vector2 transform) {
				return ((actualPosition - pivot * this.scale) * scale) + transform;
			}
			/*public Vector2 RealSize(float scale) {
				return new Vector2(imageSize.X, imageSize.Y) * scale;
			}*/
			public Vector2 RealGraphicSize(float scale) {
				return (new Vector2(graphicSource.Width, graphicSource.Height)) * scale;
			}
			public bool Bleeds {
				get {
					return (graphicSource.Width + localSourcePos.X) > imageSize.X || (graphicSource.Height+ localSourcePos.Y) > imageSize.Y;
				}
			}
			public Rectangle ClampedSource() {
				var source = graphicSource;
				source.Width = (int)Math.Min(textureSize.X - localSourcePos.X, graphicSource.Width);
				source.Height = (int)Math.Min(textureSize.Y - localSourcePos.Y, graphicSource.Height);
				return source;
			}
			public void Draw(SpriteBatch spriteBatch, bool clampRect) {
				Rectangle source;
				if(!Bleeds || clampRect) {
					source = ClampedSource();
				} else {
					source = graphicSource;
				}
				spriteBatch.Draw(texture,
					position,
					source,
					colour,
					-MathHelper.ToRadians(rot),
					pivot - textureOffset,
					scale,
					SpriteEffects.None,
					depth
				);
			}
		}

		Rect MakeRect(float x, float y, float rot, float scaleX, float scaleY) {
			return new() {
				texture = m_missingTexture,
				colour = Color.White,
				depth = 0,
				graphicSource = new(0, 0, 16, 16),
				imageSize = new(16, 16),
				pivot = Vector2.Zero,
				position = new(x, y),
				rot = rot,
				scale = new(scaleX, scaleY),
				textureOffset = Vector2.Zero,
				textureSize = new(16, 16),
				localSourcePos = Vector2.Zero
			};
		}

		Rect MakeRect(GMTextureItem texItem) {
			return new() {
				graphicSource = new(texItem.SourceX, texItem.SourceY, texItem.SourceWidth, texItem.SourceHeight),
				textureSize = new(texItem.SourceWidth, texItem.SourceHeight),
				imageSize = new(texItem.BoundWidth, texItem.BoundHeight),
				texture = m_textures[texItem.TexturePageID],
				textureOffset = new(texItem.TargetX, texItem.TargetY),
				pivot = Vector2.Zero,
				rot = 0,
				localSourcePos = Vector2.Zero
			};
		}

		Rect MakeRect(GMRoom.GameObject obj) {
			var gameObject = m_objectChunk[obj.ObjectID];
			var spriteId = gameObject.SpriteID;

			Rect? rect = null;
			if (spriteId != -1) {
				var sprite = m_spriteChunk[spriteId];
				var texItems = sprite.TextureItems;
				if (texItems.Count != 0) {
					rect = MakeRect(texItems[0]);

					rect.position = new(obj.X, obj.Y);
					rect.rot = obj.Angle;
					rect.scale = new(obj.ScaleX, obj.ScaleY);
					rect.depth = gameObject.Depth;
					rect.pivot = new(sprite.OriginX, sprite.OriginY);
					rect.colour = ABGRToColour(obj.Color);
				}
			}
			rect ??= MakeRect(obj.X, obj.Y, obj.Angle, obj.ScaleX, obj.ScaleY); //if there was no sprite rect, we give it the question mark icon
			return rect;
		}

		Rect MakeRect(GMRoom.Tile tile) {
			GMTextureItem texItem;
			if(tile.AssetID == -1) {
				Console.WriteLine("WHAT THE FUCK");
				return MakeRect(tile.X, tile.Y, 0, tile.ScaleX, tile.ScaleY);
			}
			if (m_dataReader.VersionInfo.IsVersionAtLeast(2)) {
				var sprite = m_spriteChunk[tile.AssetID];
				try {
					texItem = sprite.TextureItems[0];
				} catch(Exception e) {
					Console.WriteLine($"To whoever made this game: you are very silly\n{sprite.Name} {sprite.TextureItems.Count}\n{e}");
#pragma warning disable CS8603 // Possible null reference return.
					return null; //make the makerect call down here shut the fuck up
#pragma warning restore CS8603 // Possible null reference return.
				}
			} else {
				texItem = m_backgroundChunk[tile.AssetID].TextureItem;
			}
			Rect rect = MakeRect(texItem);

			rect.position = new(tile.X, tile.Y);
			rect.scale = new(tile.ScaleX, tile.ScaleY);
			rect.depth = tile.Depth;
			//rect.imageSize = new(tile.Width, tile.Height);
			rect.graphicSource.Location += new Point(tile.SourceX, tile.SourceY);
			rect.graphicSource.Width = tile.Width;// - (int)rect.textureOffset.X;
			rect.graphicSource.Height = tile.Height;// - (int)rect.textureOffset.Y;
			rect.colour = ABGRToColour(tile.Color);
			rect.localSourcePos = new(tile.SourceX, tile.SourceY);
			return rect;
		}
		#endregion

		void SetRoomCache() {
			m_tileRects.Clear();
			m_objectRects.Clear();
			//m_eventManager.m_eventList.Clear();

			m_currentRoom = m_roomChunk[m_selectedRoom];
			m_currentRoomObjects = m_currentRoom.GameObjects;

			//Populate rect lists
			if (m_dataReader.VersionInfo.IsVersionAtLeast(2)) {
				m_currentRoomTiles = new(); //Hope to god gc is cool
				for(var i = m_currentRoom.Layers.Count-1; i >= 0; i--) {
					var layer = m_currentRoom.Layers[i];
					if (layer.Kind == GMRoom.Layer.LayerKind.Assets) { //compat layers are asset layers
						foreach(var tile in layer.Assets.LegacyTiles) {
							m_currentRoomTiles.Add(tile);
						}
					}
				}
			} else {
				m_currentRoomTiles = m_currentRoom.Tiles;
			}
			m_currentRoomTiles.ForEach(tile => { m_tileRects.Add(MakeRect(tile)); });
			m_currentRoomObjects.ForEach(obj => { m_objectRects.Add(MakeRect(obj)); });
		}

		//current tile index in rect list and room tile list
		int GetCurrentTileIndex() {
			if (m_currentRoomTiles.Count == 0) {
				return 0;
			}
			return (int)Math.Clamp(m_tileCap - 1, 0, Math.Max(m_currentRoomTiles.Count - 1, 0)); //this max is to prevent crashing when room has neither of these
		}
		int GetCurrentObjectIndex() {
			return Math.Clamp(m_flags.HasFlag(RoomViewerFlags.UseSeparateSliderForObjects)
				?
				m_objectCap - 1
				:
				(((int)m_tileCap - 1) - m_currentRoomTiles.Count), 0, Math.Max(m_currentRoomObjects.Count - 1, 0));
		}

		void ApplyScreen(int width, int height, bool fullscreen) {
			m_gdm.PreferredBackBufferWidth = width;
			m_gdm.PreferredBackBufferHeight = height;
			m_gdm.IsFullScreen = fullscreen;
			m_gdm.ApplyChanges();
			Liner.m_basicEffect.Projection = Matrix.CreateOrthographicOffCenter
				(0, GraphicsDevice.Viewport.Width,     // left, right
				GraphicsDevice.Viewport.Height, 0,    // bottom, top
				0, 1);
		}

		protected override void Draw(GameTime gameTime) {
			GraphicsDevice.Clear(Color.CornflowerBlue);

			//Draw background
			m_spriteBatch.Begin();
			m_spriteBatch.Draw(m_backgroundTexture, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), Color.White);
			m_spriteBatch.End();

			if(m_data is null) {

				m_imRenderer.AfterLayout();
				return;
			}

			if (m_io.MouseDown[2] && IsActive) {
				m_tilemapTransform.X += m_io.MouseDelta.X;
				m_tilemapTransform.Y += m_io.MouseDelta.Y;
			}

			Rect? lastTile = null;
			Rect? lastObject = null;

			var cameraMatrix = Matrix.CreateScale(new Vector3(m_scale, m_scale, 0)) * Matrix.CreateTranslation(new Vector3(m_tilemapTransform, 0));

			//Draw room border
			DrawBorder(m_tilemapTransform.X, m_tilemapTransform.Y, m_currentRoom.Width * m_scale, m_currentRoom.Height * m_scale, Color.DarkGray, 0, 0);

			//Draw tilemap

			/*
			 * FIXME:
			 * Attempting to use xna's built in sprite sorter to sort object and tile depths together will not
			 * retain the original order of such, causing Toby Moments(tm) that would otherwise be hidden to show up,
			 * and will cause blinking when changing how many tiles to draw.
			 * For the moment being deferred will be used.
			 */

			m_spriteBatch.Begin(m_flags.HasFlag(RoomViewerFlags.BuggyDepthSorting) ? SpriteSortMode.BackToFront : SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointWrap, null, RasterizerState.CullNone, null, cameraMatrix);

			#region Draw tilemap
			for (var i = 0; i < m_currentRoomTiles.Count; i++) {
				if (m_tileCap - 1 < i) {
					break;
				}
				var tile = m_currentRoomTiles[i];
				if (m_flags.HasFlag(RoomViewerFlags.FollowBlacklist)) {
					if (m_blacklistedBackgrounds.IndexOf(tile.AssetID) != -1) {
						continue;
					}
				}

				m_tileRects[i].Draw(m_spriteBatch, m_flags.HasFlag(RoomViewerFlags.ClampTileRects));

				lastTile = m_tileRects[i];
			}
			#endregion

			#region Draw objects
			if (m_flags.HasFlag(RoomViewerFlags.ShowObjects)) {
				for (var i = 0; i < m_currentRoomObjects.Count; i++) {
					var max = m_flags.HasFlag(RoomViewerFlags.UseSeparateSliderForObjects) ? m_objectCap - 1 : (m_tileCap - m_currentRoomTiles.Count - 1);
					if (i > max) {
						break;
					}
					m_objectRects[i].Draw(m_spriteBatch, false);
					lastObject = m_objectRects[i];
					//Debug.Assert(i != 0);
				}
			}
			#endregion

			m_spriteBatch.End();

			//Draw oob text
			bool drawOobText = false;
			var roomRect = new Rectangle(0, 0, m_currentRoom.Width, m_currentRoom.Height);
			if (lastTile is not null) {
				drawOobText = !roomRect.Contains((int)lastTile.position.X, (int)lastTile.position.Y);
			} else if(lastObject is not null){
				drawOobText = !roomRect.Contains((int)lastObject.position.X, (int)lastObject.position.Y);
			}

			if (drawOobText) {
				m_spriteBatch.Begin();
				m_spriteBatch.Draw(m_oobText, new Vector2(GraphicsDevice.Viewport.Width - m_oobText.Width, 0), Color.White);
				m_spriteBatch.End();
			}

			#region Tile source rect interface
			bool canDoTileStuff = m_tileCap - 1 < m_tileRects.Count && lastTile != null;
			if (true) {
				if (m_flags.HasFlag(RoomViewerFlags.ShowTilePalette) && canDoTileStuff) {
					int paletteWidth = (int)(Math.Min(m_tilePaletteSize.X, lastTile.imageSize.X));
					int paletteHeight = (int)(Math.Min(m_tilePaletteSize.Y, lastTile.imageSize.Y));

					//m_tilePalettePos.Y = GraphicsDevice.Viewport.Height / 2 - paletteHeight / 2; //vertically center palette

					m_tilePaletteOffset = new( //center tile palette on tile source
						Math.Clamp((lastTile.localSourcePos.X - paletteWidth / 2) + lastTile.graphicSource.Width / 2, 0, Math.Max(lastTile.textureSize.X - paletteWidth, 0)), //dirty way to prevent crash because of gamemaker moments
						Math.Clamp((lastTile.localSourcePos.Y - paletteHeight / 2) + lastTile.graphicSource.Height / 2, 0, Math.Max(lastTile.textureSize.Y - paletteHeight, 0)));

					m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointWrap, null, null, null);
					///FIXME: Backgrounds with offsets break, i can't be arsed to fix this atm
					m_spriteBatch.Draw( //draw the tileset
						lastTile.texture,
						new Vector2((int)m_tilePalettePos.X, (int)m_tilePalettePos.Y) /*+ lastTile.textureOffset*/,
						new Rectangle(
							(int)(lastTile.texturePosition.X + m_tilePaletteOffset.X),
							(int)(lastTile.texturePosition.Y + m_tilePaletteOffset.Y),
							paletteWidth/* - (int)lastTile.textureOffset.X*/,
							paletteHeight/* - (int)lastTile.textureOffset.Y*/
						), Color.White
					);
					m_spriteBatch.End();
					DrawBorder(m_tilePalettePos.X, m_tilePalettePos.Y, paletteWidth, paletteHeight, Color.LightCyan, 0, 0); //neat border
				}
			}

			if (m_flags.HasFlag(RoomViewerFlags.ShowRectSource)) {
				if (canDoTileStuff) {
					var tileWorldPos = lastTile.RealPosition(m_scale, m_tilemapTransform);
					var tileWorldX = tileWorldPos.X;
					var tileWorldY = tileWorldPos.Y;
					var tileWorldSize = lastTile.RealGraphicSize(m_scale);
					if (m_flags.HasFlag(RoomViewerFlags.ShowTilePalette)) { //I know i'm checking this twice
						var realSourceX = (m_tilePalettePos.X + lastTile.localSourcePos.X) - m_tilePaletteOffset.X;
						var realSourceY = (m_tilePalettePos.Y + lastTile.localSourcePos.Y) - m_tilePaletteOffset.Y;
						//Draw lines on the corners of the source and target
						Liner.PushLine(realSourceX, realSourceY, tileWorldX, tileWorldY, Color.Red);
						Liner.PushLine(realSourceX + lastTile.graphicSource.Width, realSourceY, tileWorldX + tileWorldSize.X, tileWorldY, Color.Red);
						Liner.PushLine(realSourceX + lastTile.graphicSource.Width, realSourceY + lastTile.graphicSource.Height, tileWorldX + tileWorldSize.X, tileWorldY + tileWorldSize.Y, Color.Red);
						Liner.PushLine(realSourceX, realSourceY + lastTile.graphicSource.Height, tileWorldX, tileWorldY + tileWorldSize.Y, Color.Red);
						//Draw rectangle on the source
						Liner.PushOutline(realSourceX, realSourceY, lastTile.graphicSource.Width, lastTile.graphicSource.Height, Color.DarkRed);
					}
					//Draw rectangle on the target tile
					Liner.PushOutline(tileWorldX, tileWorldY, tileWorldSize.X, tileWorldSize.Y, Color.Red);
				}
				if (lastObject != null) {
					var actualPos = lastObject.RealPosition(m_scale, m_tilemapTransform);
					Liner.PushOutline(actualPos.X, actualPos.Y, lastObject.imageSize.X * lastObject.scale.X * m_scale, lastObject.imageSize.Y * lastObject.scale.Y * m_scale, Color.GreenYellow);
				}
				Liner.Flush(this);
			}
			#endregion

			//Draw events
			m_eventManager.Update(in gameTime, in m_spriteBatch, (int)m_tileCap - 1, m_selectedRoom, canDoTileStuff ? new(0,m_tilePaletteSize.Y+m_borderSegmentSize.Y) : Vector2.Zero);

			m_imRenderer.AfterLayout();

			base.Draw(gameTime);
		}

		protected override void Update(GameTime gameTime) {
			if(m_data is null) {
				bool showLoadErrorModal = false;
				m_imRenderer.BeforeLayout(gameTime);
				ImGui.SetNextWindowPos(new(GraphicsDevice.Viewport.Width/2, GraphicsDevice.Viewport.Height/2), ImGuiCond.Always, new(0.5f, 0.5f));
				ImGui.Begin("balls", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);
				ImGui.Image(m_filePathInstructonImgui, new(m_filePathInstruction.Width, m_filePathInstruction.Height));
				ImGui.Separator(); ImGui.Separator(); ImGui.Separator();
				ImGui.TextUnformatted("Please input the game's data file path.\nIf you don't know how to get the file path, refer to the image above.");
				ImGui.InputText("File path", ref wadPath, 0xff);
				if(ImGui.Button("Load")) {
					wadPath = wadPath.Trim('"'); //lol
					if(File.Exists(wadPath)) {
						var result = LoadWad(wadPath);
						if(!result) {
							showLoadErrorModal = true;
						}
					}
				}
				ImGui.End();
				if(showLoadErrorModal) { //i still don't know why i can't open the modal whilst a window is open
					ImGui.OpenPopup("WadLoadError");
					showLoadErrorModal = false;
				}
				bool dummy = true; //dummy value
				if(ImGui.BeginPopupModal("WadLoadError", ref dummy, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove)) {
					ImGui.TextUnformatted("Error loading wad file, look @ console");
					if(ImGui.Button("Close")) {
						ImGui.CloseCurrentPopup();
					}
					ImGui.EndPopup();
				}
				return;
			}

			var oldSelectedRoom = m_selectedRoom;
			var oldTileCap = m_tileCap;

			#region Imgui
			m_imRenderer.BeforeLayout(gameTime);

			//Overlay
			if (m_flags.HasFlag(RoomViewerFlags.ShowOverlay)) {
				ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, GraphicsDevice.Viewport.Height), ImGuiCond.Always, new System.Numerics.Vector2(0f, 1f));
				if (ImGui.Begin("overlay", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav)) {
					ImGui.TextUnformatted($"Current room: {m_roomChunk[m_selectedRoom].Name}");
					ImGui.Separator();
					if (m_tileCap - 1 < m_currentRoomTiles.Count && m_currentRoomTiles.Count != 0) {
						var tileCurrent = m_currentRoomTiles[GetCurrentTileIndex()];
						var colour = ABGRToColour(tileCurrent.Color);
						string name = "Hello hi this is a very funny tile because it SOMEHOW HAS NO ASSET ASSOCIATED WITH IT yet it still has some normal member data stuffs";
						if(tileCurrent.AssetID != -1)
							name = (m_dataReader.VersionInfo.IsVersionAtLeast(2) ? m_spriteChunk[tileCurrent.AssetID].Name : m_backgroundChunk[tileCurrent.AssetID].Name).Content;
						ImGui.TextUnformatted(@$"Tile asset: {name}
Tile index: {GetCurrentTileIndex() + 1}/{m_currentRoomTiles.Count}
Tile ID: {tileCurrent.ID}
Tile position: {tileCurrent.X},{tileCurrent.Y} 
Tile scale: {tileCurrent.ScaleX}, {tileCurrent.ScaleY}
Tile depth: {tileCurrent.Depth}
Tile source: {tileCurrent.SourceX}, {tileCurrent.SourceY}
Tile dimensions: {tileCurrent.Width}, {tileCurrent.Height}
Tile colour: R {colour.R} G {colour.G} B {colour.B} A {colour.A}");
					} else if (m_currentRoomObjects.Count != 0) {
						var objCurrent = m_currentRoomObjects[GetCurrentObjectIndex()];
						var colour = ABGRToColour(objCurrent.Color);
						string codeDescription = ""; //for pre create/creation code
						if (objCurrent.PreCreateCodeID > 0) {
							codeDescription = $"Object pre create entry: {m_codeChunk[objCurrent.PreCreateCodeID].Name}";
						}
						if (objCurrent.CreationCodeID > 0) {
							if (codeDescription.Length != 0) {
								codeDescription += "\n";
							}
							codeDescription += $"Object creation code entry: {m_codeChunk[objCurrent.CreationCodeID].Name}\n";
						}
						ImGui.TextUnformatted($@"{codeDescription}Object: {m_objectChunk[objCurrent.ObjectID].Name}
Object index: {GetCurrentObjectIndex() + 1}/{m_currentRoomObjects.Count}
Object ID: {objCurrent.InstanceID}
Object position {objCurrent.X}, {objCurrent.Y}
Object scale: {objCurrent.ScaleX}, {objCurrent.ScaleY}
Object rotation: {objCurrent.Angle}
Object depth: {m_objectChunk[objCurrent.ObjectID].Depth}
Object colour: R {colour.R} G {colour.G} B {colour.B} A {colour.A}");
					}
					if(!m_flags.HasFlag(RoomViewerFlags.ShowMainWindow)) {
						ImGui.Separator();
						ImGui.TextUnformatted("Press shift to revert to normal");
					}
					ImGui.End();
				}
			}

			//Main window
			if (m_flags.HasFlag(RoomViewerFlags.ShowMainWindow) && this.IsActive) {
				ImGui.Begin("Config"/*, ImGuiWindowFlags.AlwaysAutoResize*/);

				if (ImGui.BeginTabBar("Tab bar")) {
					//Room selection imgui
					if (ImGui.BeginTabItem("Room selection")) {
						ImGui.BeginChild("Child");
						ImGui.ListBox("Rooms", ref m_selectedRoom, m_roomNames, m_roomNames.Length);
						ImGui.EndChild();
						ImGui.EndTabItem();
					}
					//Config imgui
					if (ImGui.BeginTabItem("Settings")) {
						ImGui.BeginChild("Child");
						var flags = (int)m_flags;
						ImGui.TextUnformatted("Tip: double click to manually change value with drag sliders, click while holding alt\nfor the normal sliders");
						ImGui.SliderInt("Window width", ref gameResX, 16, m_gdm.GraphicsDevice.DisplayMode.Width);
						ImGui.SliderInt("Window height", ref gameResY, 16, m_gdm.GraphicsDevice.DisplayMode.Height);
						ImGui.CheckboxFlags("Fullscreen", ref flags, (int)RoomViewerFlags.Fullscreen);
						if(ImGui.Button("Apply")) {
							ApplyScreen(gameResX, gameResY, m_flags.HasFlag(RoomViewerFlags.Fullscreen));
						}
						ImGui.Separator();
						ImGui.CheckboxFlags("Clamp tile rects", ref flags, (int)RoomViewerFlags.ClampTileRects);
						ImGui.CheckboxFlags("Follow background blacklist", ref flags, (int)RoomViewerFlags.FollowBlacklist);
						ImGui.CheckboxFlags("Use buggy depth sorting", ref flags, (int)RoomViewerFlags.BuggyDepthSorting);
						ImGui.CheckboxFlags("Show objects", ref flags, (int)RoomViewerFlags.ShowObjects);
						ImGui.CheckboxFlags("Separate slider for objects", ref flags, (int)RoomViewerFlags.UseSeparateSliderForObjects);
						ImGui.CheckboxFlags("Show latest tile palette", ref flags, (int)RoomViewerFlags.ShowTilePalette);
						ImGui.CheckboxFlags("Show latest tile source", ref flags, (int)RoomViewerFlags.ShowRectSource);
						ImGui.CheckboxFlags("Auto reset anim", ref flags, (int)RoomViewerFlags.AutoResetAnim);
						ImGui.CheckboxFlags("Follow latest tile", ref flags, (int)RoomViewerFlags.FollowLatestRect);
						ImGui.CheckboxFlags("Show overlay", ref flags, (int)RoomViewerFlags.ShowOverlay);
						if(ImGui.CheckboxFlags("Show this window (press shift to bring it back up)", ref flags, (int)RoomViewerFlags.ShowMainWindow)) {
							m_flagBackup = (RoomViewerFlags)flags | (RoomViewerFlags.ShowMainWindow);
						}
						m_flags = (RoomViewerFlags)flags;

						ImGui.SliderFloat("Max tiles render", ref m_tileCap, 0, ((m_flags.HasFlag(RoomViewerFlags.ShowObjects) && !m_flags.HasFlag(RoomViewerFlags.UseSeparateSliderForObjects)) ? m_currentRoomTiles.Count + m_currentRoomObjects.Count : m_currentRoomTiles.Count), Math.Floor(m_tileCap).ToString());
						
						if (m_flags.HasFlag(RoomViewerFlags.UseSeparateSliderForObjects)) {
							ImGui.SliderInt("Max objects render", ref m_objectCap, 0, m_currentRoomObjects.Count);
						}
						ImGui.DragFloat("Scale", ref m_scale, 0.01f);
						ImGui.SameLine();
						if (ImGui.Button("Reset")) {
							m_scale = 1;
							m_tilemapTransform = new Vector2(0, 0);
						}
						ImGui.DragFloat("Video scale", ref m_videoScale, 0.01f);
						ImGui.SliderFloat("Anim tiles per second", ref m_animationSpeed, 1, 120, "%.0f");
						m_animationSpeed = MathF.Floor(m_animationSpeed);
						ImGui.SameLine();
						if (ImGui.Button("15")) {
							m_animationSpeed = 15f;
						}
						ImGui.SameLine();
						if (ImGui.Button("20")) {
							m_animationSpeed = 20f;
						}
						ImGui.SameLine();
						if (ImGui.Button("25")) {
							m_animationSpeed = 25f;
						}
						ImGui.SameLine();
						if (ImGui.Button("30")) {
							m_animationSpeed = 30f;
						}
						ImGui.SameLine();
						if (ImGui.Button("60")) {
							m_animationSpeed = 60f;
						}
						if (m_flags.HasFlag(RoomViewerFlags.IsAnimating)) {
							if (ImGui.Button("Stop anim")) {
								m_flags &= ~RoomViewerFlags.IsAnimating;
							}
						} else {
							if (ImGui.Button("Animate")) {
								if (m_flags.HasFlag(RoomViewerFlags.AutoResetAnim)) {
									m_tileCap = 0;
								}
								m_flags |= RoomViewerFlags.IsAnimating;
							}
							if (ImGui.Button("Begin video")) {
								m_flagBackup = m_flags;
								m_flags &= ~(RoomViewerFlags.UseSeparateSliderForObjects | RoomViewerFlags.FollowBlacklist | RoomViewerFlags.BuggyDepthSorting | RoomViewerFlags.ShowMainWindow);
								m_flags |= RoomViewerFlags.IsAnimating | RoomViewerFlags.IsVideoMode | RoomViewerFlags.ShowTilePalette | RoomViewerFlags.ShowRectSource | RoomViewerFlags.ShowObjects | RoomViewerFlags.ShowOverlay | RoomViewerFlags.FollowLatestRect;
								m_scale = m_videoScale;
								//m_tileCap = 9999999;
								m_videoModeFlag = false;
							}
						}
						ImGui.EndChild();
						ImGui.EndTabItem();
					}
					if(ImGui.BeginTabItem("Events")) {
						if (ImGui.Button("Clear events")) {
							m_eventManager.m_eventList.Clear();
						}
						ImGui.SameLine();
						if (ImGui.Button("Record none")) {
							m_eventsToRecord = 0;
						}
						ImGui.TextUnformatted("Record:");
						int flags = (int)m_eventsToRecord;
						ImGui.CheckboxFlags("Rects with colour", ref flags, (int)EventRecord.HasColour);
						ImGui.CheckboxFlags("Objects with creation/pre create code", ref flags, (int)EventRecord.CreationCode);
						ImGui.CheckboxFlags("Rects with scale", ref flags, (int)EventRecord.ScaledRect);
						ImGui.CheckboxFlags("Objects/tiles with ID discrepancies", ref flags, (int)EventRecord.IdDiscrepancy);
						ImGui.CheckboxFlags("Rects with UV bleeding out of bounding box", ref flags, (int)EventRecord.UvBleed);
						ImGui.CheckboxFlags("Rects with rotation", ref flags, (int)EventRecord.Rotation);
						m_eventsToRecord = (EventRecord)flags;
					}
					//Blacklist imgui
					if (ImGui.BeginTabItem("Blacklist")) {
						ImGui.BeginChild("Child");
						//Background name input field
						ImGui.InputText("Background name", ref m_blacklistSubjectName, 100);
						ImGui.SameLine(); //style
						if (ImGui.Button("Add")) {
							//See if the background we're looking for really exists
							var backgroundIndex = m_backgroundChunk.FindIndex(a => a.Name.Content == m_blacklistSubjectName); //fancy predicates i don't know how they work
							if (backgroundIndex != -1 && m_blacklistedBackgrounds.IndexOf(backgroundIndex) == -1) {
								m_blacklistedBackgrounds.Add(backgroundIndex);
							}
						}
						//The list of blacklisted backgrounds
						if (ImGui.BeginListBox("Blacklisted backgrounds")) {
							for (var i = 0; i < m_blacklistedBackgrounds.Count; i++) {
								bool isSelected = i == m_blacklistSelectedItem;
								ImGui.PushID(i); //just in case
								if (ImGui.Selectable(m_backgroundChunk[m_blacklistedBackgrounds[i]].Name.Content, isSelected)) {
									//Console.WriteLine("bap");
									m_blacklistSelectedItem = i;
								}
								ImGui.PopID();
								//i don't know what this is for, but the imgui demo has it and i'm not complaining
								if (isSelected) {
									ImGui.SetItemDefaultFocus();
								}
							}
							ImGui.EndListBox();
						}
						if (ImGui.Button("Remove")) {
							if (m_blacklistedBackgrounds.Count != 0) { //this needs to be checked to prevent a crash
								m_blacklistedBackgrounds.RemoveAt(m_blacklistSelectedItem);
							}
						}
						ImGui.EndChild();
						ImGui.EndTabItem();
					}
					ImGui.EndTabBar();
				}

				ImGui.End();
			}
			#endregion

			if(m_io.KeyShift) {
				m_flags = m_flagBackup;
				m_videoModeFlag = false;
			}

			if (m_selectedRoom != oldSelectedRoom) { //Room change has occured
				SetRoomCache();
			}

			if (m_flags.HasFlag(RoomViewerFlags.IsAnimating)) {
				var max = m_flags.HasFlag(RoomViewerFlags.UseSeparateSliderForObjects) ? m_currentRoomTiles.Count : m_currentRoomTiles.Count + m_currentRoomObjects.Count;
				m_tileCap += m_animationSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
				if (m_tileCap >= max) { //if animation is done
					m_tileCap = max;
					if (m_flags.HasFlag(RoomViewerFlags.IsVideoMode)) { //if in video mode
						if (m_flags.HasFlag(RoomViewerFlags.VideoModeDelaying)) { //if waiting for sumn
							m_videoModeTimer -= gameTime.ElapsedGameTime.TotalSeconds; //tick the timer down
							if (m_videoModeTimer <= 0) { //if the timer ended
								if (m_videoModeFlag) { //begin the anim
									m_videoModeFlag = false;
									m_flags &= ~(RoomViewerFlags.VideoModeDelaying);
									m_flags |= RoomViewerFlags.FollowLatestRect;
									m_tileCap = 0;
									m_scale = m_videoScale;
								} else { //switch rooms, set timer again
									m_selectedRoom++;
									if (m_selectedRoom == m_roomChunk.Count) { //if we're outta rooms
										m_selectedRoom--;
										m_flags = m_flagBackup;
										m_videoModeFlag = false;
									} else { //show overview of the room before starting anim
										m_flags &= ~(RoomViewerFlags.FollowLatestRect);
										SetRoomCache();
										m_tileCap = max = m_flags.HasFlag(RoomViewerFlags.UseSeparateSliderForObjects) ? m_currentRoomTiles.Count : m_currentRoomTiles.Count + m_currentRoomObjects.Count;
										m_videoModeTimer = 5f;
										m_videoModeFlag = true;
										m_scale = ((float)GraphicsDevice.Viewport.Width / m_currentRoom.Width);
										var roomWidthScaled = m_currentRoom.Width * m_scale;
										var roomHeightScaled = m_currentRoom.Height * m_scale;
										if (roomHeightScaled >= GraphicsDevice.Viewport.Height) {
											m_scale = (float)GraphicsDevice.Viewport.Height / m_currentRoom.Height;
											roomWidthScaled = m_currentRoom.Width * m_scale;
											roomHeightScaled = m_currentRoom.Height * m_scale;
										}
										m_tilemapTransform = new(-(roomWidthScaled / 2 - ((float)GraphicsDevice.Viewport.Width / 2)), -(roomHeightScaled / 2 - ((float)GraphicsDevice.Viewport.Height / 2)));
										//m_flags &= ~(RoomViewerFlags.VideoModeDelaying);
									}
								}
							}
						} else { //if room anim ended then halt for like 5 secs
							m_videoModeFlag = false;
							m_flags |= RoomViewerFlags.VideoModeDelaying;
							m_videoModeTimer = 5f;
						}
					} else {
						m_flags &= ~RoomViewerFlags.IsAnimating;
					}
				}
			}

			//Tile cap has changed?
			if ((int)oldTileCap != (int)m_tileCap) { //NOTE: the placement of this matters, it has to be below the animate code
				//Console.WriteLine((m_tileCap - oldTileCap).ToString());
				//Event handling
				//NOTE: this will skip if the rect gap is larger than 1
				if((int)m_tileCap != 0) { //not nothing
					Rect? rectCurrent = null; //we could be in an empty room
					if((int)m_tileCap > m_currentRoomTiles.Count && m_currentRoomObjects.Count != 0) { //object specific things
						rectCurrent = m_objectRects[GetCurrentObjectIndex()];
						GMRoom.GameObject? objectPrevious = null;
						GMRoom.GameObject objectCurrent = m_currentRoomObjects[GetCurrentObjectIndex()];
						if((int)m_tileCap-2 > m_currentRoomTiles.Count) {
							objectPrevious = m_currentRoomObjects[GetCurrentObjectIndex() - 1];
						}

						if(rectCurrent.rot != 0 && m_eventsToRecord.HasFlag(EventRecord.Rotation)) {
							m_eventManager.AddEvent(EventType.ObjectRotation, (int)m_tileCap - 1, m_selectedRoom);
						}
						if((objectCurrent.PreCreateCodeID > 0 || objectCurrent.CreationCodeID > 0) && m_eventsToRecord.HasFlag(EventRecord.CreationCode)) {
							m_eventManager.AddEvent(EventType.CreationCode, (int)m_tileCap - 1, m_selectedRoom);
						}
						if(objectPrevious is not null) {
							if(objectPrevious.InstanceID != objectCurrent.InstanceID - 1 && m_eventsToRecord.HasFlag(EventRecord.IdDiscrepancy)) {
								m_eventManager.AddEvent(EventType.IdDiscrepancy, (int)m_tileCap - 1, m_selectedRoom); //ohfuck
							}
						}

					} else if(m_currentRoomTiles.Count != 0){
						GMRoom.Tile? tilePrevious = null;
						GMRoom.Tile tileCurrent = m_currentRoomTiles[GetCurrentTileIndex()];
						rectCurrent = m_tileRects[GetCurrentTileIndex()];
						if ((int)m_tileCap > 1) {
							tilePrevious = m_currentRoomTiles[GetCurrentTileIndex() - 1];
						}
						if(tilePrevious is not null) {
							if (tilePrevious.ID != tileCurrent.ID - 1 && m_eventsToRecord.HasFlag(EventRecord.IdDiscrepancy)) {
								m_eventManager.AddEvent(EventType.IdDiscrepancy, (int)m_tileCap - 1, m_selectedRoom); //ohfuck
							}
						}
					}
					if (rectCurrent is not null) {
						if (rectCurrent.colour != Color.White && m_eventsToRecord.HasFlag(EventRecord.HasColour)) {
							m_eventManager.AddEvent(EventType.Colour, (int)m_tileCap - 1, m_selectedRoom);
						}
						if (rectCurrent.scale != Vector2.One && m_eventsToRecord.HasFlag(EventRecord.ScaledRect)) {
							m_eventManager.AddEvent(EventType.ScaledRect, (int)m_tileCap - 1, m_selectedRoom);
						}
						if(m_eventsToRecord.HasFlag(EventRecord.UvBleed)) { //i'm leaving this for both objects and tiles incase any funny business is happening
							//if(rectCurrent.graphicSource != rectCurrent.ClampedSource()) {
							if(rectCurrent.Bleeds) {
								m_eventManager.AddEvent(EventType.UvBleed, (int)m_tileCap - 1, m_selectedRoom);
							}
						}
					}
				}

				//Tile following
				if (m_flags.HasFlag(RoomViewerFlags.FollowLatestRect)) {
					float tileCamCenterX = 0, tileCamCenterY = 0;
					if (m_tileCap - 1 < m_currentRoomTiles.Count && m_currentRoomTiles.Count != 0) { //if within tile cap still
						//Console.WriteLine($"{GetCurrentTileIndex()}");
						var tileCurrent = m_currentRoomTiles[GetCurrentTileIndex()];
						tileCamCenterX = -((tileCurrent.X + tileCurrent.Width / 2) * m_scale - GraphicsDevice.Viewport.Width / 2);
						tileCamCenterY = -((tileCurrent.Y + tileCurrent.Height / 2) * m_scale - GraphicsDevice.Viewport.Height / 2);
					} else if(m_objectRects.Count != 0) { //if not, check if there are objects first
						var rect = m_objectRects[GetCurrentObjectIndex()];

						tileCamCenterX = -((rect.position.X + rect.imageSize.X / 2) * m_scale - GraphicsDevice.Viewport.Width / 2);
						tileCamCenterY = -((rect.position.Y + rect.imageSize.Y / 2) * m_scale - GraphicsDevice.Viewport.Height / 2);
					}

					m_tilemapTransform = (new Vector2(
						Math.Clamp(m_tilemapTransform.X, tileCamCenterX - 200, tileCamCenterX + 200),
						Math.Clamp(m_tilemapTransform.Y, tileCamCenterY - 200, tileCamCenterY + 200)
					));
				}
			}
			
			base.Update(gameTime);
		}

		public GameMain(string? wadPath) {
			m_gdm = new GraphicsDeviceManager(this) {
				PreferredBackBufferWidth = 1360,
				PreferredBackBufferHeight = 860,
				SynchronizeWithVerticalRetrace = false,
				PreferMultiSampling = false,
				//IsFullScreen = true,
			};
			IsMouseVisible = true;
			IsFixedTimeStep = false;

			m_gdm.ApplyChanges();
			this.wadPath = wadPath ?? String.Empty;
		}

		protected override void Initialize() {
			//Fullscreen
			//m_gdm.PreferredBackBufferWidth = m_gdm.GraphicsDevice.DisplayMode.Width;
			//m_gdm.PreferredBackBufferHeight= m_gdm.GraphicsDevice.DisplayMode.Height;
			//m_gdm.ToggleFullScreen();

			m_imRenderer = new ImGuiRenderer(this);
			m_imRenderer.RebuildFontAtlas();
			m_io = ImGui.GetIO();
			/*unsafe { //THIS DOESN'T FUCKING WORK WHY DOES IT NOT FUCKING WORK
				//var dirBytes = Encoding.UTF8.GetBytes($"{AppDomain.CurrentDomain.BaseDirectory}bally.ini");
				m_io.NativePtr->IniFilename = "fddsgsg";
			}*/

			base.Initialize();
		}
		bool LoadWad(string path) {
			using (FileStream fs = new(path, FileMode.Open)) {
				try {
					m_dataReader = new GMDataReader(fs, fs.Name);
					//Load gamemaker wad
					m_dataReader.Deserialize();
				} catch(Exception e) {
					Console.WriteLine($"Couldn't load wad\nException: {e}");
					return false;
				}
			}
			m_data = m_dataReader.Data;

			//Populate cache
			m_texturePageChunk = m_data.GetChunk<GMChunkTXTR>().List;
			m_backgroundChunk = m_data.GetChunk<GMChunkBGND>().List;
			m_roomChunk = m_data.GetChunk<GMChunkROOM>().List;
			m_texItemChunk = m_data.GetChunk<GMChunkTPAG>().List;
			m_objectChunk = m_data.GetChunk<GMChunkOBJT>().List;
			m_spriteChunk = m_data.GetChunk<GMChunkSPRT>().List;
			m_codeChunk = m_data.GetChunk<GMChunkCODE>().List;
			m_currentRoom = m_roomChunk[m_selectedRoom];
			m_currentRoomTiles = m_currentRoom.Tiles;
			m_currentRoomObjects = m_currentRoom.GameObjects;

			//Load texture pages as xna texture2d
			foreach (GMTexturePage texture in m_texturePageChunk) {
				if (texture.TextureData.IsQoi) {
					throw new NotImplementedException(
						"Too modern gamemaker version!!!"
						);
				}

				using (var stream = new MemoryStream(texture.TextureData.Data.Memory.ToArray())) {
					m_textures.Add(Texture2D.FromStream(GraphicsDevice, stream));
				}
			}

			//Room name list
			List<string> roomNames = new();
			foreach (GMRoom room in m_roomChunk) {
				roomNames.Add(room.Name.ToString());
			}
			m_roomNames = roomNames.ToArray();

			SetRoomCache();
			return true;
		}
		protected override void LoadContent() {
			m_spriteBatch = new SpriteBatch(GraphicsDevice);
			Liner.Init(this);

			Util.LoadTexture(in m_gdm, out m_missingTexture, "NoSpriteIcon.png"); //Load missing texture
			Util.LoadTexture(in m_gdm, out m_borderTexture, "Putrid10x10.png"); //Load border texture
			Util.LoadTexture(in m_gdm, out m_backgroundTexture, "Background.png"); //Load background texture
			Util.LoadTexture(in m_gdm, out m_oobText, "Oob.png"); //Load oob text(ure)
			Util.LoadTexture(in m_gdm, out m_filePathInstruction, "PathInstruction.png"); //Load instruction texture
			m_filePathInstructonImgui = m_imRenderer.BindTexture(m_filePathInstruction); //bind to imgui

			if (wadPath != String.Empty) { //please have this right here because it depends on the missing texture texture
				try {
					LoadWad(wadPath);
				} catch (Exception e) {
					Console.WriteLine(e);
				}
			}

			m_eventManager = new EventManager(in m_gdm);

			base.LoadContent();
		}
		protected override void UnloadContent() {
			m_spriteBatch.Dispose();

			//Dispose gm textures
			//ClearCachedTextures();
			foreach (var texture in m_textures) {
				texture.Dispose();
			}
			m_imRenderer.UnbindTexture(m_filePathInstructonImgui);

			m_borderTexture.Dispose();
			m_missingTexture.Dispose();
			m_backgroundTexture.Dispose();
			m_oobText.Dispose();
			m_filePathInstruction.Dispose();

			base.UnloadContent();
		}
		Color ABGRToColour(int colour) {
			return new Color((int)(colour & 0x000000FF), (int)((colour & 0x0000FF00) >> 8), (int)((colour & 0x00FF0000) >> 16), (int)((colour & 0xFF000000) >> 24));
		}

		/*
		 * Turns out, caching is a waste of time because dogscepter somehow already consumes 200mb ish of ram, fun
		 * And since I have no idea how to properly do this, there is a big delay when loading rooms
		 */

		void CacheTexture(int texturePageId) {
			if (!m_textureDict.ContainsKey(texturePageId)) {
				using (var stream = new MemoryStream(m_texturePageChunk[texturePageId].TextureData.Data.Memory.ToArray())) {
					m_textures[texturePageId] = Texture2D.FromStream(GraphicsDevice, stream);
				}
			}
		}
		void ClearCachedTextures() {
			foreach (var texture in m_textureDict) {
				texture.Value.Dispose();
				m_textureDict.Remove(texture.Key);
			}
		}
	}
}
