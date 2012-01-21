﻿using System;
using System.Linq;
using System.Drawing;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using MegaMan.Common;
using System.Collections.Generic;

namespace MegaMan.Engine
{
    public class MapHandler : IHandleGameEvents
    {
        private int playerDeadCount;

        private Action updateFunc;
        private Action<SpriteBatch> drawFunc;

        private string startScreen;
        private int startX, startY;

        private int readyBlinkTime;
        private int readyBlinks;
        private readonly Image readyImage;
        private readonly Texture2D readyTexture;

        private readonly Music music;

        private Dictionary<string, ScreenHandler> screens;

        private PauseScreen pauseScreen;

        private JoinHandler currentJoin;
        private ScreenHandler nextScreen;

        private GamePlay gamePlay;

        public Map Map { get; private set; }

        public HandlerTransfer WinHandler { get; set; }

        public HandlerTransfer LoseHandler { get; set; }

        public ScreenHandler CurrentScreen { get; private set; }

        public Player Player { get { return gamePlay.Player; } }

        public PositionComponent PlayerPos;
        public int PlayerLives { get; set; }

        public event Action<HandlerTransfer> End;

        public MapHandler(Map map, PauseScreen pauseScreen, Dictionary<string, ScreenHandler> screens, GamePlay gamePlay)
        {
            Map = map;
            this.pauseScreen = pauseScreen;
            startScreen = Map.StartScreen;
            if (string.IsNullOrEmpty(startScreen)) startScreen = Map.Screens.Keys.First();
            startX = Map.PlayerStartX;
            startY = Map.PlayerStartY;

            string intropath = (map.MusicIntroPath != null) ? map.MusicIntroPath.Absolute : null;
            string looppath = (map.MusicLoopPath != null) ? map.MusicLoopPath.Absolute : null;
            if (intropath != null || looppath != null) music = Engine.Instance.SoundSystem.LoadMusic(intropath, looppath, 1);

            String imagePath = Path.Combine(Game.CurrentGame.BasePath, @"images\ready.png");
            readyImage = Image.FromFile(imagePath);
            StreamReader sr = new StreamReader(imagePath);
            readyTexture = Texture2D.FromStream(Engine.Instance.GraphicsDevice, sr.BaseStream);

            map.Tileset.SetTextures(Engine.Instance.GraphicsDevice);

            if (pauseScreen != null) pauseScreen.End += pauseScreen_Unpaused;

            this.screens = screens;

            this.gamePlay = gamePlay;
            PlayerPos = Player.Entity.GetComponent<PositionComponent>();
            PlayerLives = 2;
        }

        void BlinkReady(GameRenderEventArgs e)
        {
            if (readyBlinkTime >= 0)
            {
                if (Engine.Instance.Foreground) 
                {
                    e.Layers.ForegroundBatch.Draw(
                        readyTexture,
                        new Microsoft.Xna.Framework.Vector2(
                            (Game.CurrentGame.PixelsAcross - readyImage.Width) / 2,
                            ((Game.CurrentGame.PixelsDown - readyImage.Height) / 2) - 24
                        ),
                        e.OpacityColor);
                }
            }
            readyBlinkTime++;
            if (readyBlinkTime > 8)
            {
                readyBlinkTime = -8;
                readyBlinks++;
                if (readyBlinks >= 8)
                {
                    Engine.Instance.GameRender -= BlinkReady;
                    BeginPlay();
                }
            }
        }

        private void Player_Death()
        {
            if (music != null) music.Stop();
            Engine.Instance.SoundSystem.StopMusicNsf();
            if (CurrentScreen.Music != null) CurrentScreen.Music.Stop();
            
            playerDeadCount = 0;
            updateFunc = DeadUpdate;
            PlayerLives--;
        }

        private void BeginPlay()
        {
            Player.Entity.Start(CurrentScreen);
            Player.Entity.GetComponent<SpriteComponent>().Visible = true;

            StateMessage msg = new StateMessage(null, "Teleport");
            PlayerPos.SetPosition(new PointF(startX, 0));
            Game.CurrentGame.Player.Entity.SendMessage(msg);
            Action teleport = () => {};
            teleport += () =>
            {
                if (PlayerPos.Position.Y >= startY)
                {
                    PlayerPos.SetPosition(new PointF(startX, startY));
                    Player.Entity.SendMessage(new StateMessage(null, "TeleportEnd"));
                    gamePlay.GameThink -= teleport;
                    updateFunc = Update;
                }
            };
            gamePlay.GameThink += teleport;
        }

        private void Draw(SpriteBatch batch)
        {
            CurrentScreen.Draw(batch, PlayerPos.Position);
        }

