﻿using System.Collections.Generic;
using StardewValley;
using SObject = StardewValley.Object;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley.TerrainFeatures;
using StardewValley.Objects;
using StardewValley.Locations;
using xTile;
using xTile.Tiles;
using xTile.Layers;
using System.IO;
using PyTK.Types;
using xTile.Dimensions;
using System;
using Netcode;
using xTile.ObjectModel;
using PyTK.Tiled;
using Newtonsoft.Json;
using Microsoft.Xna.Framework.Graphics;

namespace PyTK.Extensions
{
    public static class PyMaps
    {
        internal static IModHelper Helper { get; } = PyTKMod._helper;
        internal static IMonitor Monitor { get; } = PyTKMod._monitor;

        /// <summary>Looks for an object of the requested type on this map position.</summary>
        /// <returns>Return the object or null.</returns>
        public static T sObjectOnMap<T>(this Vector2 t) where T : SObject
        {
            if (Game1.currentLocation is GameLocation location)
            {
                if (location.netObjects.FieldDict.TryGetValue(t, out NetRef<SObject> netRaw) && netRaw.Value is T netValue)
                    return netValue;
                if (location.overlayObjects.TryGetValue(t, out SObject overlayRaw) && overlayRaw is T overlayValue)
                    return overlayValue;
            }
            return null;
        }

        /// <summary>Looks for an object of the requested type on this map position.</summary>
        /// <returns>Return the object or null.</returns>
        public static T terrainOnMap<T>(this Vector2 t) where T : TerrainFeature
        {
            if (Game1.currentLocation is GameLocation location)
            {
                if (location.terrainFeatures.FieldDict.TryGetValue(t, out NetRef<TerrainFeature> raw) && raw.Value is T value)
                    return value;
            }

            return null;
        }

        /// <summary>Looks for an object of the requested type on this map position.</summary>
        /// <returns>Return the object or null.</returns>
        public static T furnitureOnMap<T>(this Vector2 t) where T : Furniture
        {
            if (Game1.currentLocation is DecoratableLocation location)
            {
                List<Furniture> furniture = new List<Furniture>(location.furniture);
                return ((T) furniture.Find(f => f.getBoundingBox(t).Intersects(new Microsoft.Xna.Framework.Rectangle((int) t.X * Game1.tileSize, (int) t.Y * Game1.tileSize, Game1.tileSize, Game1.tileSize))));
            }
            return null;
        }

        /// <summary>Looks for an object of the requested type on this map position.</summary>
        /// <returns>Return the object or null.</returns>
        public static SObject sObjectOnMap(this Vector2 t)
        {
            if (Game1.currentLocation is GameLocation location)
            {
                if (location.netObjects.FieldDict.TryGetValue(t, out NetRef<SObject> netObj))
                    return netObj;
                if (location.overlayObjects.TryGetValue(t, out SObject overlayObj))
                    return overlayObj;
            }
            return null;
        }

        public static bool hasTileSheet(this Map map, TileSheet tilesheet)
        {
            foreach (TileSheet ts in map.TileSheets)
                if (tilesheet.ImageSource.EndsWith(new FileInfo(ts.ImageSource).Name) || tilesheet.Id == ts.Id)
                    return true;

            return false;
        }

        public static Map enableMoreMapLayers(this Map map)
        {
            foreach (Layer layer in map.Layers)
            {
                if (layer.Properties.ContainsKey("OffestXReset"))
                {
                    layer.Properties["offsetx"] = layer.Properties["OffestXReset"];
                    layer.Properties["offsety"] = layer.Properties["OffestYReset"];
                }

                if (layer.Properties.Keys.Contains("DrawChecked"))
                    continue;

                if (layer.Properties.ContainsKey("Draw") && map.GetLayer(layer.Properties["Draw"]) is Layer maplayer)
                    maplayer.AfterDraw += (s, e) => drawLayer(layer, Location.Origin, layer.Properties.ContainsKey("WrapAround"));
                else if (layer.Properties.ContainsKey("DrawBefore") && map.GetLayer(layer.Properties["DrawBefore"]) is Layer maplayerBefore)
                    maplayerBefore.BeforeDraw += (s, e) => drawLayer(layer, Location.Origin, layer.Properties.ContainsKey("WrapAround"));

                layer.Properties["DrawChecked"] = true;
            }
            return map;
        }

