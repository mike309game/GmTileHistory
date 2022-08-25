using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;

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
		VideoModeDelaying = 1 << 22
	}

	class GameMain : Game {
		GraphicsDeviceManager m_gdm;
		SpriteBatch m_spriteBatch;

		GMDataReader m_dataReader;
		GMData m_data;

		List<Texture2D> m_textures = new();
		Dictionary<int, Texture2D> m_textureDict = new();
		Texture2D m_missingTexture;

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
		RoomViewerFlags m_flagBackup;

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
		RoomViewerFlags m_flags = m_startFlags;

		ImGuiRenderer m_imRenderer;
		ImGuiIOPtr m_io;

		float m_scale = 1;

		Vector2 m_tilemapTransform = new(0, 0);

		//Tile palette
		Vector2 m_tilePalettePos = new(0, 0); //position of tile palette
		Vector2 m_tilePaletteSize = new(200, 200); //width and height of visible palette rect
		Vector2 m_tilePaletteOffset = new(0, 0); //view offset of the tile palette
		Texture2D m_borderTexture;
		Vector2 m_borderSegmentSize = new(10, 10); //size of 9slice segments

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
			public int depth;
			public Vector2 RealPosition(float scale, Vector2 transform) {
				return ((position - pivot) * scale) + transform;
			}
			public Vector2 RealGraphicPosition(float scale, Vector2 transform) {
				return ((actualPosition - pivot) * scale) + transform;
			}
			/*public Vector2 RealSize(float scale) {
				return new Vector2(imageSize.X, imageSize.Y) * scale;
			}*/
			public Vector2 RealGraphicSize(float scale) {
				return new Vector2(graphicSource.Width, graphicSource.Height) * scale;
			}
			public void Draw(SpriteBatch spriteBatch, bool clampRect) {
				var source = graphicSource;
				if (clampRect) {
					graphicSource.Width = (int)Math.Min(textureSize.X - localSourcePos.X, graphicSource.Width);
					graphicSource.Height = (int)Math.Min(textureSize.Y - localSourcePos.Y, graphicSource.Height);
				}
				spriteBatch.Draw(texture,
					position,
					graphicSource,
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

			Rect rect = null;
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
			if (rect == null) {
				rect = MakeRect(obj.X, obj.Y, obj.Angle, obj.ScaleX, obj.ScaleY); //placeholder
			}
			return rect;
		}

		Rect MakeRect(GMRoom.Tile tile) {
			var texItem = m_backgroundChunk[tile.AssetID].TextureItem;
			Rect rect = MakeRect(texItem);

			rect.position = new(tile.X, tile.Y);
			rect.scale = new(tile.ScaleX, tile.ScaleY);
			rect.depth = tile.Depth;
			//rect.imageSize = new(tile.Width, tile.Height);
			rect.graphicSource.Location += new Point(tile.SourceX, tile.SourceY);
			rect.graphicSource.Width = tile.Width;
			rect.graphicSource.Height = tile.Height;
			rect.colour = ABGRToColour(tile.Color);
			rect.localSourcePos = new(tile.SourceX, tile.SourceY);
			return rect;
		}

		void SetRoomCache() {
			m_tileRects.Clear();
			m_objectRects.Clear();

			m_currentRoom = m_roomChunk[m_selectedRoom];
			m_currentRoomTiles = m_currentRoom.Tiles;
			m_currentRoomObjects = m_currentRoom.GameObjects;

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

		protected override void Draw(GameTime gameTime) {
			GraphicsDevice.Clear(Color.CornflowerBlue);

			if (m_io.MouseDown[2]) {
				m_tilemapTransform.X += m_io.MouseDelta.X;
				m_tilemapTransform.Y += m_io.MouseDelta.Y;
			}

			Rect lastTile = null;
			Rect lastObject = null;

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
					var max = m_flags.HasFlag(RoomViewerFlags.UseSeparateSliderForObjects) ? m_objectCap - 1 : (m_tileCap - m_roomChunk[m_selectedRoom].Tiles.Count - 1);
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

			#region Tile source rect interface
			bool canDoTileStuff = m_tileCap - 1 < m_tileRects.Count && lastTile != null;
			if (true) {
				if (m_flags.HasFlag(RoomViewerFlags.ShowTilePalette) && canDoTileStuff) {
					int paletteWidth = (int)(Math.Min(m_tilePaletteSize.X, lastTile.imageSize.X));
					int paletteHeight = (int)(Math.Min(m_tilePaletteSize.Y, lastTile.imageSize.Y));

					m_tilePalettePos.Y = GraphicsDevice.Viewport.Height / 2 - paletteHeight / 2; //vertically center palette

					m_tilePaletteOffset = new( //center tile palette on tile source
						Math.Clamp((lastTile.localSourcePos.X - paletteWidth / 2) + lastTile.graphicSource.Width / 2, 0, Math.Max(lastTile.textureSize.X - paletteWidth, 0)), //dirty way to prevent crash because of gamemaker moments
						Math.Clamp((lastTile.localSourcePos.Y - paletteHeight / 2) + lastTile.graphicSource.Height / 2, 0, Math.Max(lastTile.textureSize.Y - paletteHeight, 0)));

					m_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointWrap, null, null, null);
					m_spriteBatch.Draw( //draw the tileset
						lastTile.texture,
						new Vector2((int)m_tilePalettePos.X, (int)m_tilePalettePos.Y),
						new Rectangle((int)(lastTile.texturePosition.X + m_tilePaletteOffset.X), (int)(lastTile.texturePosition.Y + m_tilePaletteOffset.Y), paletteWidth, paletteHeight),
						Color.White
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
						Liner.PushRect(realSourceX, realSourceY, lastTile.graphicSource.Width, lastTile.graphicSource.Height, Color.DarkRed);
					}
					//Draw rectangle on the target tile
					Liner.PushRect(tileWorldX, tileWorldY, tileWorldSize.X, tileWorldSize.Y, Color.Red);
				}
				if (lastObject != null) {
					var actualPos = lastObject.RealPosition(m_scale, m_tilemapTransform);
					Liner.PushRect(actualPos.X, actualPos.Y, lastObject.imageSize.X * lastObject.scale.X * m_scale, lastObject.imageSize.Y * lastObject.scale.Y * m_scale, Color.GreenYellow);
				}
				Liner.Flush(this);
			}
			#endregion

			m_imRenderer.AfterLayout();

			base.Draw(gameTime);
		}

		protected override void Update(GameTime gameTime) {
			var oldSelectedRoom = m_selectedRoom;
			var oldTileCap = m_tileCap;

			#region Imgui
			m_imRenderer.BeforeLayout(gameTime);

			//Overlay
			if (m_flags.HasFlag(RoomViewerFlags.ShowOverlay) && this.IsActive) {
				ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, GraphicsDevice.Viewport.Height), ImGuiCond.Always, new System.Numerics.Vector2(0f, 1f));
				if (ImGui.Begin("overlay", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav)) {

					ImGui.TextUnformatted($"Current room: {m_roomChunk[m_selectedRoom].Name}");
					ImGui.Separator();
					if (m_tileCap - 1 < m_currentRoomTiles.Count && m_currentRoomTiles.Count != 0) {
						var tileCurrent = m_currentRoomTiles[GetCurrentTileIndex()];
						var colour = ABGRToColour(tileCurrent.Color);
						ImGui.TextUnformatted(@$"Tile asset: {m_backgroundChunk[tileCurrent.AssetID].Name}
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
Object colour:  {colour.R} G {colour.G} B {colour.B} A {colour.A}");
					}
					if(!m_flags.HasFlag(RoomViewerFlags.ShowMainWindow)) {
						ImGui.Separator();
						ImGui.TextUnformatted("Press shift to revert to normal");
					}
					ImGui.End();
				}
			}

			//Main window
			if (m_flags.HasFlag(RoomViewerFlags.ShowMainWindow)) {
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
						ImGui.SliderFloat("Scale", ref m_scale, 0.1f, 2);
						ImGui.SameLine();
						if (ImGui.Button("Reset")) {
							m_scale = 1;
							m_tilemapTransform = new Vector2(0, 0);
						}
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
								m_scale = 3;
								//m_tileCap = 9999999;
								m_videoModeFlag = false;
							}
						}
						ImGui.EndChild();
						ImGui.EndTabItem();
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

				/*m_scale = (float)(GraphicsDevice.Viewport.Width - (m_tilePaletteSize.X + m_borderSegmentSize.X)) / m_currentRoom.Width;
				var roomWidthScaled = m_currentRoom.Width * m_scale;
				var roomHeightScaled = m_currentRoom.Height * m_scale;
				if(Math.Floor(roomHeightScaled) >= GraphicsDevice.Viewport.Height) {
					m_scale = (float)GraphicsDevice.Viewport.Height / m_currentRoom.Height;
					roomWidthScaled = m_currentRoom.Width * m_scale;
					roomHeightScaled = m_currentRoom.Height * m_scale;
				}
				m_tilemapTransform = new( -(roomWidthScaled / 2 - (GraphicsDevice.Viewport.Width - (m_tilePaletteSize.X + m_borderSegmentSize.X)) / 2), -(roomHeightScaled / 2 - GraphicsDevice.Viewport.Height / 2), 0);*/

				/*ClearCachedTextures();

				//Load required textures for tiles
				foreach(var tile in m_currentRoomTiles) {
					CacheTexture(m_backgroundChunk[tile.AssetID].TextureItem.TexturePageID);
				}
				foreach(var obj in m_currentRoomObjects) {
					var spr = m_objectChunk[obj.ObjectID].SpriteID;
					if (spr != -1) {
						foreach (var item in m_spriteChunk[spr].TextureItems)
							CacheTexture(item.TexturePageID);
					}
				}*/
			}

			if (m_flags.HasFlag(RoomViewerFlags.IsAnimating)) {
				var max = m_flags.HasFlag(RoomViewerFlags.UseSeparateSliderForObjects) ? m_currentRoomTiles.Count : m_currentRoomTiles.Count + m_currentRoomObjects.Count;
				m_tileCap += m_animationSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
				if (m_tileCap >= max) {
					m_tileCap = max;
					if (m_flags.HasFlag(RoomViewerFlags.IsVideoMode)) {
						if (!m_flags.HasFlag(RoomViewerFlags.VideoModeDelaying)) {
							m_videoModeFlag = false;
							m_flags |= RoomViewerFlags.VideoModeDelaying;
							m_videoModeTimer = 5f;
						}
					} else {
						m_flags &= ~RoomViewerFlags.IsAnimating;
					}
					if (m_flags.HasFlag(RoomViewerFlags.VideoModeDelaying)) {
						m_videoModeTimer -= gameTime.ElapsedGameTime.TotalSeconds;
						if (m_videoModeTimer <= 0) {
							if (m_videoModeFlag) { //begin anim
								m_videoModeFlag = false;
								m_flags &= ~(RoomViewerFlags.VideoModeDelaying);
								m_flags |= RoomViewerFlags.FollowLatestRect;
								m_tileCap = 0;
								m_scale = 3;
							} else { //switcharoo
								m_selectedRoom++;
								if (m_selectedRoom == m_roomChunk.Count) {
									m_selectedRoom--;
									m_flags = m_flagBackup;
									m_videoModeFlag = false;
								} else {
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
					}
				}
			}

			if (oldTileCap != m_tileCap && m_flags.HasFlag(RoomViewerFlags.FollowLatestRect)) { //NOTE: the placement of this matter, it has to be below the animate code
				float tileCamCenterX, tileCamCenterY;
				if (m_tileCap-1 < m_currentRoomTiles.Count && m_currentRoomTiles.Count != 0) {
					//Console.WriteLine($"{GetCurrentTileIndex()}");
					var tileCurrent = m_currentRoomTiles[GetCurrentTileIndex()];
					tileCamCenterX = -((tileCurrent.X + tileCurrent.Width / 2) * m_scale - GraphicsDevice.Viewport.Width / 2);
					tileCamCenterY = -((tileCurrent.Y + tileCurrent.Height / 2) * m_scale - GraphicsDevice.Viewport.Height / 2);
				} else {
					var rect = m_objectRects[GetCurrentObjectIndex()];

					tileCamCenterX = -((rect.position.X + rect.imageSize.X / 2) * m_scale - GraphicsDevice.Viewport.Width / 2);
					tileCamCenterY = -((rect.position.Y + rect.imageSize.Y / 2) * m_scale - GraphicsDevice.Viewport.Height / 2);
				}

				m_tilemapTransform = (new Vector2(
					Math.Clamp(m_tilemapTransform.X, tileCamCenterX - 200, tileCamCenterX + 200),
					Math.Clamp(m_tilemapTransform.Y, tileCamCenterY - 200, tileCamCenterY + 200)
				));
			}

			base.Update(gameTime);
		}

		public GameMain(string[] args) {
			m_gdm = new GraphicsDeviceManager(this) {
				PreferredBackBufferWidth = 1360,
				PreferredBackBufferHeight = 860,
				SynchronizeWithVerticalRetrace = false,
				PreferMultiSampling = false,
			};
			IsMouseVisible = true;
			IsFixedTimeStep = false;

			m_gdm.ApplyChanges();

			using FileStream fs = new(args[0], FileMode.Open);
			m_dataReader = new GMDataReader(fs, fs.Name);
		}

		protected override void Initialize() {
			m_imRenderer = new ImGuiRenderer(this);
			m_imRenderer.RebuildFontAtlas();
			m_io = ImGui.GetIO();

			base.Initialize();
		}
		protected override void LoadContent() {
			m_spriteBatch = new SpriteBatch(GraphicsDevice);
			Liner.Init(this);

			//Load gamemaker wad
			m_dataReader.Deserialize();
			m_data = m_dataReader.Data;

			//Load texture pages as xna texture2d
			foreach (GMTexturePage texture in m_data.GetChunk<GMChunkTXTR>().List) {
				if (texture.TextureData.IsQoi) {
					throw new NotImplementedException(
						"I don't support gms2, let alone a recent version."
						);
				}

				//I swear i couldn't find a better way of doing this shit

				//int texWidth = (int)((UInt32)((texBlob[0x10] << 24) | (texBlob[0x11] << 16) | (texBlob[0x12] << 8) | texBlob[0x13]));
				//int texHeight = (int)((UInt32)((texBlob[0x14] << 24) | (texBlob[0x15] << 16) | (texBlob[0x16] << 8) | texBlob[0x17]));


				using (var stream = new MemoryStream(texture.TextureData.Data.Memory.ToArray())) {
					m_textures.Add(Texture2D.FromStream(GraphicsDevice, stream));
				}
			}

			//Room name list
			List<string> roomNames = new();
			foreach (GMRoom room in m_data.GetChunk<GMChunkROOM>().List) {
				roomNames.Add(room.Name.ToString());
			}
			m_roomNames = roomNames.ToArray();

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

			//Load missing texture
			FileStream fs = new FileStream(@"NoSpriteIcon.png", FileMode.Open);
			m_missingTexture = Texture2D.FromStream(GraphicsDevice, fs);
			fs.Dispose();
			fs.Close();

			//Load border texture
			fs = new FileStream(@"Putrid10x10.png", FileMode.Open);
			m_borderTexture = Texture2D.FromStream(GraphicsDevice, fs);
			fs.Dispose();
			fs.Close();

			SetRoomCache();

			base.LoadContent();
		}
		protected override void UnloadContent() {
			m_spriteBatch.Dispose();

			//Dispose gm textures
			//ClearCachedTextures();
			foreach (var texture in m_textures) {
				texture.Dispose();
			}

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