        private void DeadUpdate()
        {
            playerDeadCount++;
            if (playerDeadCount >= Const.MapDeadFrames)
            {
                updateFunc = null;
                Engine.Instance.FadeTransition(Reset);
            }
        }

        private void Reset()
        {
            StopHandler();
            GameEntity.StopAll();

            if (PlayerLives < 0) // game over!
            {
                if (End != null) End(LoseHandler);

                PlayerLives = 2;
            }
            else
            {
                StartHandler();
            }
        }

        // swaps nextscreen for currentscreen and makes necessary adjustments to player
        // does not necessary represent the "end" of a scroll operation (boss doors still have to close)
        private void ScrollDone(JoinHandler join)
        {
            Game.CurrentGame.Player.Entity.Paused = false;
            join.ScrollDone -= ScrollDone;
            ChangeScreen(nextScreen);

            updateFunc = Update;
            drawFunc = Draw;

            // check for continue points
            if (Map.ContinuePoints.ContainsKey(nextScreen.Screen.Name))
            {
                startScreen = nextScreen.Screen.Name;
                startX = Map.ContinuePoints[nextScreen.Screen.Name].X;
                startY = Map.ContinuePoints[nextScreen.Screen.Name].Y;
            }
        }

        private void ChangeScreen(ScreenHandler nextScreen)
        {
            ScreenHandler oldscreen = CurrentScreen;
            CurrentScreen = nextScreen;
            Game.CurrentGame.Player.Entity.Screen = CurrentScreen;
            oldscreen.Clean();
            StartScreen();

            if (nextScreen.Music != null || nextScreen.Screen.MusicNsfTrack != 0)
            {
                if (music != null) music.Stop();
                if (Map.MusicNsfTrack != 0) Engine.Instance.SoundSystem.StopMusicNsf();
                
            }

            if (nextScreen.Screen.MusicNsfTrack != 0) Engine.Instance.SoundSystem.PlayMusicNSF((uint)nextScreen.Screen.MusicNsfTrack);
            else if (nextScreen.Music != null) nextScreen.Music.Play();
        }

        private void Update()
        {
            CurrentScreen.Update();
        }

        private void OnScrollTriggered(JoinHandler join)
        {
            currentJoin = join;

            Player.Entity.Paused = true;
            nextScreen = screens[join.NextScreenName];
            join.BeginScroll(nextScreen, PlayerPos.Position);

            updateFunc = () => join.Update(PlayerPos);
            join.ScrollDone += ScrollDone;

            drawFunc = DrawJoin;

            StopScreen();
        }

        private void DrawJoin(SpriteBatch batch)
        {
            CurrentScreen.Draw(batch, PlayerPos.Position, 0, 0, currentJoin.OffsetX, currentJoin.OffsetY);
            nextScreen.Draw(batch, PlayerPos.Position, currentJoin.NextScreenX, currentJoin.NextScreenY, currentJoin.NextOffsetX, currentJoin.NextOffsetY);
        }

        private void StartScreen()
        {
            CurrentScreen.JoinTriggered += OnScrollTriggered;
            CurrentScreen.Teleport += OnTeleport;
            CurrentScreen.BossDefeated += BossDefeated;
            CurrentScreen.Start();
        }

        private void BossDefeated()
        {
            gamePlay.EndPlay();
            if (End != null && WinHandler != null)
            {
                End(WinHandler);
            }
        }

        private void StopScreen()
        {
            CurrentScreen.JoinTriggered -= OnScrollTriggered;
            CurrentScreen.Teleport -= OnTeleport;
            CurrentScreen.Stop();
        }

        private bool teleporting = false;
        private void OnTeleport(TeleportInfo info)
        {
            if (teleporting) return;
            teleporting = true;
            Action<string> setpos = (s) => { };
            if (info.TargetScreen == CurrentScreen.Screen.Name)
            {
                setpos = (state) =>
                {
                    PlayerPos.SetPosition(info.To);
                    (Game.CurrentGame.Player.Entity.GetComponent<StateComponent>()).StateChanged -= setpos;
                    Game.CurrentGame.Player.Entity.SendMessage(new StateMessage(null, "TeleportEnd"));
                    teleporting = false;
                    (Game.CurrentGame.Player.Entity.GetComponent<MovementComponent>()).CanMove = true;
                };
            }
            else
            {
                setpos = state =>
                {
                    (Game.CurrentGame.Player.Entity.GetComponent<SpriteComponent>()).Visible = false;
                    (Game.CurrentGame.Player.Entity.GetComponent<StateComponent>()).StateChanged -= setpos;
                    Engine.Instance.FadeTransition(
                        () => 
                    { 
                        StopScreen();
                        ChangeScreen(screens[info.TargetScreen]);
                        PlayerPos.SetPosition(info.To); // do it here so drawing is correct for fade-in
                    }, () =>
                    {
                        (Game.CurrentGame.Player.Entity.GetComponent<SpriteComponent>()).Visible = true;
                        Game.CurrentGame.Player.Entity.SendMessage(new StateMessage(null, "TeleportEnd"));
                        (Game.CurrentGame.Player.Entity.GetComponent<MovementComponent>()).CanMove = true;
                        teleporting = false;
                    });
                };
            }
            (Game.CurrentGame.Player.Entity.GetComponent<MovementComponent>()).CanMove = false;
            Game.CurrentGame.Player.Entity.SendMessage(new StateMessage(null, "TeleportBlink"));
            (Game.CurrentGame.Player.Entity.GetComponent<StateComponent>()).StateChanged += setpos;
        }

