﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using MegaMan.Common;

namespace MegaMan.Engine
{
    public class ScreenHandler : IScreenInformation
    {
        private readonly MapSquare[][] tiles;
        public Screen Screen { get; private set; }
        private readonly List<BlocksPattern> patterns;
        private GameEntity[] entities;
        private readonly List<GameEntity> spawnedEntities;
        private bool[] spawnable;
        private readonly List<JoinHandler> joins;
        private readonly List<bool> teleportEnabled;
        private readonly IGameplayContainer container;
        private readonly GameEntity player;
        private readonly PositionComponent playerPos;

        private float centerX, centerY;

        public Music Music { get; private set; }

        public float OffsetX { get; private set; }
        public float OffsetY { get; private set; }

        public int TileSize { get { return Screen.Tileset.TileSize; } }

        public event Action<JoinHandler> JoinTriggered;
        public event Action<TeleportInfo> Teleport;

        public event Action BossDefeated;

        public ScreenHandler(Screen screen, MapSquare[][] tiles, IEnumerable<JoinHandler> joins, IEnumerable<BlocksPattern> blockPatterns, Music music, IGameplayContainer container)
        {
            Screen = screen;
            patterns = new List<BlocksPattern>();
            spawnedEntities = new List<GameEntity>();

            this.tiles = tiles;

            this.patterns = blockPatterns.ToList();

            this.joins = joins.ToList();

            teleportEnabled = new List<bool>(screen.Teleports.Select(info => false));

            Music = music;

            this.container = container;
            this.player = container.Player;
            playerPos = player.GetComponent<PositionComponent>();
        }

        public void Start()
        {
            entities = new GameEntity[Screen.EnemyInfo.Count];
            spawnable = new bool[Screen.EnemyInfo.Count];

            // place persistent entities
            for (int i = 0; i < Screen.EnemyInfo.Count; i++)
            {
                if (entities[i] != null) continue; // already on screen

                PlaceEntity(i);
            }

            foreach (BlocksPattern pattern in patterns)
            {
                pattern.Start(this);
            }

            container.GameThink += Instance_GameThink;
        }

        // these frames only happen if we are not paused / scrolling
        public void Update()
        {
            foreach (JoinHandler join in joins)
            {
                if (join.Trigger(playerPos.Position))
                {
                    if (JoinTriggered != null) JoinTriggered(join);
                    return;
                }
            }

            // check for teleports
            for (int i = 0; i < Screen.Teleports.Count; i++)
            {
                TeleportInfo teleport = Screen.Teleports[i];

                if (teleportEnabled[i])
                {
                    if (Math.Abs(playerPos.Position.X - teleport.From.X) <= 2 && Math.Abs(playerPos.Position.Y - teleport.From.Y) <= 8)
                    {
                        if (Teleport != null) Teleport(teleport);
                        break;
                    }
                }
                else if (Math.Abs(playerPos.Position.X - teleport.From.X) >= 16 || Math.Abs(playerPos.Position.Y - teleport.From.Y) >= 16)
                {
                    teleportEnabled[i] = true;
                }
            }

            // if the player is not colliding, they'll be allowed to pass through the walls (e.g. teleporting)
            if ((player.GetComponent<CollisionComponent>()).Enabled)
            {
                // now if we aren't scrolling, hold the player at the screen borders
                if (playerPos.Position.X >= Screen.PixelWidth - Const.PlayerScrollTrigger)
                {
                    playerPos.SetPosition(new PointF(Screen.PixelWidth - Const.PlayerScrollTrigger, playerPos.Position.Y));
                }
                else if (playerPos.Position.X <= Const.PlayerScrollTrigger)
                {
                    playerPos.SetPosition(new PointF(Const.PlayerScrollTrigger, playerPos.Position.Y));
                }
                else if (playerPos.Position.Y > Screen.PixelHeight - Const.PlayerScrollTrigger)
                {
                    if (Game.CurrentGame.GravityFlip) playerPos.SetPosition(new PointF(playerPos.Position.X, Screen.PixelHeight - Const.PlayerScrollTrigger));
                    // bottomless pit death!
                    else if (playerPos.Position.Y > Game.CurrentGame.PixelsDown + 32) playerPos.Parent.Die();
                }
                else if (playerPos.Position.Y < Const.PlayerScrollTrigger)
                {
                    if (!Game.CurrentGame.GravityFlip) playerPos.SetPosition(new PointF(playerPos.Position.X, Const.PlayerScrollTrigger));
                    else if (playerPos.Position.Y < -32) playerPos.Parent.Die();
                }
            }
        }

        public void AddSpawnedEntity(GameEntity entity)
        {
            spawnedEntities.Add(entity);
        }

