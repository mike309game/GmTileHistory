#What is this
This is a tool for visualising the tile orders of tiles in GameMaker games, and for visualising certain properties in rooms that a game may not commonly use.
#How
GameMaker stores tiles as a list of rectangles with properties such as what texture to use and a rectangle of the area in the texture that defines this tile's graphic, and the order of this list is stored in a semi-intact order of when the tiles were placed.
#Limitations
Before explaining, keep in mind tiles and objects have a value called depth which is used by GameMaker to tell when a sprite should be drawn. For instance, a tile with a depth value of 1000 will be drawn after something with a depth of -900.
With tiles, depth values can be used as layer IDs.
The only apparent way GameMaker modifies the order of this rectangle list is it puts higher depth tiles (or "layers") earlier in the list, but the order of tiles that share the same depth, or "layer", is kept.
I'm not sure how to properly implement GMS2 legacy layer tiles (and object orders to an extent) yet, so perhaps the order of tiles (and objects) in these games may not be accurate. If you know how orders with those games work, a PR is welcome.
