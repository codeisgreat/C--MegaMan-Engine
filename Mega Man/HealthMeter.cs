﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Xml.Linq;
using Microsoft.Xna.Framework.Graphics;

namespace Mega_Man
{
    public class HealthMeter : IHandleGameEvents
    {
        private float value;
        private float maxvalue;
        private float tickSize;
        private Texture2D meterTexture;
        private Texture2D tickTexture;
        private int sound;
        private int tickframes;
        private int stopvalue;

        private Point tickOffset;

        private bool running;

        private float positionX;
        private float positionY;

        private bool horizontal;

        /// <summary>
        /// Do not rely on this as the actual health. It can differ while it animates. It only reflects the value present in the bar itself.
        /// </summary>
        public float Value
        {
            get
            {
                return this.value;
            }
            set
            {
                if (running && value > this.value) // tick up slowly
                {
                    tickframes = 0;
                    stopvalue = (int)value;
                    Engine.Instance.GameLogicTick -= new GameTickEventHandler(GameTick);
                    Engine.Instance.GameLogicTick += new GameTickEventHandler(GameTick);
                }
                else
                {
                    this.value = value;
                    if (this.value < 0) this.value = 0;
                }
            }
        }

        void UpTick()
        {
            tickframes++;
            if (tickframes >= 3)
            {
                tickframes = 0;
                this.value += this.tickSize;
                Engine.Instance.PlaySound(sound);
            }
        }

        public float MaxValue
        {
            get { return this.maxvalue; }
            set
            {
                this.maxvalue = value;
                this.tickSize = this.maxvalue / 28;
            }
        }

        public HealthMeter()
        {
            this.value = this.maxvalue;
            sound = Engine.Instance.LoadSoundEffect(System.IO.Path.Combine(Game.CurrentGame.BasePath, "sounds\\health.wav"), false, 1);
            running = false;
        }

        public void LoadXml(XElement node)
        {
            this.positionX = float.Parse(node.Attribute("x").Value);
            this.positionY = float.Parse(node.Attribute("y").Value);
            XAttribute imageAttr = node.Attribute("image");
            if (imageAttr == null) throw new EntityXmlException(node, "HealthMeters must have an image attribute to specify the tick image.");
            
            if (this.tickTexture != null) this.tickTexture.Dispose();
            this.tickTexture = Texture2D.FromFile(Engine.Instance.GraphicsDevice, System.IO.Path.Combine(Game.CurrentGame.BasePath, imageAttr.Value));

            XAttribute backAttr = node.Attribute("background");
            if (backAttr != null) this.meterTexture = Texture2D.FromFile(Engine.Instance.GraphicsDevice, System.IO.Path.Combine(Game.CurrentGame.BasePath, backAttr.Value));

            bool horiz = false;
            XAttribute dirAttr = node.Attribute("orientation");
            if (dirAttr != null)
            {
                horiz = (dirAttr.Value == "horizontal");
            }
            this.horizontal = horiz;

            int x = 0; int y = 0;
            XAttribute offXAttr = node.Attribute("tickX");
            if (offXAttr != null) int.TryParse(offXAttr.Value, out x);
            XAttribute offYAttr = node.Attribute("tickY");
            if (offYAttr != null) int.TryParse(offYAttr.Value, out y);

            this.tickOffset = new Point(x, y);
        }

        public void Reset()
        {
            this.value = this.maxvalue;
        }

        public void Draw(SpriteBatch batch)
        {
            Draw(batch, positionX, positionY);
        }

        public void Draw(SpriteBatch batch, float positionX, float positionY)
        {
            if (this.tickTexture != null)
            {
                int i = 0;
                int ticks = (int)Math.Round(this.value / this.tickSize);

                if (this.meterTexture != null) batch.Draw(this.meterTexture, new Microsoft.Xna.Framework.Vector2(positionX, positionY), Engine.Instance.OpacityColor);
                if (this.horizontal)
                {
                    for (int y = (int)positionX; i < ticks; i++, y += tickTexture.Width)
                    {
                        batch.Draw(tickTexture, new Microsoft.Xna.Framework.Vector2(y, positionY), Engine.Instance.OpacityColor);
                    }
                }
                else
                {
                    for (int y = 54 + (int)positionY; i < ticks; i++, y -= tickTexture.Height)
                    {
                        batch.Draw(tickTexture, new Microsoft.Xna.Framework.Vector2(positionX + tickOffset.X, y + tickOffset.Y), Engine.Instance.OpacityColor);
                    }
                }
            }
        }

        #region IHandleGameEvents Members

        public void StartHandler()
        {
            Engine.Instance.GameRender += new GameRenderEventHandler(GameRender);
            Game.CurrentGame.AddGameHandler(this);
            running = true;
        }

        public void StopHandler()
        {
            Engine.Instance.GameLogicTick -= new GameTickEventHandler(GameTick);
            Engine.Instance.GameRender -= new GameRenderEventHandler(GameRender);
            Game.CurrentGame.RemoveGameHandler(this);
            running = false;
        }

        public void GameInputReceived(GameInputEventArgs e)
        {
            throw new NotImplementedException();
        }

        public void GameTick(GameTickEventArgs e)
        {
            UpTick();
            if (this.value >= stopvalue || this.value >= maxvalue) Engine.Instance.GameLogicTick -= new GameTickEventHandler(GameTick);
        }

        public void GameRender(GameRenderEventArgs e)
        {
            this.Draw(e.Layers.ForegroundBatch, positionX, positionY);
        }

        #endregion
    }
}