        // because it is a thinking event, it happens every frame
        private void Instance_GameThink()
        {
            // place any entities that have just appeared on screen
            for (int i = 0; i < Screen.EnemyInfo.Count; i++)
            {
                if (entities[i] != null) continue; // already on screen
                if (!IsOnScreen(Screen.EnemyInfo[i].screenX, Screen.EnemyInfo[i].screenY))
                {
                    spawnable[i] = true;    // it's off-screen, so it can spawn next time it's on screen
                    continue;
                }
                if (!spawnable[i]) continue;

                PlaceEntity(i);
            }
        }

        public bool IsOnScreen(float x, float y)
        {
            return x >= OffsetX && y >= OffsetY &&
                x <= OffsetX + Game.CurrentGame.PixelsAcross &&
                y <= OffsetY + Game.CurrentGame.PixelsDown;
        }

        private void PlaceEntity(int index)
        {
            spawnable[index] = false;
            EnemyCopyInfo info = Screen.EnemyInfo[index];

            GameEntity enemy = GameEntity.Get(info.enemy, container);
            if (enemy == null) return;
            PositionComponent pos = enemy.GetComponent<PositionComponent>();
            if (!pos.PersistOffScreen && !IsOnScreen(info.screenX, info.screenY)) return; // what a waste of that allocation...

            pos.SetPosition(new PointF(info.screenX, info.screenY));
            if (info.state != "Start")
            {
                StateMessage msg = new StateMessage(null, info.state);
                enemy.SendMessage(msg);
            }
            enemy.Start(this);
            if (info.boss)
            {
                HealthComponent health = enemy.GetComponent<HealthComponent>();
                health.DelayFill(120);
                BossFightTimer();
                enemy.Death += () =>
                {
                    if (Music != null) Music.FadeOut(30);
                    (player.GetComponent<InputComponent>()).Paused = true;
                    Engine.Instance.DelayedCall(() => player.SendMessage(new StateMessage(null, "TeleportStart")), null, 120);
                    Engine.Instance.DelayedCall(() => { if (BossDefeated != null) BossDefeated(); }, null, 240);
                };
            }
            if (info.pallete != "Default" && info.pallete != null)
            {
                (enemy.GetComponent<SpriteComponent>()).ChangeGroup(info.pallete);
            }
            entities[index] = enemy;
            enemy.Stopped += () => entities[index] = null;
        }

        private void BossFightTimer()
        {
            InputComponent input = player.GetComponent<InputComponent>();
            input.Paused = true;
            Engine.Instance.DelayedCall(() => { input.Paused = false; }, null, 200);
        }

        public void Stop()
        {
            for (int i = 0; i < entities.Length; i++ )
            {
                if (entities[i] != null) entities[i].Stop();
                entities[i] = null;
            }

            foreach (GameEntity entity in spawnedEntities)
            {
                entity.Stop();
            }
            spawnedEntities.Clear();

            foreach (BlocksPattern pattern in patterns)
            {
                pattern.Stop();
            }

            container.GameThink -= Instance_GameThink;
        }

        public void Clean()
        {
            foreach (JoinHandler join in joins)
            {
                join.Stop();
            }
        }

        public MapSquare SquareAt(int x, int y)
        {
            if (y < 0 || y >= tiles.GetLength(0)) return null;
            if (x < 0 || x >= tiles[y].GetLength(0)) return null;
            return tiles[y][x];
        }

        public IEnumerable<MapSquare> Tiles
        {
            get 
            {
                return tiles.SelectMany(row => row);
            }
        }

        public void Draw(Microsoft.Xna.Framework.Graphics.SpriteBatch batch, PointF playerPos, float adj_x = 0, float adj_y = 0, float off_x = 0, float off_y = 0)
        {
            int width = Screen.PixelWidth;
            int height = Screen.PixelHeight;

            OffsetX = OffsetY = 0;

            centerX = playerPos.X + adj_x;
            centerY = playerPos.Y + adj_y;

            if (centerX > Game.CurrentGame.PixelsAcross / 2)
            {
                OffsetX = centerX - Game.CurrentGame.PixelsAcross / 2;
                if (OffsetX > width - Game.CurrentGame.PixelsAcross) OffsetX = width - Game.CurrentGame.PixelsAcross;
            }

            if (centerY > Game.CurrentGame.PixelsDown / 2)
            {
                OffsetY = centerY - Game.CurrentGame.PixelsDown / 2;
                if (OffsetY > height - Game.CurrentGame.PixelsDown) OffsetY = height - Game.CurrentGame.PixelsDown;
                if (OffsetY < 0) OffsetY = 0;
            }

            OffsetX += off_x;
            OffsetY += off_y;

            Screen.DrawXna(batch, Engine.Instance.OpacityColor, -OffsetX, -OffsetY, Game.CurrentGame.PixelsAcross, Game.CurrentGame.PixelsDown);
        }

        public Tile TileAt(int tx, int ty)
        {
            return Screen.TileAt(tx, ty);
        }
    }
}