        private static void drawLayer(Layer layer, Location offset, bool wrap = false)
        {
            if (layer.Properties.ContainsKey("offsetx") && layer.Properties.ContainsKey("offsety"))
            {
                offset = new Location(int.Parse(layer.Properties["offsetx"]), int.Parse(layer.Properties["offsety"]));
                if (!layer.Properties.ContainsKey("OffestXReset"))
                {
                    layer.Properties["OffestXReset"] = offset.X;
                    layer.Properties["OffestYReset"] = offset.Y;
                }
            }

            if (!layer.Properties.ContainsKey("StartX"))
            {
                Vector2 local = Game1.GlobalToLocal(new Vector2(offset.X,offset.Y));
                layer.Properties["StartX"] = local.X;
                layer.Properties["StartY"] = local.Y;
            }

            if (layer.Properties.ContainsKey("AutoScrollX"))
            {
                offset.X += int.Parse(layer.Properties["AutoScrollX"]);
                layer.Properties["offsetx"] = offset.X;
            }

            if (layer.Properties.ContainsKey("AutoScrollY"))
            {
                offset.Y += int.Parse(layer.Properties["AutoScrollY"]);
                layer.Properties["offsety"] = offset.Y;
            }


            if (layer.Properties.ContainsKey("isImageLayer"))
                drawImageLayer(layer, offset, wrap);
            else
                layer.Draw(Game1.mapDisplayDevice, Game1.viewport, offset, wrap, Game1.pixelZoom);
        }

        private static void drawImageLayer(Layer layer, Location offset, bool wrap = false)
        {
            Texture2D texture = Helper.Content.Load<Texture2D>(layer.Map.GetTileSheet("zImageSheet_" + layer.Id).ImageSource, ContentSource.GameContent);
            Vector2 pos = new Vector2(offset.X, offset.Y);


            pos = Game1.GlobalToLocal(pos);

            if (layer.Properties.ContainsKey("ParallaxX") || layer.Properties.ContainsKey("ParallaxY"))
            {
                Vector2 end = pos;
                if (layer.Properties.ContainsKey("OffestXReset"))
                {
                    end.X = layer.Properties["OffestXReset"];
                    end.Y = layer.Properties["OffestYReset"];
                }
                end = Game1.GlobalToLocal(end);

                Vector2 start = new Vector2(layer.Properties["StartX"], layer.Properties["StartY"]);

                Vector2 dif = start - end;

                if (layer.Properties.ContainsKey("ParallaxX"))
                    pos.X += ((float.Parse(layer.Properties["ParallaxX"]) * dif.X) / 100f) - dif.X;

                if (layer.Properties.ContainsKey("ParallaxY"))
                    pos.Y += ((float.Parse(layer.Properties["ParallaxY"]) * dif.Y) / 100f) - dif.Y;

            }

            Microsoft.Xna.Framework.Rectangle dest = new Microsoft.Xna.Framework.Rectangle((int)pos.X, (int)pos.Y, texture.Width * Game1.pixelZoom, texture.Height * Game1.pixelZoom);
            Game1.spriteBatch.Draw(texture, dest, Color.White * (float)layer.Properties["opacity"]);
            Vector2 oPos = pos;
            if (wrap)
            {
                if (layer.Properties["WrapAround"] != "Y")
                    while (pos.X > 0)
                    {
                        pos.X -= dest.Width;
                        dest = new Microsoft.Xna.Framework.Rectangle((int)pos.X, (int)pos.Y, dest.Width, dest.Height);
                        Game1.spriteBatch.Draw(texture, dest, Color.White * (float)layer.Properties["opacity"]);
                    }
                else
                    while (pos.Y > 0)
                    {
                        pos.Y -= dest.Height;
                        dest = new Microsoft.Xna.Framework.Rectangle((int)pos.X, (int)pos.Y, dest.Width, dest.Height);
                        Game1.spriteBatch.Draw(texture, dest, Color.White * (float)layer.Properties["opacity"]);
                    }
                pos = oPos;
                if (layer.Properties["WrapAround"] != "Y")
                    while (pos.X < Game1.viewport.X + Game1.viewport.Width)
                    {
                        pos.X += dest.Width;
                        dest = new Microsoft.Xna.Framework.Rectangle((int)pos.X, (int)pos.Y, dest.Width, dest.Height);
                        Game1.spriteBatch.Draw(texture, dest, Color.White * (float)layer.Properties["opacity"]);
                    }
                else
                    while (pos.Y < Game1.viewport.Y + Game1.viewport.Height)
                    {
                        pos.Y += dest.Height;
                        dest = new Microsoft.Xna.Framework.Rectangle((int)pos.X, (int)pos.Y, dest.Width, dest.Height);
                        Game1.spriteBatch.Draw(texture, dest, Color.White * (float)layer.Properties["opacity"]);
                    }
            }
        }