        #region IHandleGameEvents Members

        public void StartHandler()
        {
            Player.ResetEntity();

            Player.Entity.Stopped += Player_Death;
            PlayerPos = Player.Entity.GetComponent<PositionComponent>();


            if (!Map.Screens.ContainsKey(startScreen)) throw new GameEntityException("The start screen for \""+Map.Name+"\" is supposed to be \""+startScreen+"\", but it doesn't exist!");
            CurrentScreen = screens[startScreen];
            StartScreen();

            if (music != null) music.Play();
            if (Map.MusicNsfTrack != 0) Engine.Instance.SoundSystem.PlayMusicNSF((uint)Map.MusicNsfTrack);

            // updateFunc isn't set until BeginPlay
            drawFunc = Draw;

            Unpause();

            // ready flashing
            readyBlinkTime = 0;
            readyBlinks = 0;
            Engine.Instance.GameRender += BlinkReady;

            Player.Entity.GetComponent<SpriteComponent>().Visible = false;

            Player.Entity.Start();

            // make sure we can move
            (Game.CurrentGame.Player.Entity.GetComponent<InputComponent>()).Paused = false;
        }

        public void StopHandler()
        {
            Player.Entity.Stopped -= Player_Death;

            if (CurrentScreen != null)
            {
                StopScreen();
                CurrentScreen.Clean();
            }

            if (music != null) music.Stop();
            if (Map.MusicNsfTrack != 0) Engine.Instance.SoundSystem.StopMusicNsf();

            if (pauseScreen != null) pauseScreen.StopHandler();

            Pause();
            Engine.Instance.GameRender -= BlinkReady;
        }

        private void Pause()
        {
            gamePlay.StopHandler();

            Engine.Instance.GameLogicTick -= GameTick;
            Engine.Instance.GameRender -= GameRender;
            
            Engine.Instance.GameInputReceived -= GameInputReceived;
        }

        private void Unpause()
        {
            Engine.Instance.GameLogicTick += GameTick;
            Engine.Instance.GameRender += GameRender;
            
            Engine.Instance.GameInputReceived += GameInputReceived;

            gamePlay.StartHandler();
        }

        private void GameInputReceived(GameInputEventArgs e)
        {
            if (updateFunc == null || (Game.CurrentGame.Player.Entity.GetComponent<InputComponent>()).Paused) return;
            if (e.Input == GameInput.Start && e.Pressed)
            {
                // has to handle both pause and unpause, in case a pause screen isn't defined
                if (pauseScreen == null)
                {
                    if (Game.CurrentGame.Paused)
                    {
                        Game.CurrentGame.Unpause();
                    }
                    else
                    {
                        Game.CurrentGame.Pause();
                    }
                }
                else
                {
                    // clearly we are unpaused, otherwise we would not be receiving this input
                    Game.CurrentGame.Pause();
                    pauseScreen.Sound();
                    Engine.Instance.FadeTransition(OpenPauseScreen);
                }
            }
        }

        private void OpenPauseScreen()
        {
            Pause();
            if (pauseScreen != null)
            {
                pauseScreen.StartHandler();
            }
        }

        private void pauseScreen_Unpaused(HandlerTransfer nextHandler)
        {
            pauseScreen.Sound();
            Engine.Instance.FadeTransition(ClosePauseScreen, Game.CurrentGame.Unpause);
        }

        private void ClosePauseScreen()
        {
            Unpause();
            if (pauseScreen != null)
            {
                pauseScreen.ApplyWeapon();
                pauseScreen.StopHandler();
            }
        }

        private void GameTick(GameTickEventArgs e)
        {
            if (updateFunc != null) updateFunc();

            foreach (Tile t in Map.Tileset)
            {
                t.Sprite.Update();
            }
        }

        public void GameRender(GameRenderEventArgs e)
        {
            if (drawFunc != null && Engine.Instance.Background) drawFunc(e.Layers.BackgroundBatch);
        }

        #endregion
    }
}
