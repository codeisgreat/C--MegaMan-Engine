﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Drawing;
using System.Xml.Linq;

namespace Mega_Man
{
    public class ScreenSizeChangedEventArgs : EventArgs
    {
        public int PixelsAcross { get; private set; }
        public int PixelsDown { get; private set; }

        public ScreenSizeChangedEventArgs(int across, int down)
        {
            PixelsAcross = across;
            PixelsDown = down;
        }
    }

    public class Game
    {
        public static Game CurrentGame { get; private set; }

        private string currentPath;
        private IHandleGameEvents currentHandler;
        private StageSelect select;
        private PauseScreen pauseScreen;
        private List<IHandleGameEvents> gameObjects;
        public MapHandler CurrentMap { get; private set; }
        public int PixelsAcross { get; private set; }
        public int PixelsDown { get; private set; }
        public float Gravity { get; private set; }
        public bool GravityFlip { get; set; }

        public bool Paused { get; private set; }

        public string BasePath { get; private set; }

        public static event EventHandler<ScreenSizeChangedEventArgs> ScreenSizeChanged;

        private Font font;

        public static void Load(string path)
        {
            if (CurrentGame != null)
            {
                CurrentGame.Unload();
            }
            CurrentGame = new Game();
            CurrentGame.LoadFile(path);
        }

        private void StopHandlers()
        {
            List<IHandleGameEvents> temp = new List<IHandleGameEvents>(gameObjects);
            foreach (IHandleGameEvents handler in temp) handler.StopHandler();
        }

        public void Unload()
        {
            StopHandlers();
            GameEntity.UnloadAll();
            select = null;
            Engine.Instance.UnloadAudio();
            FontSystem.Unload();
            CurrentGame = null;
        }

        public void Reset()
        {
            Unload();
            Load(currentPath);
        }

        private Game()
        {
            gameObjects = new List<IHandleGameEvents>();
            font = new Font(FontFamily.GenericMonospace, 14, FontStyle.Bold);

            Gravity = 0.25f;
            GravityFlip = false;
        }

        private void LoadFile(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("The project file does not exist: " + path);

            BasePath = System.IO.Path.GetDirectoryName(path);
            XElement reader = XElement.Load(path);

            XElement sizeNode = reader.Element("Size");
            if (sizeNode != null)
            {
                int across, down;
                if (!int.TryParse(sizeNode.Attribute("x").Value, out across)) throw new EntityXmlException(path, (sizeNode as IXmlLineInfo).LineNumber, null, "Size", "x", "Specified width was not a valid integer.");
                if (!int.TryParse(sizeNode.Attribute("y").Value, out down)) throw new EntityXmlException(path, (sizeNode as IXmlLineInfo).LineNumber, null, "Size", "y", "Specified height was not a valid integer.");
                PixelsDown = down;
                PixelsAcross = across;
                if (ScreenSizeChanged != null)
                {
                    ScreenSizeChangedEventArgs args = new ScreenSizeChangedEventArgs(PixelsAcross, PixelsDown);
                    ScreenSizeChanged(this, args);
                }
            }

            XElement stageNode = reader.Element("StageSelect");
            if (stageNode != null) select = new StageSelect(stageNode);

            XElement pauseNode = reader.Element("PauseScreen");
            if (pauseNode != null) pauseScreen = new PauseScreen(pauseNode);

            if (pauseScreen != null) pauseScreen.Unpaused += new Action(pauseScreen_Unpaused);

            foreach (XElement entityNode in reader.Elements("Entities"))
            {
                string enemyfile = System.IO.Path.Combine(BasePath, entityNode.Value);
                GameEntity.LoadEntities(enemyfile);
            }
            currentPath = path;

            currentHandler = select;
            select.MapSelected += new Action<string>(select_MapSelected);

            currentHandler.StartHandler();
        }

        private void select_MapSelected(string path)
        {
            currentHandler.StopHandler();
            CurrentMap = new MapHandler(new MegaMan.Map(path));
            currentHandler = CurrentMap;
            CurrentMap.StartHandler();
            CurrentMap.Paused += new Action(CurrentMap_Paused);
        }

        void CurrentMap_Paused()
        {
            Game.CurrentGame.Pause();
            if (pauseScreen != null) pauseScreen.Sound();
            Engine.Instance.FadeTransition(PauseScreen);
        }

        private void PauseScreen()
        {
            if (pauseScreen != null)
            {
                CurrentMap.Pause();
                pauseScreen.StartHandler();
            }
        }

        void pauseScreen_Unpaused()
        {
            if (pauseScreen != null) pauseScreen.Sound();
            Engine.Instance.FadeTransition(UnPause, Game.CurrentGame.Unpause);
        }

        public void UnPause()
        {
            CurrentMap.Unpause();
            if (pauseScreen != null) pauseScreen.StopHandler();
        }

        public void ResetMap()
        {
            if (CurrentMap == null) return;
            StopHandlers();
            CurrentMap.StopHandler();
            CurrentMap.StartHandler();
        }

        public void AddGameHandler(IHandleGameEvents handler)
        {
            gameObjects.Add(handler);
        }

        public void RemoveGameHandler(IHandleGameEvents handler)
        {
            gameObjects.Remove(handler);
        }

        public void Pause()
        {
            Paused = true;
        }

        public void Unpause()
        {
            Paused = false;
        }
    }
}