        public static Map switchLayers(this Map t, string layer1, string layer2)
        {
            Layer newLayer1 = t.GetLayer(layer1);
            Layer newLayer2 = t.GetLayer(layer2);

            t.RemoveLayer(t.GetLayer(layer1));
            t.RemoveLayer(t.GetLayer(layer2));

            newLayer1.Id = layer2;
            newLayer2.Id = layer1;
            
            t.AddLayer(newLayer1);
            t.AddLayer(newLayer2);
            
            return t;
        }

        public static Map switchTileBetweenLayers(this Map t, string layer1, string layer2, int x, int y)
        {
            Location tileLocation = new Location(x , y);

            Tile tile1 = t.GetLayer(layer1).Tiles[tileLocation];
            Tile tile2 = t.GetLayer(layer2).Tiles[tileLocation];

            t.GetLayer(layer1).Tiles[tileLocation] = tile2;
            t.GetLayer(layer2).Tiles[tileLocation] = tile1;
            
            return t;
        }

        public static GameLocation clearArea(this GameLocation l, Microsoft.Xna.Framework.Rectangle area)
        {

            for (int x = area.X; x < area.Width; x++)
                for (int y = area.Y; y < area.Height; y++)
                {
                    l.objects.Remove(new Vector2(x, y));
                    l.largeTerrainFeatures.Remove(new List<LargeTerrainFeature>(l.largeTerrainFeatures).Find(p => p.tilePosition.Value == new Vector2(x,y)));
                    l.terrainFeatures.Remove(new Vector2(x, y));
                }

            return l;
        }

        public static Map mergeInto(this Map t, Map map, Vector2 position, Microsoft.Xna.Framework.Rectangle? sourceArea = null, bool includeEmpty = true, bool properties = true)
        {
            Microsoft.Xna.Framework.Rectangle sourceRectangle = sourceArea.HasValue ? sourceArea.Value : new Microsoft.Xna.Framework.Rectangle(0, 0, t.DisplayWidth / Game1.tileSize, t.DisplayHeight / Game1.tileSize);

            foreach (TileSheet tilesheet in t.TileSheets)
                if (!map.hasTileSheet(tilesheet))
                    map.AddTileSheet(new TileSheet(tilesheet.Id, map, tilesheet.ImageSource, tilesheet.SheetSize, tilesheet.TileSize));

            if(properties)
            foreach (KeyValuePair<string, PropertyValue> p in t.Properties)
                if (map.Properties.ContainsKey(p.Key))
                    if (p.Key == "EntryAction")
                        map.Properties[p.Key] = map.Properties[p.Key] + ";" + p.Value;
                    else
                        map.Properties[p.Key] = p.Value;
                else
                    map.Properties.Add(p);

            for (Vector2 _x = new Vector2(sourceRectangle.X, position.X); _x.X < sourceRectangle.Width; _x += new Vector2(1, 1))
            {
                for (Vector2 _y = new Vector2(sourceRectangle.Y, position.Y); _y.X < sourceRectangle.Height; _y += new Vector2(1, 1))
                {
                    foreach (Layer layer in t.Layers)
                    {
                        

                        Tile sourceTile = layer.Tiles[(int)_x.X, (int)_y.X];
                        Layer mapLayer = map.GetLayer(layer.Id);

                        if (mapLayer == null)
                        {
                            map.InsertLayer(new Layer(layer.Id, map, map.Layers[0].LayerSize, map.Layers[0].TileSize), map.Layers.Count);
                            mapLayer = map.GetLayer(layer.Id);
                        }

                        if (properties)
                            foreach (var prop in layer.Properties)
                                if (!mapLayer.Properties.ContainsKey(prop.Key))
                                    mapLayer.Properties.Add(prop);
                                else
                                    mapLayer.Properties[prop.Key] = prop.Value;

                        if (sourceTile == null)
                        {
                            if (includeEmpty)
                            {
                                try
                                {
                                    mapLayer.Tiles[(int)_x.Y, (int)_y.Y] = null;
                                }
                                catch { }
                            }
                            continue;
                        }

                        TileSheet tilesheet = map.GetTileSheet(sourceTile.TileSheet.Id);
                        Tile newTile = new StaticTile(mapLayer, tilesheet, BlendMode.Additive, sourceTile.TileIndex);

                        if (sourceTile is AnimatedTile aniTile)
                        {
                            List<StaticTile> staticTiles = new List<StaticTile>();

                            foreach (StaticTile frame in aniTile.TileFrames)
                                staticTiles.Add(new StaticTile(mapLayer, tilesheet, BlendMode.Additive, frame.TileIndex));

                            newTile = new AnimatedTile(mapLayer, staticTiles.ToArray(), aniTile.FrameInterval);
                        }

                        if(properties)
                            foreach (var prop in sourceTile.Properties)
                                newTile.Properties.Add(prop);
                        try
                        {
                            mapLayer.Tiles[(int)_x.Y, (int)_y.Y] = newTile;
                        }catch(Exception e){
                            Monitor.Log($"{e.Message} ({map.DisplayWidth} -> {layer.Id} -> {_x.Y}:{_y.Y})");
                        }
                    }

                }
            }
            return map;
        }

        public static void addAction(this Map m, Vector2 position, TileAction action, string args)
        {
            m.GetLayer("Buildings").PickTile(new Location((int)position.X * Game1.tileSize, (int)position.Y * Game1.tileSize), Game1.viewport.Size).Properties.AddOrReplace("Action", action.trigger + " " + args);
        }

        public static void addAction(this Map m, Vector2 position, string trigger, string args)
        {
            m.GetLayer("Buildings").PickTile(new Location((int)position.X * Game1.tileSize, (int)position.Y * Game1.tileSize), Game1.viewport.Size).Properties.AddOrReplace("Action", trigger + " " + args);
        }

        public static void addTouchAction(this Map m, Vector2 position, TileAction action, string args)
        {
            m.GetLayer("Back").PickTile(new Location((int)position.X * Game1.tileSize, (int)position.Y * Game1.tileSize), Game1.viewport.Size).Properties.AddOrReplace("TouchAction", action.trigger + " " + args);
        }

        public static void addTouchAction(this Map m, Vector2 position, string trigger, string args)
        {
            m.GetLayer("Back").PickTile(new Location((int)position.X * Game1.tileSize, (int)position.Y * Game1.tileSize), Game1.viewport.Size).Properties.AddOrReplace("TouchAction", trigger + " " + args);
        }
    }
}
